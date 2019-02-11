using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Locking;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq {

  public class LinqCommandAnalyzer {
    DynamicLinqCommand _command; 
    EntityModel _model;
    List<LambdaExpression> _includes = new List<LambdaExpression>();

    List<Type> _entityTypes = new List<Type>();
    List<Expression> _locals = new List<Expression>(); //local values and parameters
    QueryOptions _options; //OR-ed value from all sub-queries
    LockType _lockType = LockType.None;
    SqlCacheKeyBuilder _cacheKeyBuilder;

    // parameters of internal lambdas
    List<ParameterExpression> _internalParams = new List<ParameterExpression>();
    // parameters of external lambdas (Query filters)
    List<ParameterExpression> _externalParams; //initialized on the fly

    //Helper enum identifying kind of value (origin) returned by the node
    enum ValueKind {
      None, //not a value, like Lambda expression
      Constant, //constant, part of query definition
      Local,  
      Db, // identifies data record value (or derived from it)
    }

    public static void Analyze(DynamicLinqCommand command) {
      var scanner = new LinqCommandAnalyzer();
      scanner.AnalyzeCommand(command);
    }

    private void AnalyzeCommand(DynamicLinqCommand command) {
      _command = command;
      _model = command.Session.Context.App.Model;
      var maskStr = command.MemberMask?.AsHexString() ?? "?";
      _cacheKeyBuilder = new SqlCacheKeyBuilder("Linq", command.Source.ToString(), 
                              command.Operation.ToString(), "mask:", maskStr);
      try {
        AnalyzeNode(_command.Expression);
        //copy some values to command 
        command.SqlCacheKey = _cacheKeyBuilder.Key;
        command.EntityTypes = _entityTypes;
        command.LockType = _lockType;
        command.Includes = _includes;
        command.Options |= _options;
        command.Locals.AddRange(_locals);
        command.ExternalParameters = _externalParams?.ToArray();
        if(command.Locals.Count > 0)
          LinqCommandHelper.EvaluateLocals(command); 
      } catch(Exception ex) {
        ex.Data["LinqExperssion"] = _command.Expression + string.Empty;
        throw;
      }
    }

    private ValueKind AnalyzeNode(Expression node) {
      if(node == null)
        return ValueKind.None;
      //remember cache key length - we will trim all added keys/locals if this node is local value
      var savedKeyLength = GetCacheKeyLength();
      var saveLocalCount = _locals.Count;
      //TODO: optimize this using arrays? (lookup in array by node type)
      AddCacheKey(node.NodeType); //note: for local nodes this value will be popped out - see below
      var kind = AnalyzeNodeByType(node);
      if(kind == ValueKind.Local) {
        // The node is a local variable, not dependent on DB data. Its value is provided by the code, will be turned into parameter
        // The node and its children should not be part of Query cache(hash) key. We eliminate all child keys that might have been added. 
        TrimCacheKey(savedKeyLength);
        AddCacheKey("@p"); 
        if(_locals.Count > saveLocalCount)
          _locals.RemoveRange(saveLocalCount, _locals.Count - saveLocalCount);
        _locals.Add(node);
      }
      return kind;
    }

    private ValueKind AnalyzeNodeByType(Expression node) {
      if(node == null)
        return ValueKind.None;
      switch(node.NodeType) {
        case ExpressionType.Constant:
          return AnalyzeConstant((ConstantExpression)node);
        case ExpressionType.Parameter:
          return AnalyzeParameter((ParameterExpression)node);
        case ExpressionType.MemberAccess:
          return AnalyzeMember((MemberExpression)node);
        case ExpressionType.Call:
          return AnalyzeCall((MethodCallExpression)node);

        case ExpressionType.UnaryPlus:
        case ExpressionType.Negate:
        case ExpressionType.NegateChecked:
        case ExpressionType.Not:
        case ExpressionType.Convert:
        case ExpressionType.ConvertChecked:
        case ExpressionType.ArrayLength:
        case ExpressionType.TypeAs:
          var uexp = (UnaryExpression)node;
          AddCacheKey(node.NodeType.ToString());
          return AnalyzeNode(uexp.Operand);
        case ExpressionType.Quote:
          var qexp = (UnaryExpression)node;
          // no node key
          AnalyzeNode(qexp.Operand);
          return ValueKind.None;
        case ExpressionType.Add:
        case ExpressionType.AddChecked:
        case ExpressionType.Subtract:
        case ExpressionType.SubtractChecked:
        case ExpressionType.Multiply:
        case ExpressionType.MultiplyChecked:
        case ExpressionType.Divide:
        case ExpressionType.Modulo:
        case ExpressionType.Power:
        case ExpressionType.And:
        case ExpressionType.AndAlso:
        case ExpressionType.Or:
        case ExpressionType.OrElse:
        case ExpressionType.LessThan:
        case ExpressionType.LessThanOrEqual:
        case ExpressionType.GreaterThan:
        case ExpressionType.GreaterThanOrEqual:
        case ExpressionType.Equal:
        case ExpressionType.NotEqual:
        case ExpressionType.Coalesce:
        case ExpressionType.ArrayIndex:
        case ExpressionType.RightShift:
        case ExpressionType.LeftShift:
        case ExpressionType.ExclusiveOr:
          var binExp = (BinaryExpression)node;
          AddCacheKey(node.NodeType.ToString());
          return AnalyzeNodes(binExp.Left, binExp.Right, binExp.Conversion);
        case ExpressionType.TypeIs:
          var tbe = (TypeBinaryExpression)node;
          return this.AnalyzeNode(tbe.Expression);
        case ExpressionType.Conditional:
          var c = (ConditionalExpression)node;
          return AnalyzeNodes(c.Test, c.IfTrue, c.IfFalse);
        case ExpressionType.Lambda:
          var lambda = (LambdaExpression)node;
          return AnalyzeLambda(lambda);
        case ExpressionType.New:
          var n = (NewExpression)node;
          AddCacheKey(n.Constructor.DeclaringType.Name);
          var argsKind = AnalyzeNodes(n.Arguments);
          //we do not fold new-anon-type expressions into parameter, to force  to be 
          if(n.Type.IsDbPrimitive())
            // if it is simple expr like 'new DateTime(...)', we assign kind from arguments (it might be db or local)
            return argsKind;
          else
            // If it is 'new { Prop = ?..}' - anontype-new, we avoid folding it into local, even if argumens are constants
            // This is done to force SQL output clause to have all values listed in initializer - important for View and 
            // non-query LINQ statements
            return ValueKind.Db;
        case ExpressionType.NewArrayInit:
        case ExpressionType.NewArrayBounds:
          var na = (NewArrayExpression)node;
          return this.AnalyzeNodes(na.Expressions);
        case ExpressionType.Invoke:
          var iv = (InvocationExpression)node;
          var vk1 = this.AnalyzeNode(iv.Expression);
          var vk2 = this.AnalyzeNodes(iv.Arguments);
          return Max(vk1, vk2);
        case ExpressionType.MemberInit:
          var mi = (MemberInitExpression)node;
          return Max(AnalyzeNode(mi.NewExpression), AnalyzeBindings(mi.Bindings));
        case ExpressionType.ListInit:
          var li = (ListInitExpression)node;
          return Max(AnalyzeNode(li.NewExpression), AnalyzeInitializers(li.Initializers));
      }
      Util.Throw("Unknown expression type {0}", node.NodeType);
      return ValueKind.None; //never happens
    }

    private ValueKind AnalyzeLambda(LambdaExpression lambda) {
      var paramCount = lambda.Parameters.Count;
      if(paramCount > 0)
        _internalParams.AddRange(lambda.Parameters);
      AnalyzeNode(lambda.Body);
      if(paramCount > 0)
        _internalParams.RemoveRange(_internalParams.Count - paramCount, paramCount);
      return ValueKind.None;
    }

    //TODO: review this
    // parameters in lambda are not consts or local values; we return DB which well be propagated up; lambda itself will return None
    private ValueKind AnalyzeParameter(ParameterExpression node) {
      var index = _internalParams.IndexOf(node);
      if(index >= 0) {
        AddCacheKey("@P" + index); //We anonymize all internal parameters, so it does not matter how you name vars in LINQ queries (in lambdas) 
        return ValueKind.Db;
      } else {
        AddCacheKey(node.Name);
        _externalParams = _externalParams ?? new List<ParameterExpression>(); 
        if (!_externalParams.Contains(node))
          _externalParams.Add(node);
        return ValueKind.Local;
      }
    }


    private ValueKind AnalyzeConstant(ConstantExpression constNode) {
      AddCacheKey(constNode.Value);
      if(constNode.Value == null)
        return ValueKind.Constant;
      //entity set
      var entQuery = constNode.Value as EntityQuery;
      if(entQuery != null) {
        _entityTypes.Add(entQuery.ElementType);
        var lockTarget = constNode.Value as ILockTarget;
        if(lockTarget != null && lockTarget.LockType != LockType.None) {
          var lt = lockTarget.LockType; 
          AddCacheKey(lt.ToString());
          if(lt > _lockType)
            _lockType = lt; 
        }
        return ValueKind.Db;
      }
      if(!constNode.Type.IsValueTypeOrString())
        return ValueKind.Local;
      return ValueKind.Constant;
    }

    private ValueKind AnalyzeMember(MemberExpression node) {
      // check if it is subquery stored in local variable; if yes, retrieve the value and convert to constant
      IQueryable subQuery;
      if(LinqExpressionHelper.CheckSubQueryInLocalVariable(node, out subQuery)) {
        AnalyzeNode(subQuery.Expression); // we need to visit it as well, to process sub-query nodes
        return ValueKind.Db;
      }

      if(node.Type.IsSubclassOf(typeof(LinqLiteralBase))) {
        return ValueKind.Constant;
      }
      if(node.Type == typeof(SequenceDefinition)) {
        return ValueKind.Constant;
      }

      //regular member access node
      var exprValueKind = this.AnalyzeNode(node.Expression);
      AddCacheKey(node.Member.Name);
      if(node.Member.IsStaticMember())
        return ValueKind.Local;
      else
        return exprValueKind; //same as mexp.Expression's valueKind
    }

    private ValueKind AnalyzeCall(MethodCallExpression node) {
      var objValueKind = AnalyzeNode(node.Object);
      var method = node.Method;
      if(method.DeclaringType == typeof(EntityQueryExtensions)) {
        switch(method.Name) {
          case "Include":
            // Note: we do not add method name to cache key, or any args; so queries with different includes have the same key 
            // - and can be reused - which is what we want
            var lambda = ExpressionHelper.UnwrapLambda(node.Arguments[1]);
            Util.Check(lambda != null, "Invalid Include argument: {0}.", node.Arguments[1]);
            _includes.Add(lambda);
            return AnalyzeNode(node.Arguments[0]);
          case "WithOptions":
            AddCacheKey(method.Name);
            var options = (QueryOptions)ExpressionHelper.Evaluate(node.Arguments[1]);
            _options |= options;
            bool matches = method.GetGenericMethodDefinition() == EntityQueryExtensions.WithOptionsMethod;
            return AnalyzeNode(node.Arguments[0]);
        }//switch
      }
      //For all other cases method.Name is included in cache key
      AddCacheKey(method.Name);

      //Analyze child nodes
      if(method.DeclaringType == typeof(Queryable)) {
        //read QueryOptions and included queries from first argument (query)
        // One special case: Skip(n), Take(n). In LINQ expression they represented as int constants (not expressions)
        // - we will translate them into parameters, so we can use the same query for different pages.
        // This means that values of arguments should be excluded from cache key - we do it by not analyzing them.
        switch(method.Name) {
          case "Take":
          case "Skip":
            AnalyzeNode(node.Arguments[0]);// analyze only the first arg (query), but not const value
            _locals.Add(node.Arguments[1]); // int arg is turned into parameter - here we save the value to use when executing cached query definition
            return ValueKind.Db;
        }
      } //if

      if(method.DeclaringType == typeof(NativeSqlFunctionStubs)) {
        foreach(var arg in node.Arguments)
          AnalyzeNode(arg);
        return ValueKind.Db;
      }

      if(method.IsEntitySetMethod()) {
        //Add type of entity set
        AddCacheKey(method.ReturnType.GetGenericArguments()[0].Name);
        return ValueKind.Db;
      }
      //General case
      //analyze arguments
      var argKinds = AnalyzeNodes(node.Arguments);
      //result is max of obj and args
      var result = Max(objValueKind, argKinds);
      return result;
    }

    //collection analyzers
    private ValueKind AnalyzeNodes(params Expression[] expressions) {
      var result = ValueKind.None;
      for(int i = 0; i < expressions.Length; i++)
        result = Max(result, this.AnalyzeNode(expressions[i]));
      return result;
    }
    private ValueKind AnalyzeNodes<E>(ReadOnlyCollection<E> expressions) where E : Expression {
      var result = ValueKind.None;
      for(int i = 0; i < expressions.Count; i++)
        result = Max(result, this.AnalyzeNode(expressions[i]));
      return result;
    }

    private ValueKind AnalyzeInitializers(ReadOnlyCollection<ElementInit> original) {
      var result = ValueKind.None;
      for(int i = 0, n = original.Count; i < n; i++)
        result = Max(result, this.AnalyzeNodes(original[i].Arguments));
      return result;
    }

    private ValueKind AnalyzeBindings(ReadOnlyCollection<MemberBinding> original) {
      var result = ValueKind.None;
      for(int i = 0, n = original.Count; i < n; i++)
        result = Max(result, this.AnalyzeBinding(original[i]));
      return result;
    }

    private ValueKind AnalyzeBinding(MemberBinding binding) {
      switch(binding.BindingType) {
        case MemberBindingType.Assignment:
          var asm = (MemberAssignment)binding;
          return this.AnalyzeNode(asm.Expression);
        case MemberBindingType.MemberBinding:
          var mmb = (MemberMemberBinding)binding;
          return this.AnalyzeBindings(mmb.Bindings);
        case MemberBindingType.ListBinding:
          var mlb = (MemberListBinding)binding;
          return this.AnalyzeInitializers(mlb.Initializers);
        default:
          Util.Throw("Uhnandled binding type {0}", binding.BindingType);
          return ValueKind.None;//never happens
      }
    }

    private ValueKind Max(ValueKind x, ValueKind y) {
      var gt = (int)x > (int)y;
      return gt ? x : y;
    }

    #region QueryCache key builder
    private void AddCacheKey(ExpressionType nodeType) {
      _cacheKeyBuilder.Add(nodeType.AsString());
    }

    private void AddCacheKey(string key) {
      _cacheKeyBuilder.Add(key);
    }

    private void AddCacheKey(object value) {
      var s = value == null ? string.Empty : value.ToString(); 
      _cacheKeyBuilder.Add(s); 
    }

    private void TrimCacheKey(int toLength) {
      _cacheKeyBuilder.Trim(toLength);
    }
    private int GetCacheKeyLength() {
      return _cacheKeyBuilder.Length;
    }
    #endregion


  }//class
}
