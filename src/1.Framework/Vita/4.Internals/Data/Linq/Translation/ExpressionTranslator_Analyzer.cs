
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Entities.Utilities;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities;
using Vita.Entities.Locking;

namespace Vita.Data.Linq.Translation {

  partial class ExpressionTranslator {
    public Expression Analyze(ExpressionChain expressionChain, Expression parameter, TranslationContext context) {
      Expression resultExpression = parameter;

      Expression last = expressionChain.Last();
      foreach(Expression expr in expressionChain) {
        if(expr == last)
          context.IsExternalInExpressionChain = true;
        resultExpression = this.Analyze(expr, resultExpression, context);
      }
      return resultExpression;
    }

    /// <summary>
    /// Entry point for Analyzis
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="parameter"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression Analyze(Expression expression, Expression parameter, TranslationContext context) {
      return Analyze(expression, new[] { parameter }, context);
    }

    protected virtual Expression Analyze(Expression expression, TranslationContext context) {
      return Analyze(expression, new Expression[0], context);
    }

    protected virtual Expression Analyze(Expression expression, IList<Expression> parameters, TranslationContext context) {
      switch(expression.NodeType) {
        case ExpressionType.Call:
          return AnalyzeCall((MethodCallExpression)expression, parameters, context);
        case ExpressionType.Lambda:
          return AnalyzeLambda(expression, parameters, context);
        case ExpressionType.Parameter:
          return AnalyzeParameter(expression, context);
        case ExpressionType.Quote:
          return AnalyzeQuote(expression, parameters, context);
        case ExpressionType.MemberAccess:
          return AnalyzeMember((MemberExpression)expression, context);
        case ExpressionType.Equal:
        case ExpressionType.NotEqual:
          var binExpr = (BinaryExpression)expression;
          if(binExpr.Left.Type == typeof(bool))
            return AnalyzeEqualBoolOperator(binExpr, context);
          else
            return AnalyzeEqualNonBoolOperator(binExpr, context);
        case ExpressionType.Add:
        case ExpressionType.AddChecked:
        case ExpressionType.Divide:
        case ExpressionType.Modulo:
        case ExpressionType.Multiply:
        case ExpressionType.MultiplyChecked:
        case ExpressionType.Power:
        case ExpressionType.Subtract:
        case ExpressionType.SubtractChecked:
        case ExpressionType.And:
        case ExpressionType.Or:
        case ExpressionType.ExclusiveOr:
        case ExpressionType.LeftShift:
        case ExpressionType.RightShift:
        case ExpressionType.AndAlso:
        case ExpressionType.OrElse:
        case ExpressionType.GreaterThanOrEqual:
        case ExpressionType.GreaterThan:
        case ExpressionType.LessThan:
        case ExpressionType.LessThanOrEqual:
        case ExpressionType.Coalesce:
          return AnalyzeBinaryOperator((BinaryExpression)expression, context);
        //case ExpressionType.ArrayIndex
        //case ExpressionType.ArrayLength
        case ExpressionType.Convert:
        case ExpressionType.ConvertChecked:
        case ExpressionType.Negate:
        case ExpressionType.NegateChecked:
        case ExpressionType.Not:
        //case ExpressionType.TypeAs
        case ExpressionType.UnaryPlus:
          return AnalyzeUnaryOperator((UnaryExpression)expression, context);

        case ExpressionType.New:
          return AnalyzeNewOperator((NewExpression)expression, context);
        case ExpressionType.Constant:
          return AnalyzeConstant((ConstantExpression)expression, context);
        case ExpressionType.Invoke:
          return AnalyzeInvoke((InvocationExpression)expression, parameters, context);
        case ExpressionType.MemberInit:
          return AnalyzeMemberInit((MemberInitExpression)expression, context);
        case ExpressionType.Conditional: //RI: added this
          return AnalyzeOperands(expression, context);

        case ExpressionType.Extension:
          var sqlExpr = expression as SqlExpression;
          switch(sqlExpr.SqlNodeType) {
            case SqlExpressionType.ExternalValue:
              return AnalyzeParameter(expression, context);
            case SqlExpressionType.SqlFunction:
              return AnalyzeOperands(expression, context);
          }
          break;

      }
      return expression;
    }

    protected virtual Expression AnalyzeCall(MethodCallExpression expression, IList<Expression> parameters, TranslationContext context) {
      var operands = expression.GetOperands().ToList();
      var operarandsToSkip = expression.Method.IsStatic ? 1 : 0;
      var originalParameters = operands.Skip(parameters.Count + operarandsToSkip);
      var newParameters = parameters.Union(originalParameters).ToList();

      var dt = expression.Method.DeclaringType;
      if(IsQueryable(dt) || dt == typeof(Enumerable))
        return AnalyzeQueryableCall(expression.Method, newParameters, context);
      if(dt == typeof(Vita.Entities.EntityQueryExtensions))
        return AnalyzeQueryExtensionsCall(expression.Method, newParameters, context);
      if(dt == typeof(string))
        return AnalyzeStringCall(expression.Method, newParameters, context);
      if(dt == typeof(Math))
        return AnalyzeMathCall(expression.Method, newParameters, context);
      if(dt == typeof(NativeSqlFunctionStubs))
        return AnalyzeNativeDbFunction(expression, newParameters, context);
      if (IsCustomSqlSnippet(expression.Method, out var sqlSnippet))
        return AnalyzeCustomSqlSnippet(sqlSnippet, newParameters, context);
      return AnalyzeUnknownCall(expression, newParameters, context);
    }

    protected virtual Expression AnalyzeQueryExtensionsCall(MethodInfo method, IList<Expression> parameters, TranslationContext context) {
      switch(method.Name) {
        case nameof(EntityQueryExtensions.OrderBy):
          throw new Exception($"Method EntityQueryExtensions.OrderBy may not be used inside query expression(s). Apply method to EntitySet<T> first " +
                               "and then use the resulting query inside the parent query expression.");
        default:
          // Ignore the method, analyze the source 'object'
          return Analyze(parameters[0], context);
      }
    }

    private Expression AnalyzeQueryableCall(MethodInfo method, IList<Expression> parameters, TranslationContext context) {
      var dt = method.DeclaringType;
      //Check for IEnumerable is to cover entity lists in members (like book.Authors.Contains(a))
      if(!(IsQueryable(dt) || dt == typeof(Enumerable)))
        return null;
      // all methods to handle are listed here:
      // ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/fxref_system.core/html/2a54ce9d-76f2-81e2-95bb-59740c85386b.htm
      switch(method.Name) {
        case "AsQueryable":
          return AnalyzeAsQueryable(parameters, context);
        case "All":
          return AnalyzeAll(parameters, context);
        case "Any":
          return AnalyzeAny(parameters, context);
        case "DefaultIfEmpty":
          return AnalyzeOuterJoin(parameters, context);
        case "Distinct":
          return AnalyzeDistinct(parameters, context);
        case "First":
        case "FirstOrDefault":
          return AnalyzeScalar(method.Name, 1, parameters, context);
        case "GroupBy":
          return AnalyzeGroupBy(method, parameters, context);
        case "GroupJoin":
          return AnalyzeGroupJoin(parameters, context);
        case "Join":
          return AnalyzeJoin(parameters, context);
        case "Last":
        case "LastOrDefault":
          return AnalyzeScalar(method.Name, null, parameters, context);
        case "OrderBy":
        case "ThenBy":
          return AnalyzeOrderBy(parameters, false, context);
        case "OrderByDescending":
        case "ThenByDescending":
          return AnalyzeOrderBy(parameters, true, context);
        case "Select":
          return AnalyzeSelect(parameters, context);
        case "SelectMany":
          return AnalyzeSelectMany(parameters, context);
        case "Single":
        case "SingleOrDefault":
          return AnalyzeScalar(method.Name, 2, parameters, context);
        case "Skip":
          return AnalyzeSkip(parameters, context);
        case "Take":
          return AnalyzeTake(parameters, context);
        case "Where":
          return AnalyzeWhere(parameters, context);
        // Aggregates
        case "Average":
          return AnalyzeAggregate(AggregateType.Average, parameters, context);
        case "Contains":
          return AnalyzeContains(parameters, context);
        case "Count":
        case "LongCount":
          return AnalyzeAggregate(AggregateType.Count, parameters, context);
        case "Sum":
          return AnalyzeAggregate(AggregateType.Sum, parameters, context);
        case "Max":
          return AnalyzeAggregate(AggregateType.Max, parameters, context);
        case "Min":
          return AnalyzeAggregate(AggregateType.Min, parameters, context);
       
        // multi-set ops
        case nameof(Queryable.Concat):
          return AnalyzeUnionIntersect(MultiSetType.UnionAll, parameters, context);
        case nameof(Queryable.Except):
          return AnalyzeUnionIntersect(MultiSetType.Except, parameters, context);
        case nameof(Queryable.Intersect):
          return AnalyzeUnionIntersect(MultiSetType.Intersect, parameters, context);
        case nameof(Queryable.Union):
          return AnalyzeUnionIntersect(MultiSetType.Union, parameters, context);

        default:
          //if(IsQueryable(method.DeclaringType))
          Util.Throw("Queryable/Enumerable method {0} not supported.", method.Name);
          return null;
      }
    }

    private Expression AnalyzeStringCall(MethodInfo method, IList<Expression> parameters, TranslationContext context) {
      if(method.DeclaringType != typeof(string))
        return null;
      switch(method.Name) {
        case "Contains":
          return AnalyzeLike(parameters, context);
        case "EndsWith":
          return AnalyzeLikeEnd(parameters, context);
        case "StartsWith":
          return AnalyzeLikeStart(parameters, context);
        case "Substring":
          return AnalyzeSubString(parameters, context);
        case "ToLower":
          return AnalyzeToLower(parameters, context);
        case "ToString":
          return AnalyzeToString(method, parameters, context);
        case "ToUpper":
          return AnalyzeToUpper(parameters, context);
        case "Trim":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Trim, parameters, context);
        case "TrimEnd":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.RTrim, parameters, context);
        case "TrimStart":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.LTrim, parameters, context);
        default:
          Util.Throw("Method not supported in LINQ: String.{0}.", method.Name);
          return null;
      }
    }

    private Expression AnalyzeMathCall(MethodInfo method, IList<Expression> parameters, TranslationContext context) {
      if(method.DeclaringType != typeof(System.Math))
        return null;
      switch(method.Name) {
        case "Abs":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Abs, parameters, context);
        case "Exp":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Exp, parameters, context);
        case "Floor":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Floor, parameters, context);
        case "Round":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Round, parameters, context);
        case "Sign":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Sign, parameters, context);
        case "Sqrt":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Sqrt, parameters, context);
        case "Log":
          return AnalyzeLog(parameters, context);
        case "Log10":
          return AnalyzeSqlFunctionExpression(SqlFunctionType.Log, parameters, context);
        default:
          Util.Throw("S0133: Implement QueryMethod Math.{0}.", method.Name);
          return null;
      }
    }

    private Expression AnalyzeNativeDbFunction(MethodCallExpression expression, IList<Expression> parameters, TranslationContext context) {
      var method = expression.Method;
      switch(method.Name) {
        case nameof(NativeSqlFunctionStubs.NextValue):
          //get the sequence instance
          var seq = ExpressionHelper.Evaluate(parameters[0]);
          var seqConst = Expression.Constant(seq);
          return new SqlFunctionExpression(SqlFunctionType.SequenceNextValue, expression.Type, new Expression[] { seqConst });
        default:
          Util.Throw("Unknown native db function {0}", method.Name);
          return null;
      }

    }
    private Expression AnalyzeUnknownCall(MethodCallExpression expression, IList<Expression> parameters, TranslationContext context) {
      var method = expression.Method;
      switch(method.Name) {
        case nameof(object.ToString):
          return AnalyzeToString(method, parameters, context);
        case nameof(IList.Contains):
          // handle List.Contains
          if(method.DeclaringType.IsListOrArray())
            // AnalyzeContains handles static Queryable.Contains which expects 2 parameters
            return AnalyzeContains(new Expression[] { expression.Object, expression.Arguments[0] }, context);
          else
            Util.Throw("Unsupported version of Contains method.");
          break;
        case "NewGuid":
          if(method.DeclaringType == typeof(Guid))
            return new SqlFunctionExpression(SqlFunctionType.NewGuid, typeof(Guid));
          else
            goto default;

        case nameof(ExpressionMaker.GetPropertyStub): //generalized way of accessing entity prop: book.Editor_Id
          if(method.DeclaringType == typeof(ExpressionMaker)) {
            var tableExpr = (TableExpression)Analyze(parameters[0], context);
            var constMemberName = (ConstantExpression)parameters[1];
            var memberName = (string)constMemberName.Value;
            var columnExpr = RegisterColumnByMemberName(tableExpr, memberName, context);
            return columnExpr;
          }
          break;

        default:
          //TODO: add support for custom functions through Linq engine extensions
          Util.Throw("Function {0} not supported in queries", method.Name);
          break;

      }

      var args = new List<Expression>();
      foreach(var arg in expression.Arguments) {
        Expression newArg = arg;
        var pe = arg as ParameterExpression;
        if(pe != null) {
          if(!context.LambdaParameters.TryGetValue(pe.Name, out newArg))
            throw new NotSupportedException("Do not currently support expression: " + expression);
        } else
          newArg = Analyze(arg, context);
        args.Add(newArg);
      }
      return Expression.Call(expression.Object, expression.Method, args);
    }

    protected virtual Expression AnalyzeLog(IList<Expression> parameters, TranslationContext context) {
      if(parameters.Count == 1)
        return CreateSqlFunction(SqlFunctionType.Ln, parameters.Select(p => Analyze(p, context)).ToArray());
      else if(parameters.Count == 2)
        return CreateSqlFunction(SqlFunctionType.Log, parameters.Select(p => Analyze(p, context)).ToArray());
      else
        throw new NotSupportedException();
    }

    protected virtual Expression AnalyzeSqlFunctionExpression(SqlFunctionType functionType, IList<Expression> parameters, TranslationContext context) {
      return CreateSqlFunction(functionType, parameters.Select(p => Analyze(p, context)).ToArray());
    }


    protected virtual Expression AnalyzeToString(MethodInfo method, IList<Expression> parameters, TranslationContext context) {
      if(parameters.Count != 1)
        throw new ArgumentException();

      Expression parameter = parameters.First();
      if(parameter.Type.IsNullableValueType())
        parameter = Analyze(Expression.Convert(parameter, parameter.Type.GetUnderlyingStorageClrType()), context);

      var parameterToHandle = Analyze(parameter, context);
      if(parameterToHandle is ExternalValueExpression)
        Util.Throw("ToString() method is not supported for query parameter.");
      /*
      bool insideWhere = context.CallStack.Count > 0 && context.CallStack.Peek().Name == "Where";
      if(insideWhere)
        Util.Throw("ToString() method is not supported inside WHERE clause.");
        */
      bool canHandle = parameter.Type.IsDbPrimitive() || parameterToHandle.Type == typeof(string);
      if(!canHandle)
        Util.Throw("ToString() can only be translated for primitive types. Type {0} is not primitive.", parameter.Type);
      // RI: new method, based on object.ToString 
      var convertMethod = LinqExpressionHelper.GetToStringConverter(parameterToHandle.Type);
      return Expression.Convert(parameterToHandle, typeof(string), convertMethod); // 
    }

    /// <summary>
    /// Limits selection count
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeTake(IList<Expression> parameters, TranslationContext context) {
      AddLimit(Analyze(parameters[1], context), context);
      return Analyze(parameters[0], context);
    }

    protected virtual void AddLimit(Expression limit, TranslationContext context) {
      var previousLimit = context.CurrentSelect.Limit;
      if(previousLimit != null)
        context.CurrentSelect.Limit = MergeLimits(previousLimit, limit);
      else
        context.CurrentSelect.Limit = limit;
    }

    //FirstOrDefault sets Limit=1; if there's already a limit expression, 
    private Expression MergeLimits(Expression limit1, Expression limit2) {
      int valueLimit;
      if(limit1.IsConst(out valueLimit) && valueLimit <= 1)
        return limit1;
      if(limit2.IsConst(out valueLimit) && valueLimit <= 1)
        return limit2;
      //othersie throw
      Util.Throw("Multiple limit(Take) clauses not supported.");
      return null; //never happens
    }

    /// <summary>
    /// Skip selection items
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeSkip(IList<Expression> parameters, TranslationContext context) {
      AddOffset(Analyze(parameters[1], context), context);
      return Analyze(parameters[0], context);
    }

    protected virtual void AddOffset(Expression offset, TranslationContext context) {
      var previousOffset = context.CurrentSelect.Offset;
      if(previousOffset != null)
        context.CurrentSelect.Offset = Expression.Add(offset, previousOffset);
      else
        context.CurrentSelect.Offset = offset;
    }

    /// <summary>
    /// Registers a scalar method call for result
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="limit"></param>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeScalar(string methodName, int? limit, IList<Expression> parameters, TranslationContext context) {
      if(limit.HasValue)
        AddLimit(Expression.Constant(limit.Value), context);
      var table = Analyze(parameters[0], context);
      if(parameters.Count > 1) {
        var predicate = Analyze(parameters[1], parameters, context);
        RegisterWhere(predicate, context);
      }
      //Create query results processor
      var rowType = parameters[0].Type;
      //Special case, when First is used on List property: book.Authors.First() inside more complex query - this is not supported.
      //           if(rowType.IsGenericType)
      //              Util.Throw("Method '{0}' is not allowed in this context.", methodName);
      context.CurrentSelect.RowListProcessor = RowListProcessor.CreateFirstSingleLast(methodName, rowType);
      return table;
    }

    protected virtual Expression AnalyzeAggregate(AggregateType aggregateType, IList<Expression> parameters,
                                                        TranslationContext context) {

      if(context.IsExternalInExpressionChain) {
        var operand0 = Analyze(parameters[0], context);
        Expression projectionOperand;

        if(context.CurrentSelect.Operands.Count() > 0 || context.CurrentSelect.Group.Count > 0) {
          // No TableInfo in projection
          operand0 = new SubSelectExpression(context.CurrentSelect, operand0.Type, "source", null);
          context.NewParentSelect();
          context.CurrentSelect.Tables.Add(operand0 as TableExpression);
        }

        // basically, we have three options for aggregate methods:
        // - projection on grouped table (1 operand, a GroupExpression)
        // - projection on grouped column (2 operands, GroupExpression and ColumnExpression)
        // - projection on table/column, with optional restriction
        var groupOperand0 = operand0 as GroupExpression;
        if(groupOperand0 != null) {
          if(parameters.Count > 1)
            projectionOperand = Analyze(parameters[1], groupOperand0.GroupedExpression, context);
          else
            projectionOperand = Analyze(groupOperand0.GroupedExpression, context);
        } else {
          if(parameters.Count > 1)
            projectionOperand = Analyze(parameters[1], operand0, context);
          else
            projectionOperand = operand0;
          if(aggregateType == AggregateType.Count && parameters.Count > 1) {
            RegisterWhere(projectionOperand, context);
            projectionOperand = null;
          }
        }

        if(projectionOperand is TableExpression)
          projectionOperand = RegisterTable((TableExpression)projectionOperand, context);

        if(groupOperand0 != null) {
          var childColumns = GetChildColumns(projectionOperand, context);
          projectionOperand = new GroupExpression(projectionOperand, groupOperand0.KeyExpression, childColumns);
        }

        var opList = new List<Expression>();
        //opList.Add(projectionOperand); // RI: 12/17, do not add table as operand, predicate only
        if(aggregateType != AggregateType.Count && projectionOperand != null)
          opList.Add(projectionOperand);
        return CreateAggregate(aggregateType, opList.ToArray());
        // end if ExternalInExprChain
      } else {
        var subQueryContext = context.NewSelect();
        var tableExpression = Analyze(parameters[0], subQueryContext);

        //RI: new stuff - handling grouping with aggregates
        //if (IsAggregate(specialExpressionType)) {
        var grpExpr = tableExpression as GroupExpression;
        var srcTable = grpExpr == null ? tableExpression : grpExpr.GroupedExpression;
        AggregateExpression specialExpr;
        if(parameters.Count > 1) {
          var predicate = Analyze(parameters[1], srcTable, subQueryContext);
          if(aggregateType == AggregateType.Count) {
            RegisterWhere(predicate, context);
            predicate = null;
            specialExpr = CreateAggregate(aggregateType);
          } else
            specialExpr = CreateAggregate(aggregateType, predicate);
        } else {
          specialExpr = CreateAggregate(aggregateType);
        }
        // If subQuery context has no tables added, it is not a subquery, it's just an aggregate function over 'main' table
        var currSelect = subQueryContext.CurrentSelect;
        if(currSelect.Tables.Count == 0)
          return specialExpr;
        //this is a real subquery, so mutate and return the current select from this context
        currSelect = currSelect.ChangeOperands(new Expression[] { specialExpr }, currSelect.Operands);
        return currSelect;
        //}
        //RI: end my special code
      }
    }

    /// <summary>
    /// Entry point for a Select()
    /// static Select(this Expression table, λ(table))
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeSelect(IList<Expression> parameters, TranslationContext context) {
      // just call back the underlying lambda (or quote, whatever)
      Expression result = Analyze(parameters[1], parameters[0], context);

      // RI: changed the following to explicitly check if table included (instead checking for Count==0)
      //TableExpression table = parameters[0] as TableExpression;
      // if (table != null && context.CurrentSelect.Tables.Count == 0)
      // RegisterTable(table, context);
      // RI: Special case - for queries like 'books.Select(b => 1)', no book columns are in output, so books table is not registered - this results in invalid query.
      // to compensate for this, we check and register table explicitly. We need to check for both TableExpression and GroupExpression
      var p0 = parameters[0];
      TableExpression table = null;
      if(p0 is GroupExpression) {
        var gex = (GroupExpression)p0;
        table = gex.GroupedExpression as TableExpression;
      } else
        table = p0 as TableExpression;
      if(table != null && !context.CurrentSelect.Tables.Contains(table))
        table = RegisterTable(table, context);
      return result;
    }

    /// <summary>
    /// Entry point for a Where()
    /// static Where(this Expression table, λ(table))
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeWhere(IList<Expression> parameters, TranslationContext context) {
      var tablePiece = parameters[0];
      if(!(tablePiece is TableExpression))
        tablePiece = Analyze(tablePiece, context);
      var where = Analyze(parameters[1], tablePiece, context);
      //RI: bit/bool
      where = LinqExpressionHelper.CheckNeedConvert(where, typeof(bool));
      RegisterWhere(where, context);
      return tablePiece;
    }

    /// <summary>
    /// Handling a lambda consists in:
    /// - filling its input parameters with what's on the stack
    /// - using the body (parameters are registered in the context)
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeLambda(Expression expression, IList<Expression> parameters, TranslationContext context) {
      var lambdaExpression = expression as LambdaExpression;
      // for a lambda, first parameter is body, others are input parameters
      // so we create a parameters stack
      for(int parameterIndex = 0; parameterIndex < lambdaExpression.Parameters.Count; parameterIndex++) {
        var parameterExpression = lambdaExpression.Parameters[parameterIndex];
        context.LambdaParameters[parameterExpression.Name] = Analyze(parameters[parameterIndex], context);
      }
      // we keep only the body, the header is now useless
      // and once the parameters have been substituted, we don't pass one anymore
      return Analyze(lambdaExpression.Body, context);
    }

    /// <summary>
    /// When a parameter is used, we replace it with its original value
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeParameter(Expression expression, TranslationContext context) {
      if(expression is ExternalValueExpression) {
        var inpPrm = (ExternalValueExpression)expression;
        inpPrm.SqlUseCount++;
        return inpPrm;
      }
      // first check input QUERY parameters (top-level lambda)
      var inputPrm = context.ExternalValues.FirstOrDefault(ip => ip.SourceExpression == expression);
      if(inputPrm != null) {
        inputPrm.SqlUseCount++;
        return inputPrm;
      }

      var prmExpr = (ParameterExpression)expression;
      Expression unaliasedExpression;
      var parameterName = GetParameterName(expression);
      context.LambdaParameters.TryGetValue(parameterName, out unaliasedExpression);
      if(unaliasedExpression == null)
        Util.Throw("Can not find parameter '{0}'", parameterName);

      #region set alias helper

      // for table...
      var unaliasedTableExpression = unaliasedExpression as TableExpression;
      if(unaliasedTableExpression != null && unaliasedTableExpression.Alias == null)
        unaliasedTableExpression.Alias = CleanupNameForAlias(parameterName);
      // .. or column
      var unaliasedColumnExpression = unaliasedExpression as ColumnExpression;
      if(unaliasedColumnExpression != null && unaliasedColumnExpression.Alias == null)
        unaliasedColumnExpression.Alias = CleanupNameForAlias(parameterName);

      #endregion

      //var groupByExpression = unaliasedExpression as GroupByExpression;
      //if (groupByExpression != null)
      //    unaliasedExpression = groupByExpression.ColumnExpression.Table;

      return unaliasedExpression;
    }

    private string CleanupNameForAlias(string name) {
      if(name == null)
        return name;
      return name.Replace("@", string.Empty);
    }

    /// <summary>
    /// Analyzes a member access.
    /// This analyzis is down to top: the highest identifier is at bottom
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeMember(MemberExpression expression, TranslationContext context) {
      // RI: added this optimization
      if(expression.Expression != null && expression.Expression.NodeType == ExpressionType.MemberAccess) {
        // possible optimization: convert 'book.Publisher.Id' to 'book.Publisher_id', to avoid unnecessary joins
        var optExpression = OptimizeChainedMemberAccess(expression, context);
        if(optExpression != null)
          return optExpression;
      }

      Expression objectExpression = null;
      var memberInfo = expression.Member;
      var memberType = memberInfo.GetMemberReturnType();
      var memberName = memberInfo.Name;

      //maybe is a static member access like DateTime.Now
      bool isStaticMemberAccess = expression.Member.IsStaticMember();
      if(!isStaticMemberAccess) {
        // special case - expr like book.Authors.Count
        if (memberName == "Count") {
          var objExprType = expression.Expression.Type;
          if (objExprType.IsGenericType && typeof(IList<>).IsAssignableFrom(objExprType.GetGenericTypeDefinition()))
            return AnalyzeAggregate(AggregateType.Count, new[] { expression.Expression }, context);
        }
        objectExpression = Analyze(expression.Expression, context);

      }

      //RI: check if objectExpression is a subquery (EntityQuery) wrapped into a constant. We get this in join queries that use sub-queres
      // saved in local variables
      if (objectExpression.NodeType == ExpressionType.Constant &&
           typeof(EntityQuery).IsAssignableFrom(objectExpression.Type)) {
        var constExpr = (ConstantExpression)objectExpression;
        var entQuery = constExpr.Value as EntityQuery;
        objectExpression = Analyze(entQuery.Expression, context);
      }
      // then see what we can do, depending on object type
      // - DerivedTable --> then the result is a table
      // - Table --> the result may be a column or a join
      // - Object --> external parameter or table (can this happen here? probably not... to be checked)


      if(objectExpression is DerivedTableExpression) {
        var derivedTableExpression = (DerivedTableExpression)objectExpression;
        return derivedTableExpression.GetMappedExpression(memberInfo);
      }

      if(objectExpression is GroupExpression) {
        if(memberName == "Key")
          return ((GroupExpression)objectExpression).KeyExpression;
      }

      // if object is a table, then we need a column, or an association
      if(objectExpression is TableExpression) {
        var tableExpression = (TableExpression)objectExpression;
        if(!memberType.IsDbPrimitive() && memberType.IsEntitySequence()) {
          var listMemberExpr = AnalyzeEntityListMember(tableExpression, (PropertyInfo)memberInfo, context);
          if(listMemberExpr != null)
            return listMemberExpr;
        }
        //entity reference
        if(_dbModel.EntityModel.IsEntity(memberType)) {
          var refMemberInfo = tableExpression.TableInfo.Entity.GetMember(memberName, throwIfNotFound: true);
          var queryAssociationExpression = RegisterAssociation(tableExpression, refMemberInfo, context);
          if(queryAssociationExpression != null)
            return queryAssociationExpression;
        }
        // try to map to column; will throw if member cannot be mapped.
        var queryColumnExpression = RegisterColumnByMemberName(tableExpression, memberName, context);
        return queryColumnExpression;
      }


      // if object is parameter, then we have parameter-derived value
      if(objectExpression is ExternalValueExpression)
        return DeriveMemberAccessParameter((ExternalValueExpression)objectExpression, memberInfo, context);

      // we have here a special cases for nullables
      if(!isStaticMemberAccess && objectExpression.Type != null && objectExpression.Type.IsNullableValueType()) {
        // Value means we convert the nullable to a value --> use Convert instead (works both on CLR and SQL, too)
        if(memberInfo.Name == "Value")
          return Expression.Convert(objectExpression, memberType);
        // HasValue means not null (works both on CLR and SQL, too)
        if(memberInfo.Name == "HasValue")
          return CreateSqlFunction(SqlFunctionType.IsNotNull, objectExpression);
      }


      if(memberInfo.DeclaringType == typeof(DateTime))
        return AnalyzeDateTimeMemberAccess(objectExpression, memberInfo, isStaticMemberAccess);

      // TODO: make this expresion safe (objectExpression can be null here)
      if(objectExpression.Type == typeof(TimeSpan))
        return AnalyzeTimeSpanMemberAccess(objectExpression, memberInfo);


      if(objectExpression is MemberInitExpression) {
        var foundExpression = OptimizeMemberGetFromMemberInit((MemberInitExpression)objectExpression, memberInfo, context);
        if(foundExpression != null)
          return foundExpression;
      }

      return AnalyzeCommonMember(objectExpression, memberInfo, context);
    }

    private ExternalValueExpression DeriveInputParameter(ExternalValueExpression oldPrm, Expression newSource, TranslationContext context) {
      oldPrm.SqlUseCount--;
      var newPrm = new ExternalValueExpression(newSource);
      newPrm.SqlUseCount++;
      context.ExternalValues.Add(newPrm);
      return newPrm;
    }
    private ExternalValueExpression DeriveMemberAccessParameter(ExternalValueExpression oldPrm, MemberInfo memberInfo, TranslationContext context) {
      MemberExpression newSource = Expression.MakeMemberAccess(oldPrm.SourceExpression, memberInfo);
      Expression safeNewSource = newSource;
      if(!memberInfo.IsStaticMember() && oldPrm.Type.IsInterface)
        safeNewSource = MakeSafeEntityParameterMemberAccess(newSource);
      return DeriveInputParameter(oldPrm, safeNewSource, context);
    }

    private static Expression MakeSafeEntityParameterMemberAccess(MemberExpression node) {
      Type resultType = node.Type;
      if(!resultType.IsNullable())
        resultType = typeof(Nullable<>).MakeGenericType(node.Type);
      var ifTest = Expression.Equal(node.Expression, Expression.Constant(null, node.Expression.Type));
      var defaultValueExpr = Expression.Constant(null, resultType);
      var nodeObj = Expression.Convert(node, resultType);
      var ifExpr = Expression.Condition(ifTest, defaultValueExpr, nodeObj, resultType);
      return ifExpr;
    }



    // TODO: (RI:) should be removed most likely
    protected Expression AnalyzeTimeSpanMemberAccess(Expression objectExpression, MemberInfo memberInfo) {
      //A timespan expression can be only generated in a c# query as a DateTime difference, as a function call return or as a paramter
      //this case is for the DateTime difference operation

      if(!(objectExpression is BinaryExpression))
        throw new NotSupportedException();

      var operands = objectExpression.GetOperands();

      bool absoluteSpam = memberInfo.Name.StartsWith("Total");
      string operationKey = absoluteSpam ? memberInfo.Name.Substring(5) : memberInfo.Name;

      Expression currentExpression;
      switch(operationKey) {
        case "Milliseconds":
          currentExpression = Expression.Convert(CreateSqlFunction(SqlFunctionType.DateDiffInMilliseconds, operands.First(), operands.ElementAt(1)), typeof(double));
          break;
        case "Seconds":
          currentExpression = Expression.Divide(
              Expression.Convert(CreateSqlFunction(SqlFunctionType.DateDiffInMilliseconds, operands.First(), operands.ElementAt(1)), typeof(double)),
              Expression.Constant(1000.0));
          break;
        case "Minutes":
          currentExpression = Expression.Divide(
                  Expression.Convert(CreateSqlFunction(SqlFunctionType.DateDiffInMilliseconds, operands.First(), operands.ElementAt(1)), typeof(double)),
                  Expression.Constant(60000.0));
          break;
        case "Hours":
          currentExpression = Expression.Divide(
                  Expression.Convert(CreateSqlFunction(SqlFunctionType.DateDiffInMilliseconds, operands.First(), operands.ElementAt(1)), typeof(double)),
                  Expression.Constant(3600000.0));
          break;
        case "Days":
          currentExpression = Expression.Divide(
                  Expression.Convert(CreateSqlFunction(SqlFunctionType.DateDiffInMilliseconds, operands.First(), operands.ElementAt(1)), typeof(double)),
                  Expression.Constant(86400000.0));
          break;
        default:
          throw new NotSupportedException(string.Format("The operation {0} over the TimeSpan isn't currently supported", memberInfo.Name));
      }

      if(!absoluteSpam) {
        switch(memberInfo.Name) {
          case "Milliseconds":
            currentExpression = Expression.Convert(Expression.Modulo(Expression.Convert(currentExpression, typeof(long)), Expression.Constant(1000L)), typeof(int));
            break;
          case "Seconds":
            currentExpression = Expression.Convert(Expression.Modulo(Expression.Convert(currentExpression, typeof(long)),
                                                  Expression.Constant(60L)), typeof(int));
            break;
          case "Minutes":
            currentExpression = Expression.Convert(Expression.Modulo(Expression.Convert(currentExpression, typeof(long)),
                                                    Expression.Constant(60L)), typeof(int));
            break;
          case "Hours":
            currentExpression = Expression.Convert(Expression.Modulo(Expression.Convert(
                                                                            currentExpression, typeof(long)),
                                                    Expression.Constant(24L)), typeof(int));
            break;
          case "Days":
            currentExpression = Expression.Convert(currentExpression, typeof(int));
            break;
        }

      }
      return currentExpression;
    }

    protected Expression AnalyzeDateTimeMemberAccess(Expression objectExpression, MemberInfo memberInfo, bool isStaticMemberAccess) {
      var outType = memberInfo.GetMemberReturnType();
      if(isStaticMemberAccess) {
        if(memberInfo.Name == "Now")
          return CreateSqlFunction(SqlFunctionType.Now);
        else
          throw new NotSupportedException(string.Format("DateTime Member access {0} not supported", memberInfo.Name));
      } else {
        switch(memberInfo.Name) {
          case "Year":
            return CreateSqlFunction(SqlFunctionType.Year, objectExpression);
          case "Month":
            return CreateSqlFunction(SqlFunctionType.Month, objectExpression);
          case "Day":
            return CreateSqlFunction(SqlFunctionType.Day, objectExpression);
          case "Hour":
            return CreateSqlFunction(SqlFunctionType.Hour, objectExpression);
          case "Minute":
            return CreateSqlFunction(SqlFunctionType.Minute, objectExpression);
          case "Second":
            return CreateSqlFunction(SqlFunctionType.Second, objectExpression);
          case "Millisecond":
            return CreateSqlFunction(SqlFunctionType.Millisecond, objectExpression);
          case "Date":
            return CreateSqlFunction(SqlFunctionType.Date, objectExpression);
          case "TimeOfDay":
            return CreateSqlFunction(SqlFunctionType.Time, objectExpression);
          case "Week":
            return CreateSqlFunction(SqlFunctionType.Week, objectExpression);
          default:
            throw new NotSupportedException(string.Format("DateTime Member access {0} not supported", memberInfo.Name));
        }
      }
    }

    /// <summary>
    /// This method analyzes the case of a new followed by a member access
    /// for example new "A(M = value).M", where the Expression can be reduced to "value"
    /// Caution: it may return null if no result is found
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="memberInfo"></param>
    /// <param name="context"></param>
    /// <returns>A member initializer or null</returns>
    protected virtual Expression OptimizeMemberGetFromMemberInit(MemberInitExpression expression, MemberInfo memberInfo,
                                                   TranslationContext context) {
      foreach(var binding in expression.Bindings) {
        var memberAssignment = binding as MemberAssignment;
        if(memberAssignment != null) {
          if(memberAssignment.Member == memberInfo)
            return memberAssignment.Expression;
        }
      }
      return null;
    }

    protected virtual Expression AnalyzeCommonMember(Expression objectExpression, MemberInfo memberInfo, TranslationContext context) {
      if(typeof(string).IsAssignableFrom(objectExpression.Type)) {
        switch(memberInfo.Name) {
          case "Length":
            return CreateSqlFunction(SqlFunctionType.StringLength, objectExpression);
        }
      }
      //Util.Throw("S0324: Don't know how to handle Piece");
      return Expression.MakeMemberAccess(objectExpression, memberInfo);
    }

    /// <summary>
    /// A Quote creates a new local context, outside which created parameters disappear
    /// This is why we clone the context
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeQuote(Expression expression, IList<Expression> parameters, TranslationContext context) {
      var builderContextClone = context.NewQuote();
      UnaryExpression unExpr = (UnaryExpression)expression;
      return Analyze(unExpr.Operand, parameters, builderContextClone);
    }

    protected virtual Expression AnalyzeOperands(Expression expression, TranslationContext context) {
      var operands = expression.GetOperands().ToList();
      var newOperands = new List<Expression>();
      for(int operandIndex = 0; operandIndex < operands.Count; operandIndex++) {
        var op = operands[operandIndex];
        var newOp = Analyze(op, context);
        newOperands.Add(newOp);
      }
      return expression.ChangeOperands(newOperands, operands);
    }

    protected virtual Expression AnalyzeUnaryOperator(UnaryExpression expression, TranslationContext context) {
      string parameterName;
      if(expression.NodeType == ExpressionType.Convert &&
              expression.Method == null &&
              (parameterName = GetParameterName(expression.Operand)) != null) {
        Expression unaliasedExpression;
        context.LambdaParameters.TryGetValue(parameterName, out unaliasedExpression);
        var unaliasedTableExpression = unaliasedExpression as TableExpression;
        if(unaliasedTableExpression != null)
          return unaliasedTableExpression;
      }
      var newOp = Analyze(expression.Operand, context);
      if(expression.Type == typeof(bool))
        newOp = CheckBoolBitExpression(newOp); //convert it if it is bit field
      return expression.ChangeOperands(new Expression[] { newOp }, new Expression[] { expression.Operand });
    }

    // comparing bools - special case
    protected virtual Expression AnalyzeEqualBoolOperator(BinaryExpression expression, TranslationContext context) {
      var newLeft = Analyze(expression.Left, context);
      var newRight = Analyze(expression.Right, context);
      var bitAsInt = _dbModel.Driver.Supports(DbFeatures.TreatBitAsInt);
      // We do need extra conversion
      if(bitAsInt) {
        if(!ReturnsBit(newLeft))
          newLeft = CreateSqlFunction(SqlFunctionType.ConvertBoolToBit, newLeft);
        if(!ReturnsBit(newRight))
          newRight = CreateSqlFunction(SqlFunctionType.ConvertBoolToBit, newRight);
      }
      return Expression.MakeBinary(expression.NodeType, newLeft, newRight);
    }

    protected virtual Expression AnalyzeEqualNonBoolOperator(BinaryExpression expression, TranslationContext context) {
      var expr = CheckEqualOperator(expression, context);
      return AnalyzeOperands(expr, context);
    }

    protected virtual Expression AnalyzeBinaryOperator(BinaryExpression expression, TranslationContext context) {
      // Detect if it is a logical expression - if yes, check for bit column argument
      var isLogicOp = expression.Left.Type == typeof(bool);
      // process arguments
      var ops = expression.GetOperands();
      var newOps = ops.Select(op => CheckBoolBitExpression(Analyze(op, context))).ToList();
      //special case - string + string; this used to be in PrequelAnalyzer
      if(expression.NodeType == ExpressionType.Add && expression.Left.Type == typeof(string))
        return CreateConcat(newOps[0], newOps[1]);
      //general case
      var newExpr = expression.ChangeOperands(newOps, ops);
      // Check bitwise op
      var result = CheckBitwiseOp(newExpr);
      return result;
    }

    private SqlFunctionExpression CreateConcat(Expression arg1, Expression arg2) {
      // optimization for multiple strings concatenated
      var c1 = arg1 as SqlFunctionExpression;
      if(c1 != null && c1.FunctionType == SqlFunctionType.Concat) {
        c1.Operands.Add(arg2);
        return c1;
      }
      return CreateSqlFunction(SqlFunctionType.Concat, arg1, arg2);
    }

    private Expression CheckBitwiseOp(BinaryExpression expression) {
      if(!expression.Type.IsInt())
        return expression;
      switch(expression.NodeType) {
        case ExpressionType.And:
        case ExpressionType.Or:
        case ExpressionType.ExclusiveOr:
          return CreateSqlFunction(expression.NodeType.ToBitwise(), expression.Left, expression.Right);
      }//switch
      return expression;
    }

    protected virtual Expression AnalyzeMemberInit(MemberInitExpression expression, TranslationContext context) {
      var newExpr = AnalyzeNewOperator(expression.NewExpression, context);
      var newBindings = new List<MemberBinding>();
      foreach(var b in expression.Bindings) {
        var newBnd = AnalyzeMemberBinding(b, context);
        newBindings.Add(newBnd);
      }
      var typedNewExpr = newExpr as NewExpression;
      if(typedNewExpr == null)
        return newExpr;
      return Expression.MemberInit(typedNewExpr, newBindings);
    }

    protected virtual MemberBinding AnalyzeMemberBinding(MemberBinding binding, TranslationContext context) {
      switch(binding.BindingType) {
        case MemberBindingType.Assignment:
          var asmt = (MemberAssignment)binding;
          var opExpr = Analyze(asmt.Expression, context);
          var sqlExpr = opExpr as SqlExpression;
          if(sqlExpr != null)
            sqlExpr.Alias = asmt.Member.Name;
          return ExpressionMaker.MakeMemberAssignment(binding.Member, opExpr);

        case MemberBindingType.ListBinding:
          var listBnd = (MemberListBinding)binding;
          var newInits = new List<ElementInit>();
          foreach(var init in listBnd.Initializers) {
            var newArgs = new List<Expression>();
            foreach(var arg in init.Arguments) {
              var newArg = Analyze(arg, context);
              newArgs.Add(newArg);
            }
            var newInit = Expression.ElementInit(init.AddMethod, newArgs);
            newInits.Add(newInit);
          }
          return Expression.ListBind(listBnd.Member, newInits);

        case MemberBindingType.MemberBinding:
          var mmBnd = (MemberMemberBinding)binding;
          var newBnds = new List<MemberBinding>();
          foreach(var bnd in mmBnd.Bindings) {
            var newBnd = AnalyzeMemberBinding(bnd, context);
            newBnds.Add(newBnd);
          }
          return Expression.MemberBind(mmBnd.Member, newBnds);

        default:
          return null; //never happens
      }//switch
    }//method


    protected virtual Expression AnalyzeNewOperator(NewExpression expression, TranslationContext context) {
      // Special case for expressions like 'new DateTime(1999, 1, 2)'
      object value;
      if(LinqExpressionHelper.IsNewConstant(expression, out value))
        return Expression.Constant(value);
      if(expression.Arguments.Count == 0)
        return expression;

      var newValues = new List<SqlExpression>();
      for(int i = 0; i < expression.Arguments.Count; i++) {
        var newArg = Analyze(expression.Arguments[i], context);
        //Force aliases from to output columns/expressions from member names
        var sqlExpr = newArg as SqlExpression;
        if(sqlExpr == null)
          sqlExpr = new AliasedExpression(newArg, null);
        if(expression.Members != null)
          sqlExpr.Alias = expression.Members[i].Name;
        newValues.Add(sqlExpr);
      }
      var derivedTableExpression = new DerivedTableExpression(expression, newValues);
      return derivedTableExpression;
    }

    protected virtual Expression AnalyzeSelectMany(IList<Expression> parameters, TranslationContext context) {
      var tableExpression = parameters[0];
      var projectionExpression = Analyze(parameters[1], new[] { tableExpression }, context);
      switch(parameters.Count) {
        case 2:
          return projectionExpression;
        case 3:
          var derivedTableContext = new TranslationContext(context);
          //derivedTableDefinitionBuilderContext.ExpectDerivedTableDefinition = true;
          var expression = Analyze(parameters[2], new[] { tableExpression, projectionExpression },
                                    derivedTableContext);
          return expression;
      }
      return null; //never happens
    }



    //protected virtual IDictionary<MemberInfo, E> GetTypeInitializers<E>(NewExpression newExpression)
    //    where E : Expression
    //{
    //    Type metaType;
    //    return GetTypeInitializers<E>(newExpression, out metaType);
    //}

    /// <summary>
    /// Analyzes a Join statement (explicit join)
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeJoin(IList<Expression> parameters, TranslationContext context) {
      return AnalyzeJoin(parameters, TableJoinType.Inner, context);
    }

    /// <summary>
    /// Analyzes a Join statement (explicit join)
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeGroupJoin(IList<Expression> parameters, TranslationContext context) {
      return AnalyzeJoin(parameters, TableJoinType.Inner, context);
    }

    protected virtual Expression AnalyzeOuterJoin(IList<Expression> parameters, TranslationContext context) {
      var expression = Analyze(parameters[0], context);
      var tableExpression = expression as TableExpression;
      if(tableExpression != null) {
        tableExpression.SetOuterJoin();
      }
      return expression;
    }

    private Expression AnalyzeJoin(IList<Expression> parameters, TableJoinType joinType, TranslationContext context) {
      if(parameters.Count == 5) {
        var outerExpr = Analyze(parameters[0], context);
        var innerExpr = Analyze(parameters[1], context);
        
         // 2019/4 - enabling joins with subqueries
        var innerTable = innerExpr as TableExpression;
        // TODO: fix this. When joined table is a subquery, we have this exception
        if(innerTable == null)
          Util.Throw("Join with sub-query is not supported. Sub-query: {0}.", innerExpr);
     
        // RI: check if key selectors return Entity: if yes, change to PK
        var outerSel = CheckJoinKeySelector(parameters[2], context);
        var innerSel = CheckJoinKeySelector(parameters[3], context);
        var outerKeySelector = Analyze(outerSel, outerExpr, context);
        var innerKeySelector = Analyze(innerSel, innerExpr, context);
        // from here, we have two options to join:
        // 1. left and right are tables, we can use generic expressions (most common)
        // 2. left is something else (a meta table)
        var outerTable = outerExpr as TableExpression;
        if(outerTable == null) {
          var outerKeyColumn = outerKeySelector as ColumnExpression;
          if(outerKeyColumn == null)
            Util.Throw("S0701: No way to find left table for Join");
          outerTable = outerKeyColumn.Table;
        } 
        var joinExpr = ExpressionMaker.MakeBinary(ExpressionType.Equal, outerKeySelector, innerKeySelector);
        innerTable.Join(joinType, outerTable, joinExpr,
                        string.Format("join{0}", context.EnumerateAllTables().Count()));
        // last part is lambda, with two tables as parameters
        var derivedTableDefinitionBuilderContext = new TranslationContext(context);
        //derivedTableDefinitionBuilderContext.ExpectDerivedTableDefinition = true;
        var expression = Analyze(parameters[4], new[] { outerExpr, innerTable }, derivedTableDefinitionBuilderContext);
        return expression;
      }
      Util.Throw("S0530: Don't know how to handle GroupJoin() with {0} parameters", parameters.Count);
      return null;
    }

    /// <summary>
    /// "Distinct" means select X group by X
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeDistinct(IList<Expression> parameters, TranslationContext context) {
      var expression = Analyze(parameters[0], context);
      // we select and group by the same criterion
      // RI: adding explicit list of columns, to catch situation of duplicate column names in joins
      var childColumns = GetChildColumns(expression, context);
      // some providers (ex SQL CE) do not allow Text columns in distinct output
      CheckDistinctClauseColumns(childColumns, context);

      var group = new GroupExpression(expression, expression, childColumns);
      context.CurrentSelect.Group.Add(group);

      //RI: added this special check
      // var table  
      // var cols = RegisterAllColumns()

      // "Distinct" method is equivalent to a GroupBy
      // but for some obscure reasons, Linq expects a IQueryable instead of an IGrouping
      // so we return the column, not the group
      return expression;
    }

    // just in case there are specific restrictions for particular server
    private void CheckDistinctClauseColumns(IList<ColumnExpression> columns, TranslationContext context) {
    }

    //RI: adding this to get all child columns in GroupExpression
    private IList<ColumnExpression> GetChildColumns(Expression expression, TranslationContext context) {
      var columns = new List<ColumnExpression>();
      // extract columns (for SQL build)
      expression.Recurse(
          delegate (Expression e) {
            if(e is ColumnExpression)
              columns.Add((ColumnExpression)e);
            else if(e is TableExpression) {
              var t = (TableExpression)e;
              columns.AddRange(RegisterAllColumns(t, context));
            }
            return e;
          }//
      );
      return columns;
    }

    protected virtual Expression AnalyzeGroupBy(MethodInfo method, IList<Expression> parameters, TranslationContext context) {
      // there are 8 overloads of Queryable.GroupBy method. 4 of them have last parameter IEqualityComparer - reject these overloads
      // The remaining 4 have Expression<> as last parameter - we use it to detect these.
      var methodParams = method.GetParameters();
      var lastParamType = methodParams[methodParams.Length - 1].ParameterType;
      if(methodParams.Length > 2 && !lastParamType.IsSubclassOf(typeof(LambdaExpression)))
        throw Util.Throw("The overload of GroupBy method with IEqualityComparer parameter is not supported.");
      /* The remaining overloads are the following:
   (#1)   public static IQueryable<IGrouping<TKey, TSource>> GroupBy<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector);
                      -- 2 args, 2 type args
   (#2)   public static IQueryable<IGrouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, 
                                                                                               Expression<Func<TSource, TElement>> elementSelector);
                      -- 3 args, 3 type args
   (#3)   public static IQueryable<TResult> GroupBy<TSource, TKey, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, 
                                                                            Expression<Func<TKey, IEnumerable<TSource>, TResult>> resultSelector);
                      -- 3 args, 3 type args
   (#4)   public static IQueryable<TResult> GroupBy<TSource, TKey, TElement, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, 
                                            Expression<Func<TSource, TElement>> elementSelector, Expression<Func<TKey, IEnumerable<TElement>, TResult>> resultSelector);
                      -- 4 args, 4 type args
        We can distinguish them by number of args; for #2 and #3 - both have 3 args and 3 type args. We distinguish them by analyzing type argument
        */
      // Detect which overload we have
      int ovldNum = 0;
      switch(methodParams.Length) {
        case 2:
          ovldNum = 1;
          break;
        case 4:
          ovldNum = 4;
          break;
        case 3:
          //analyze type argument of the last parameter (generic expression)
          var funcType = lastParamType.GenericTypeArguments[0];
          //this is a Func<..>, check it count of arguments
          var funcGenArgCount = funcType.GenericTypeArguments.Length;
          switch(funcGenArgCount) {
            case 2:
              ovldNum = 2;
              break;
            case 3:
              ovldNum = 3;
              break;
          }
          break;
      }
      if(ovldNum == 0)
        throw Util.Throw("Unknown/unsupported overload of GroupBy method - failed to detect overload type.");
      //analyze parameters
      var table = Analyze(parameters[0], context);
      var keySelector = Analyze(parameters[1], table, context);
      Expression result = null;
      bool clrGrouping = false;
      switch(ovldNum) {
        case 1: //2 params          
          result = table; // we return the whole table
          clrGrouping = true;
          break;
        case 2: //3 params
          result = Analyze(parameters[2], new Expression[] { table }, context);
          clrGrouping = true;
          break;
        case 3: // 3 params
          result = Analyze(parameters[2], new Expression[] { keySelector, table }, context);
          break;
        case 4: // 4 params
          Util.Throw("This overload of GroupBy method is not supported. Rewrite the LINQ query.");
          break;
      }
      var childColumns = GetChildColumns(keySelector, context);
      var group = new GroupExpression(result, keySelector, childColumns, clrGrouping);
      context.CurrentSelect.Group.Add(group);
      return group;
    }

    //Just return analyzed operand. AsQueryable might be injected by code handling special cases, to satisfy type constraints
    protected virtual Expression AnalyzeAsQueryable(IList<Expression> parameters, TranslationContext context) {
      return Analyze(parameters[0], context);
    }


    protected virtual Expression AnalyzeAll(IList<Expression> parameters, TranslationContext context) {
      // Convert to !Exist(!cond)
      var allBuilderContext = context.NewSelect();
      var tableExpression = Analyze(parameters[0], allBuilderContext);
      var allClause = Analyze(parameters[1], tableExpression, allBuilderContext);
      var whereExpression = Expression.Not(allClause);
      RegisterWhere(whereExpression, allBuilderContext);
      var currSelect = allBuilderContext.CurrentSelect;
      var existsExpr = new SqlFunctionExpression(SqlFunctionType.Exists, typeof(bool), currSelect);
      var notExistsExpr = Expression.Not(existsExpr);
      return notExistsExpr;
    }

    /// <summary>
    /// Any() returns true if the given condition satisfies at least one of provided elements
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeAny(IList<Expression> parameters, TranslationContext context) {
      // RI: changing cond to NOT
      // TODO: fix this!
      //  if(context.IsExternalInExpressionChain) {
      if(context.IsExternalInExpressionChain) {
        var tableExpression = Analyze(parameters[0], context);
        Expression projectionOperand;

        // basically, we have three options for projection methods:
        // - projection on grouped table (1 operand, a GroupExpression)
        // - projection on grouped column (2 operands, GroupExpression and ColumnExpression)
        // - projection on table/column, with optional restriction
        var groupOperand0 = tableExpression as GroupExpression;
        if(groupOperand0 != null) {
          if(parameters.Count > 1) {
            projectionOperand = Analyze(parameters[1], groupOperand0.GroupedExpression, context);
          } else
            projectionOperand = Analyze(groupOperand0.GroupedExpression, context);
        } else {
          projectionOperand = tableExpression;
          CheckWhere(projectionOperand, parameters, 1, context);
        }

        if(projectionOperand is TableExpression)
          projectionOperand = RegisterTable((TableExpression)projectionOperand, context);

        if(groupOperand0 != null) {
          var childColumns = GetChildColumns(groupOperand0.KeyExpression, context);
          projectionOperand = new GroupExpression(projectionOperand, groupOperand0.KeyExpression, childColumns);
        }

        var res = CreateSqlFunction(SqlFunctionType.Exists, context.CurrentSelect);// projectionOperand);
        context.NewParentSelect();
        return res; 
      } else {
        //*/
        var anyExprContext = context.NewSelect();
      var tableExpression = Analyze(parameters[0], anyExprContext);

      if(!(tableExpression is TableExpression))
        tableExpression = Analyze(tableExpression, anyExprContext);

      // from here we build a custom clause: EXists
      if(parameters.Count > 1) {
        var anyClause = Analyze(parameters[1], tableExpression, anyExprContext);
        RegisterWhere(anyClause, anyExprContext);
      } else {
        //TODO: fix this
        //Util.Throw("Queryable.Any() method without parameter is not supported.");
      }
        var currSelect = anyExprContext.CurrentSelect;
        var existsExpr = CreateSqlFunction(SqlFunctionType.Exists, currSelect);
        anyExprContext.CurrentSelect = currSelect.ChangeOperands(new Expression[] { existsExpr }, currSelect.Operands);
        return existsExpr;
      }
    }

    protected virtual Expression AnalyzeLikeStart(IList<Expression> parameters, TranslationContext context) {
      return AnalyzeLike(parameters[0], null, parameters[1], "%", context);
    }

    protected virtual Expression AnalyzeLikeEnd(IList<Expression> parameters, TranslationContext context) {
      return AnalyzeLike(parameters[0], "%", parameters[1], null, context);
    }

    protected virtual Expression AnalyzeLike(IList<Expression> parameters, TranslationContext context) {
      return AnalyzeLike(parameters[0], "%", parameters[1], "%", context);
    }

    protected virtual Expression AnalyzeLike(Expression expr, string before, Expression operand, string after, TranslationContext context) {
      var forceIgnoreCase = context.Command.Options.IsSet(QueryOptions.ForceIgnoreCase);
      //The main goal is to provide automatic escaping of pattern (of wildcard characters)
      var newExpr = Analyze(expr, context);
      var sqlDialect = _dbModel.Driver.SqlDialect;
      var escapeChar = sqlDialect.LikeEscapeChar;
      var wildcards = sqlDialect.LikeWildCardChars;
      //Special case - constant 
      if(operand.NodeType == ExpressionType.Constant) {
        var opValue = ((ConstantExpression)operand).Value as string;
        var escapedOp = Expression.Constant(before + LinqExpressionHelper.EscapeLikePattern(opValue, escapeChar, wildcards) + after);
        return CreateSqlFunction(SqlFunctionType.Like, forceIgnoreCase, newExpr, escapedOp);
      }
      var newOp = Analyze(operand, context);
      if(newOp is ExternalValueExpression) {
        var newOpExt = newOp as ExternalValueExpression;
        var escapedNewOp = LinqExpressionHelper.CallEscapeLikePattern(newOpExt.SourceExpression, escapeChar, wildcards, before, after);
        var newParam = DeriveInputParameter(newOpExt, escapedNewOp, context);
        return CreateSqlFunction(SqlFunctionType.Like, forceIgnoreCase, newExpr, newParam);
      }
      //General case, when both args of LIKE are database-stored values (ex: two columns)
      if(before != null)
        newOp = CreateSqlFunction(SqlFunctionType.Concat, Expression.Constant(before), newOp);
      if(after != null)
        newOp = CreateSqlFunction(SqlFunctionType.Concat, newOp, Expression.Constant(after));
      return CreateSqlFunction(SqlFunctionType.Like, forceIgnoreCase, newExpr, newOp);
    }

    protected virtual Expression AnalyzeSubString(IList<Expression> parameters, TranslationContext context) {
      var stringExpression = Analyze(parameters[0], context);
      var startExpression = Analyze(parameters[1], context);
      if(parameters.Count > 2) {
        var lengthExpression = parameters[2];
        return CreateSqlFunction(SqlFunctionType.Substring, stringExpression, startExpression, lengthExpression);
      }
      return CreateSqlFunction(SqlFunctionType.Substring, stringExpression, startExpression);
    }

    private Expression ConvertArrayToConstant(ExternalValueExpression parameter, TranslationContext context) {
      object value = null;
      try {
        //value = parameter.GetValue(null);
      } catch(Exception ex) {
        var msg = "Failed to translate 'Contains' call: supported only for arrays or lists that can be converted to constant list. Inner error: " + ex.Message;
        throw new Exception(msg, ex);
      }
      // do not unregister - it will break parameter sequence
      //UnregisterParameter(parameter, context);
      return Expression.Constant(value);
    }

    protected virtual Expression AnalyzeContains(IList<Expression> parameters, TranslationContext context) {
      if(!parameters[1].Type.IsDbPrimitive())
        parameters = ConvertContainsWithObject(parameters, context);

      var t0 = parameters[0].Type;
      if(t0.IsGenericQueryable()) {
        Expression p1 = Analyze(parameters[1], context);
        var newContext = context.NewSelect();
        Expression p0 = AnalyzeSubQuery(parameters[0], newContext);
        var c = p0 as ColumnExpression;
        if(c != null && !newContext.CurrentSelect.Tables.Contains(c.Table))
          newContext.CurrentSelect.Tables.Add(c.Table);
        return CreateSqlFunction(SqlFunctionType.In, p1, newContext.CurrentSelect.Mutate(new Expression[] { p0 }));
      } else if(t0.IsListOrArray()) {
        Expression array = Analyze(parameters[0], context);
        var expression = Analyze(parameters[1], context);
        return CreateSqlFunction(SqlFunctionType.InArray, expression, array);
      }
      throw Util.Throw("{0}.Contains() method is not supported.", t0.GetDisplayName());
    }

    protected virtual Expression AnalyzeToUpper(IList<Expression> parameters, TranslationContext context) {
      return CreateSqlFunction(SqlFunctionType.ToUpper, Analyze(parameters[0], context));
    }

    protected virtual Expression AnalyzeToLower(IList<Expression> parameters, TranslationContext context) {
      return CreateSqlFunction(SqlFunctionType.ToLower, Analyze(parameters[0], context));
    }

    /// <summary>
    /// Registers ordering request
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="descending"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeOrderBy(IList<Expression> parameters, bool descending, TranslationContext context) {
      var table = Analyze(parameters[0], context);
      // the column is related to table
      var column = Analyze(parameters[1], table, context);
      context.CurrentSelect.OrderBy.Add(new OrderByExpression(descending, column));
      return table;
    }

    /// <summary>
    /// Analyzes constant expression value, and eventually extracts a table
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Expression AnalyzeConstant(ConstantExpression expression, TranslationContext context) {
      if(expression != null && expression.Type.IsGenericType) {
        var queriedType = expression.Type.GetGenericArguments()[0];
        // check if it is EntityQuery 
        var entQuery = expression.Value as EntityQuery;
        if(entQuery != null) {
          // check if it is entity set
          if(entQuery.IsEntitySet) {
            var iLock = entQuery as ILockTarget;
            var lockType = iLock == null ? LockType.None : iLock.LockType;
            var tableType = expression.Type.GenericTypeArguments[0];
            var tblExpr = CreateTable(tableType, context, lockType);
            context.CurrentSelect.Tables.Add(tblExpr);
            return tblExpr;
          } else {
            var newCtx = context; //.NewSelect();
            return AnalyzeSubQuery(entQuery.Expression, newCtx);
          }
        }//if entQuery != null
      } //if expression != null
      return expression;
    }

    // RI: my new stuff
    protected virtual Expression AnalyzeSubQuery(Expression expression, TranslationContext context) {
      var exprChain = ExpressionChain.Build(expression);
      var tableExpression = ExtractFirstTable(exprChain[0], context);
      return this.Analyze(exprChain, tableExpression, context);
    }

    protected virtual Expression AnalyzeInvoke(InvocationExpression expression, IList<Expression> parameters,
                                               TranslationContext context) {
      var lambda = expression.Expression as LambdaExpression;
      if(lambda == null)
        Util.Throw("The invocation/call not supported: {0}", expression.Expression);
      var localBuilderContext = context.NewQuote();
      return Analyze(lambda, expression.Arguments, localBuilderContext);
    }

    protected virtual Expression AnalyzeUnionIntersect(MultiSetType multiSetType, IList<Expression> parameters, TranslationContext context) {
      Expression firstSet = Analyze(parameters[0], context);
      // BuildSelectResultReaderAndCutOutSql(firstSet, context, firstSet.Type);

      var otherQuery = parameters[1];
      // Handle second select first
      var newContext = context.NewSisterSelect();
      var tableExpression = AnalyzeSubQuery(otherQuery, newContext);
      BuildSelectResultReaderAndCutOutSql(tableExpression, newContext, tableExpression.Type);
      // add the second select select to the chain
      if (newContext.CurrentSelect.ChainedSet != null) {
        var typedTable = tableExpression as TableExpression;
        var dbTable = typedTable == null ? null : typedTable.TableInfo;
        var operand0 = new SubSelectExpression(newContext.CurrentSelect, tableExpression.Type, "source", dbTable);
        newContext.NewParentSelect();
        newContext.CurrentSelect.Tables.Add(operand0);
      }
      // attach it to main select
      var selectToModify = context.CurrentSelect;
      while (selectToModify.ChainedSet != null)
        selectToModify = selectToModify.ChainedSet.ChainedSelect;

      selectToModify.ChainedSet = new MultiSetChainLink() { ChainedSelect = newContext.CurrentSelect, MultiSetType = multiSetType };

      return firstSet;
    }

    protected bool IsCustomSqlSnippet(MethodInfo method, out CustomSqlSnippet snippet) {
      snippet = null;
      if (!method.IsStatic)
        return false;
      if (_dbModel.CustomSqlSnippets.TryGetValue(method, out snippet))
        return true;
      return false; 
    }
    protected CustomSqlExpression AnalyzeCustomSqlSnippet(CustomSqlSnippet sqlSnippet,
                                            IList<Expression> parameters, TranslationContext context) {
      var visitedParams = parameters.Select(p => Analyze(p, context)).ToArray();
      return new CustomSqlExpression(sqlSnippet, visitedParams);
    }


  }
}
