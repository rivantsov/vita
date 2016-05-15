using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;

using Vita.Common;
using Vita.Entities.Runtime;
using Vita.Entities.Model;
using Vita.Entities;
using System.Collections;
using Vita.Entities.Locking;

namespace Vita.Entities.Linq {


  #region comments
  // Performs initial analisys of the query. 
  // The main task is to build query data - a 'minimal' information about submitted dynamic query - cache key and parameter values.
  // If prepared version of the same query is found in query cache, this previously translated query is executed with new parameter values
  // Initial analysis computes 2 things:
  //  1. QueryCacheKey - to match the the query to previously submitted queries and retrieve previously prepared query from query cache.
  //                     The cache key is based on query definition - the expression with 'masked'/excluded arguments - constants and variables
  //                      used in the expression.
  //  2. Values of query parameters - locally-evaluatable values or sub-expressions. These will be used as parameter values for 
  //                     cached query definition to execute the query.
  // Notes:
  // 1. Query cache key does not look like original query's ToString(), it is a jumbled version, like this:
  //     !Call/FirstOrDefault/!Call/Where/!Constant/EntitySet<ICoupon>/!Quote/!Lambda/!Parameter/c/!Equal/Equal/!MemberAccess/!Parameter/c/PromoCode/?p?
  // 2. The main goal here is performance - the analysis is perfomed each time the query is submitted, even if we already executed similar query and 
  // it is saved in query cache. We use a hand-crafted expression visitor here. The default Linq ExpressionVisitor is doing more work - it expects 
  // the expression tree to be modified, and it constantly checks if args have changed. 
  // Compare this case to the query tranlation process - when query is not in query cache, we go into process of translating it. Translation is 
  // not performance critical - we expect it to happen once for a particular query, then it will be saved in query cache and reused. 
  // So in translation we use regular .NET expression visitors. 
  // 3. We could use Expression.ToString() for query cache key, but it seems it is not efficient enough - it involves full tree visit, with a lot of string
  //   merges. Our solution is to build an array of node keys while we iterate the tree and then do a string.Join of all keys.
  // 4. Analysis results are put into EntityQueryData object and saved put EntityQuery's field - in case if analysis is called more than once, it is actually
  //    done only once, with reusing value saved in EntityQuery's field. 
  // Algorithm
  // The goal is to detect all 'locally-evaluatable' subexpressions that will turn into parameters. 
  //  We build 2 lists as we navigate the tree - list of node keys and list of local values. Node keys are components of cache key; local values are candidates
  // for query parameters; they are excluded from cache key as they are not part of query 'shape'. We start from tree leaf nodes, determine leaf's value kind,
  // and then propagate up the ValueKind value. When propagating and deciding on comlex node's value kind, we use 'Max' function on ValueKind enum, 
  // when combining kinds of child nodes - value kind of the node is a Max of value kinds of children (most of the time). 
  // Example: for a binary expression 'a + b', if a is ValueKind.Local and b is ValueKind.Constant, then for 'a + b' the kind is Max(Local, Constant) -> Local. 
  #endregion

  //TODO: review assigning value kind Db for parameter (AnalyzeParameter) - it might not be right for all cases

  /// <summary>Performs initial query analysis, computes cache key and values of parameters. </summary>
  public class LinqCommandAnalyzer {
    EntityModel _model; 
    LinqCommand _command;
    // Filter parameters coming from outside - for ex: clauses in EntityFilters (query/authorization filters)
    List<ParameterExpression> _externalParams = new List<ParameterExpression>();
    // parameters of internal lambdas
    List<ParameterExpression> _internalParams = new List<ParameterExpression>();
    List<LambdaExpression> _includes = new List<LambdaExpression>();

    List<Type> _entityTypes = new List<Type>(); 
    List<Expression> _locals = new List<Expression>(); 
    QueryOptions _options; //OR-ed value from all sub-queries
    LinqCommandFlags _flags;

    //Helper enum identifying kind of value (origin) returned by the node
    enum ValueKind {
      None, //not a value, like Lambda expression
      Constant, //constant, part of query definition
      Local,  // local value; parameter, not part of query definition
      Db, // identifies data record value (or derived from it)
    }
    public static LinqCommandInfo Analyze(EntityModel model, LinqCommand command) {
      // if the query was already analyzed, return the old object; otherwise analyze and save in query's field
      if (command.Info != null)
        return command.Info; 
      var analyzer = new LinqCommandAnalyzer();
      command.Info = analyzer.AnalyzeCommand(model, command);
      return command.Info;
    }

    private LinqCommandInfo AnalyzeCommand(EntityModel model, LinqCommand command) {
      _model = model; 
      _command = command;
      try {
        //include command type and options value into cache key
        AddCacheKey(command.CommandType);
        AnalyzeNode(_command.Query.Expression);
        _command.Locals = _locals;
        AddCacheKey(_options);
        var cacheKey = _cacheKeyBuilder.ToString();
        //Build command info
        var info = new LinqCommandInfo(command, _options, _flags, _entityTypes, cacheKey, _externalParams, _includes);
        info.ResultShape = GetResultShape(_command.Query.Expression.Type); 
        return info; 
      } catch(Exception ex) {
        ex.Data["QueryExperssion"] = command.Query.Expression + string.Empty;
        throw; 
      }
    }

    private ValueKind AnalyzeNode(Expression node) {
      if (node == null)
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
        AddCacheKey("?p?"); // indication of parameter
        if(_locals.Count > saveLocalCount)
          _locals.RemoveRange(saveLocalCount, _locals.Count - saveLocalCount);
        _locals.Add(node);
      }
      return kind; 
    }

    private ValueKind AnalyzeNodeByType(Expression node) {
      if (node == null)
        return ValueKind.None;
      switch (node.NodeType) {
        case ExpressionType.Constant: return AnalyzeConstant( (ConstantExpression)node);
        case ExpressionType.Parameter: return AnalyzeParameter((ParameterExpression)node);
        case ExpressionType.MemberAccess: return AnalyzeMember((MemberExpression)node);
        case ExpressionType.Call: return AnalyzeCall((MethodCallExpression)node);

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
      if (paramCount > 0)
        _internalParams.AddRange(lambda.Parameters);
      // AnalyzeNodes(lambda.Parameters);
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
        _externalParams.Add(node);
        return ValueKind.Local;
      }
    }


    private ValueKind AnalyzeConstant(ConstantExpression constNode) {
      AddCacheKey(constNode.Value);
      if (constNode.Value == null)
        return ValueKind.Constant;
      //entity set
      var entQuery = constNode.Value as EntityQuery;
      if (entQuery != null) {
        _entityTypes.Add(entQuery.ElementType);
        var lockTarget = constNode.Value as ILockTarget;
        if (lockTarget != null) {
          if (lockTarget.LockOptions.IsSet(LockOptions.ForUpdate))
            _flags |= LinqCommandFlags.WriteLock;
          if (lockTarget.LockOptions.IsSet(LockOptions.SharedRead))
            _flags |= LinqCommandFlags.ReadLock;
          if (lockTarget.LockOptions.IsSet(LockOptions.NoLock))
            _flags |= LinqCommandFlags.NoLock;
        }
        return ValueKind.Db; 
      }
      if (!constNode.Type.IsValueTypeOrString())
        return ValueKind.Local;
      return ValueKind.Constant;
    }

    private ValueKind AnalyzeMember(MemberExpression node) {
      // check if it is subquery stored in local variable; if yes, retrieve the value and convert to constant
      IQueryable subQuery;
      if (EntityQueryUtil.CheckSubQueryInLocalVariable(node, out subQuery)) {
        AnalyzeNode(subQuery.Expression); // we need to visit it as well, to process sub-query nodes
        return ValueKind.Db;
      }
      //regular member access node
      var exprValueKind = this.AnalyzeNode(node.Expression);
      AddCacheKey(node.Member.Name);
      if (node.Member.IsStaticMember())
        return ValueKind.Local;
      else
        return exprValueKind; //same as mexp.Expression's valueKind
    }

    private ValueKind AnalyzeCall(MethodCallExpression node) {
      var objValueKind = AnalyzeNode(node.Object);
      var method = node.Method;
      if (method.DeclaringType == typeof(EntityQueryExtensions)) {
        switch (method.Name) {
          case "Include":
            // Note: we do not add method name to cache key, or any args; so queries with different includes have the same key 
            // - and can be reused - which is what we want
            var constArg1 = node.Arguments[1] as ConstantExpression;
            var lambda = constArg1.Value as LambdaExpression;
            Util.Check(lambda != null, "Invalid Include argument: {0}.", constArg1.Value);
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
      if (method.DeclaringType == typeof(Queryable)) {
        //read QueryOptions and included queries from first argument (query)
        // One special case: Skip(n), Take(n). In LINQ expression they represented as int constants (not expressions)
        // - we will translate them into parameters, so we can use the same query for different pages.
        // This means that values of arguments should be excluded from cache key - we do it by not analyzing them.
        switch (method.Name) {
          case "Take":
          case "Skip":
            AnalyzeNode(node.Arguments[0]);// analyze only the first arg (query), but not const value
            _locals.Add(node.Arguments[1]); // int arg is turned into parameter - here we save the value to use when executing cached query definition
            return ValueKind.Db;
        }
      } //if
      if (method.ReturnsEntitySet()) {
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
      for (int i = 0; i < expressions.Length; i++)
        result = Max(result, this.AnalyzeNode(expressions[i]));
      return result;
    }
    private ValueKind AnalyzeNodes<E>(ReadOnlyCollection<E> expressions) where E : Expression {
      var result = ValueKind.None;
      for (int i = 0; i < expressions.Count; i++)
        result = Max(result, this.AnalyzeNode(expressions[i]));
      return result; 
    }

    private ValueKind AnalyzeInitializers(ReadOnlyCollection<ElementInit> original) {
      var result = ValueKind.None;
      for (int i = 0, n = original.Count; i < n; i++)
        result = Max(result, this.AnalyzeNodes(original[i].Arguments));
      return result; 
    }

    private ValueKind AnalyzeBindings(ReadOnlyCollection<MemberBinding> original) {
      var result = ValueKind.None;
      for (int i = 0, n = original.Count; i < n; i++)
        result = Max(result,  this.AnalyzeBinding(original[i]));
      return result;
    }

    private ValueKind AnalyzeBinding(MemberBinding binding) {
      switch (binding.BindingType) {
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

    private QueryResultShape GetResultShape(Type outType) {
      if (outType.IsInterface && _model.IsEntity(outType))
        return QueryResultShape.Entity;
      if (outType.IsGenericType) {
        var genArg0 = outType.GetGenericArguments()[0];
        if (typeof(IEnumerable).IsAssignableFrom(outType) && _model.IsEntity(genArg0))
          return QueryResultShape.EntityList;
      }
      return QueryResultShape.Object; // don't know and don't care      
    }


    #region QueryCache key builder
    // The value is used as key in the QueryCache (it is dictionary)
    StringBuilder _cacheKeyBuilder = new StringBuilder(512); //big enough for most cases, to avoid reallocation
    private void AddCacheKey(object key) {
      _cacheKeyBuilder.Append(key);
      _cacheKeyBuilder.Append("/");
    }
    private void TrimCacheKey(int toLength) {
      _cacheKeyBuilder.Remove(toLength, _cacheKeyBuilder.Length - toLength);
    }
    private int GetCacheKeyLength() {
      return _cacheKeyBuilder.Length;
    }
    #endregion


  }//class

}

