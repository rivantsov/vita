using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Linq;

namespace Vita.Data.Linq.Translation {

  public static class ExpressionMutator {
    public static Expression Mutate(Expression expression, IList<Expression> newOperands) {
      var mex = expression as SqlExpression;
      if (mex != null)
        return mex.Mutate(newOperands);
      var handler = GetHandler(expression);
      var newExpr = handler.Mutator(expression, newOperands);
      return newExpr; 
    }

    public static IList<Expression> GetOperands(Expression expression) {
      var mex = expression as SqlExpression;
      if (mex != null)
        return mex.Operands.ToArray();
      var handler = GetHandler(expression);
      var ops = handler.OperandGetter(expression);
      return ops; 
    }

    static ExpressionHandler GetHandler(Expression expression) {
      if (expression == null)
        return null;
      return GetHandler(expression.GetType());
    }

    static ExpressionHandler GetHandler(Type exprType) {
/*
      // Some special cases
      var baseType = exprType.BaseType; 
      if (baseType == typeof(MethodCallExpression) || baseType == typeof(LambdaExpression) || baseType == typeof(BinaryExpression) ||
          baseType == typeof(ParameterExpression) || baseType == typeof(MemberExpression)) 
        exprType = baseType;
      if (baseType.BaseType == typeof(BinaryExpression))
        exprType = baseType.BaseType; 
 */ 
      //if (exprType.IsGenericType && typeof(LambdaExpression).IsAssignableFrom(exprType))
        //exprType = typeof(LambdaExpression);
      ExpressionHandler handler;
      if (_handlers.TryGetValue(exprType, out handler))
        return handler;
      // try base type
      if (exprType.BaseType != typeof(Expression))
        return GetHandler(exprType.BaseType);
      //error
      Vita.Common.Util.Throw("Expression handler/mutator not found for expression type {0}.", exprType);
      return null; 
    }

    // private fields
    static readonly Expression[] _empty = new Expression[] { };
    class ExpressionHandler {
      public Func<Expression, IList<Expression>> OperandGetter;
      public Func<Expression, IList<Expression>, Expression> Mutator;
    }
    static Dictionary<Type, ExpressionHandler> _handlers;
    //static constructor
    static ExpressionMutator() {
      Init(); 
    }

    static void Init() {
      _handlers = new Dictionary<Type, ExpressionHandler>();
      AddHandler<BinaryExpression>(GetBinaryOperands, MutateBinary);
      AddHandler<ConditionalExpression>(GetConditionalOperands, MutateConditional);
      AddHandler<ConstantExpression>(GetConstantOperands, MutateConstant);
      AddHandler<InvocationExpression>(GetInvocationOperands, MutateInvocation);
      AddHandler<LambdaExpression>(GetLambdaOperands, MutateLambda);
      AddHandler<ListInitExpression>(GetListInitOperands, MutateListInit);
      AddHandler<MemberExpression>(GetMemberOperands, MutateMember);
      AddHandler<MemberInitExpression>(GetMemberInitOperands, MutateMemberInit);
      AddHandler<MethodCallExpression>(GetMethodCallOperands, MutateMethodCall);
      AddHandler<NewArrayExpression>(GetNewArrayOperands, MutateNewArray);
      AddHandler<NewExpression>(GetNewOperands, MutateNew);
      AddHandler<ParameterExpression>(GetParameterOperands, MutateParameter);
      AddHandler<TypeBinaryExpression>(GetTypeBinaryOperands, MutateTypeBinary);
      AddHandler<UnaryExpression>(GetUnaryOperands, MutateUnary);
      AddHandler<DefaultExpression>(GetDefaultOperands, MutateDefault);
      AddHandler<IndexExpression>(GetIndexOperands, MutateIndex);
    }

    static void AddHandler<T>(Func<T, IList<Expression>> getter, Func<T, IList<Expression>, T> mutator) where T : Expression {
      var handler = new ExpressionHandler() {
        OperandGetter = e => getter((T)e),
        Mutator = (e, ops) => mutator((T)e, ops)      
      };
      _handlers.Add(typeof(T), handler);
    }

    #region Mutators
    static BinaryExpression MutateBinary(BinaryExpression node, IList<Expression> operands) {
      // After replacing arguments with column expressions there might be a problem with nullable columns.
      // The following code compensates for this
      return ExpressionUtil.MakeBinary(node.NodeType, operands[0], operands[1]);
    }
    static ConditionalExpression MutateConditional(ConditionalExpression node, IList<Expression> operands) {
      return Expression.Condition(operands[0], operands[1], operands[2], node.Type);
    }
    static ConstantExpression MutateConstant(ConstantExpression node, IList<Expression> operands) {
      return Expression.Constant(node.Value, node.Type); 
    }
    static InvocationExpression MutateInvocation(InvocationExpression node, IList<Expression> operands) {
      return Expression.Invoke(operands[0], operands.Skip(1));
    }
    static LambdaExpression MutateLambda(LambdaExpression node, IList<Expression> operands) {
      var count = operands.Count;
      var body = operands[count - 1];
      var prms = operands.Take(count -1).Select(p => (ParameterExpression)p); 
      return Expression.Lambda(body, prms);
    }
    static ListInitExpression MutateListInit(ListInitExpression node, IList<Expression> operands) {
      var newExpr = (NewExpression) operands[0];
      var initExprs = operands.Skip(1);
      return Expression.ListInit(newExpr, initExprs);
    }

    static MemberExpression MutateMember(MemberExpression node, IList<Expression> operands) {
      return Expression.MakeMemberAccess(operands[0], node.Member);
    }

    static MemberInitExpression MutateMemberInit(MemberInitExpression node, IList<Expression> operands) {
      var newNewExpression = operands[0] as NewExpression;
      var bindingOperands = operands.Skip(1).ToList(); 
      var newBindings = new List<MemberBinding>();
      foreach (var binding in node.Bindings) {
        var newBinding = MutateBinding(binding, bindingOperands);
        newBindings.Add(newBinding);
      }
      return node.Update(newNewExpression, newBindings);
    }

    static MemberBinding MutateBinding(MemberBinding binding, List<Expression> bindingOperands) {
      switch (binding.BindingType) {
        case MemberBindingType.Assignment:
          var asmt = (MemberAssignment)binding;
          var opExpr = bindingOperands[0];
          bindingOperands.RemoveAt(0);
          return Expression.Bind(asmt.Member, opExpr);
        case MemberBindingType.ListBinding:
          var listBnd = (MemberListBinding)binding;
          var newInits = new List<ElementInit>(); 
          foreach (var init in listBnd.Initializers) {
            var newArgs = bindingOperands.Take(init.Arguments.Count);
            bindingOperands.RemoveRange(0, init.Arguments.Count);
            var newInit = Expression.ElementInit(init.AddMethod, newArgs);
            newInits.Add(newInit); 
          }
          return Expression.ListBind(listBnd.Member, newInits); 
        case MemberBindingType.MemberBinding:
          var mmBnd = (MemberMemberBinding)binding;
          var newBnds = new List<MemberBinding>();
          foreach (var bnd in mmBnd.Bindings) {
            var newBnd = MutateBinding(bnd, bindingOperands);
            newBnds.Add(newBnd); 
          }
          return Expression.MemberBind(mmBnd.Member, newBnds); 
        default:
          return null; //never happens
      }//switch
    }//method

    static MethodCallExpression MutateMethodCall(MethodCallExpression node, IList<Expression> operands) {
      var args = operands.Skip(1); 
      return node.Update(operands[0], args); 
    }
    static NewArrayExpression MutateNewArray(NewArrayExpression node, IList<Expression> operands) {
      return node.Update(operands);
    }
    static NewExpression MutateNew(NewExpression node, IList<Expression> operands) {
      Type[] types = node.Members == null ? node.Constructor.GetParameters().Select(p=>p.ParameterType).ToArray()
                        : node.Members.Select(m => m.GetMemberType()).ToArray();
      for (int i = 0; i < operands.Count; i++) {
        operands[i] = ExpressionUtil.CheckNeedConvert(operands[i], types[i]);
      }
      return node.Update(operands);
    }

    static ParameterExpression MutateParameter(ParameterExpression node, IList<Expression> operands) {
      return node;
    }
    static TypeBinaryExpression MutateTypeBinary(TypeBinaryExpression node, IList<Expression> operands) {
      return node.Update(operands[0]);
    }
    static UnaryExpression MutateUnary(UnaryExpression node, IList<Expression> operands) {
      return node.Update(operands[0]);
    }
    static DefaultExpression MutateDefault(DefaultExpression node, IList<Expression> operands) {
      return node;
    }
    static IndexExpression MutateIndex(IndexExpression node, IList<Expression> operands) {
      var args = operands.Skip(1); 
      return node.Update(operands[0], args);
    }


    #endregion


    #region Operand getter implementations
    static IList<Expression> GetBinaryOperands(BinaryExpression node) {
      return new Expression[] { node.Left, node.Right };
    }
    static IList<Expression> GetConditionalOperands(ConditionalExpression node) {
      return new Expression[] { node.Test, node.IfTrue, node.IfFalse };
    }
    static IList<Expression> GetConstantOperands(ConstantExpression node) {
      return _empty;
    }
    static IList<Expression> GetInvocationOperands(InvocationExpression node) {
      var result = new List<Expression>();
      result.Add(node.Expression);
      result.AddRange(node.Arguments);
      return result; 
    }
    static IList<Expression> GetLambdaOperands(LambdaExpression node) {
      var result = new List<Expression>();
      result.AddRange(node.Parameters);
      result.Add(node.Body);
      return result;
    }
    static IList<Expression> GetListInitOperands(ListInitExpression node) {
      var result = new List<Expression>();
      result.Add(node.NewExpression); 
      foreach (var init in node.Initializers)
        result.AddRange(init.Arguments); 
      return result;
    }
    static IList<Expression> GetMemberOperands(MemberExpression node) {
      return new Expression[] { node.Expression }; 
    }
    static IList<Expression> GetMemberInitOperands(MemberInitExpression node) {
      var operands = new List<Expression>(); 
      operands.Add(node.NewExpression); 
      foreach (var bnd in node.Bindings)
        CollectMemberBindingOperands(bnd, operands);
      return operands; 
    }
    //not operand getter, utility method
    static void CollectMemberBindingOperands(MemberBinding binding, List<Expression> operands) {
      switch(binding.BindingType) {
        case MemberBindingType.Assignment:
          var asmt = (MemberAssignment)binding;
          operands.Add(asmt.Expression);
          break; 
        case MemberBindingType.ListBinding:
          var listBnd = (MemberListBinding)binding;
          foreach (var init in listBnd.Initializers)
            operands.AddRange(init.Arguments);
          break; 
        case MemberBindingType.MemberBinding:
          var mmBnd = (MemberMemberBinding)binding;
          foreach (var bnd in mmBnd.Bindings)
            CollectMemberBindingOperands(bnd, operands);
          break; 
      }
    }//method

    static IList<Expression> GetMethodCallOperands(MethodCallExpression node) {
      var operands = new List<Expression>();
      operands.Add(node.Object);
      operands.AddRange(node.Arguments); 
      return operands;
    }

    static IList<Expression> GetNewArrayOperands(NewArrayExpression node) {
      return node.Expressions.ToList();
    }
    static IList<Expression> GetNewOperands(NewExpression node) {
      return node.Arguments.ToList();
    }
    static IList<Expression> GetParameterOperands(ParameterExpression node) {
      return _empty;
    }
    static IList<Expression> GetTypeBinaryOperands(TypeBinaryExpression node) {
      return new Expression[] {node.Expression};
    }
    static IList<Expression> GetUnaryOperands(UnaryExpression node) {
      return new Expression[] { node.Operand };
    }
    // .DefaultExpression
    // .IndexExpression
    static IList<Expression> GetDefaultOperands(DefaultExpression node) {
      return _empty;
    }
    static IList<Expression> GetIndexOperands(IndexExpression node) {
      var result = new List<Expression>();
      result.Add(node.Object);
      result.AddRange(node.Arguments);
      return result; 
    }



    #endregion


  }//class
}
