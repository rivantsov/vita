
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Common;
using Vita.Entities.Runtime;
using Vita.Entities.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;

namespace Vita.Data.Linq.Translation {

    internal partial class ExpressionTranslator {
        private DbModel _dbModel; 

        public ExpressionTranslator(DbModel dbModel) {
          _dbModel = dbModel;
        }

        public virtual void BuildSelect(Expression selectExpression, TranslationContext context, Type expectedResultType = null) {
            // collect columns, split Expression in
            // - things we will do in CLR
            // - things we will do in SQL
            expectedResultType = expectedResultType ?? selectExpression.Type; 
            LambdaExpression recordReaderExpr;
            var dataRecordParameter = Expression.Parameter(typeof(IDataRecord), "dataRecord");
            var sessionParameter = Expression.Parameter(typeof(EntitySession), "session");
            // if we have a GroupByExpression, the result type is not the same:
            // - we need to read what is going to be the Key expression
            // - the final row generator builds a IGrouping<K,T> instead of T
            var selectGroupExpression = selectExpression as GroupExpression;
            if (selectGroupExpression != null) {
              var sqlOutExpr = CutOutSqlTierLambda(selectGroupExpression.GroupedExpression, dataRecordParameter, sessionParameter, null, context);
              var selectKeyExpr = CutOutSqlTierLambda(selectGroupExpression.KeyExpression, dataRecordParameter, sessionParameter, null, context);
              recordReaderExpr = sqlOutExpr;
              if (selectGroupExpression.UseClrGrouping) {
                recordReaderExpr = BuildGroupByPairsReader(sqlOutExpr, selectKeyExpr, dataRecordParameter, sessionParameter, context);
                var grouper = QueryResultsProcessor.CreateGroupBy(selectKeyExpr.Body.Type, sqlOutExpr.Body.Type);
                context.CurrentSelect.ResultsProcessor = grouper;
                context.CurrentSelect.Group.Remove(selectGroupExpression);
              }
            }
            else
              recordReaderExpr = CutOutSqlTierLambda(selectExpression, dataRecordParameter, sessionParameter, expectedResultType, context);
            context.CurrentSelect.Reader = recordReaderExpr;
        }


        /// <summary>
        /// Builds reader GroupBy producing intermediate key/value pairs; actual grouping will be performed in CLR layer.
        /// </summary>
        protected virtual LambdaExpression BuildGroupByPairsReader(LambdaExpression select, LambdaExpression selectKey,
                                                            ParameterExpression dataRecordParameter, ParameterExpression sessionParameter,
                                                            TranslationContext context)
        {
            var kType = selectKey.Body.Type;
            var lType = select.Body.Type;
            var groupingType = typeof(TransientKeyValuePair<,>).MakeGenericType(kType, lType);
            var groupingCtor = groupingType.GetConstructor(new[] { kType, lType });
            var invokeSelectKey = Expression.Invoke(selectKey, dataRecordParameter, sessionParameter);
            var invokeSelect = Expression.Invoke(select, dataRecordParameter, sessionParameter);
            var newLineGrouping = Expression.New(groupingCtor, invokeSelectKey, invokeSelect);
/*
            var iGroupingType = typeof(IGrouping<,>).MakeGenericType(kType, lType);
            var newIGrouping = Expression.Convert(newLineGrouping, iGroupingType);
 */ 
            var lambda = Expression.Lambda(newLineGrouping, dataRecordParameter, sessionParameter);
            return lambda;
        }

        protected virtual LambdaExpression CutOutSqlTierLambda(Expression selectExpression,
                                                         ParameterExpression dataRecordParameter, ParameterExpression sessionParameter, 
                                                         Type expectedResultType,
                                                         TranslationContext context)
        {
            var expression = CutOutSqlTier(selectExpression, dataRecordParameter, sessionParameter, expectedResultType, context);
            return Expression.Lambda(expression, dataRecordParameter, sessionParameter);
        }

        protected virtual Expression CutOutSqlTier(Expression expression,
                                                    ParameterExpression dataRecordParameter, ParameterExpression sessionParameter,
                                                    Type expectedType,
                                                    TranslationContext context)
        {
            expectedType = expectedType ?? expression.Type; 
            // two options: we cut and return
            if (IsSqlTier(expression, context))
            {
                // "cutting out" means we replace the current expression by a SQL result reader
                // before cutting out, we check that we're not cutting a table in this case, we convert it into its declared columns
                if (expression is TableExpression)
                  // RI: entity reader comes here
                  return GetOutputTableReader((TableExpression)expression, dataRecordParameter,
                                                sessionParameter, context);
                // RI: single-value result goes here
                return GetOutputValueReader(expression, expectedType, dataRecordParameter, sessionParameter, context);
            }
            // RI: Anon types, custom types go here
            var newOperands = new List<Expression>();
            var operands = expression.GetOperands();
            var argTypes = expression.GetArgumentTypes();
            for(int i = 0; i < operands.Count; i++ ) {
              var op = operands[i];
              var newOp = op == null ? null : CutOutSqlTier(op, dataRecordParameter, sessionParameter, argTypes[i], context);
              newOperands.Add(newOp);
            }
            Expression newExpr;
            if (expression is MetaTableExpression)
              //Joins go here
              newExpr = ((MetaTableExpression)expression).ConvertToNew(newOperands);
            else 
              newExpr =  expression.ChangeOperands(newOperands, operands);
            return newExpr; 
        }

        /// <summary>
        /// Returns true if we must cut out the given Expression
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private bool IsSqlTier(Expression expression, TranslationContext context)
        {
          //RI: moved this to LinqSqlProvider, to allow different implementations for servers
          var isSql = _dbModel.LinqSqlProvider.IsSqlTier(expression, context.Command.Kind);
          return isSql; 
/*
            //RI: changed this (simplified?), see DbLinq code for old version
            var tier = ExpressionUtil.GetTier(expression);
            // RI: changed order, choose SQL by default if both are available
            // this is especially important for Views!
            if((tier & ExpressionTier.Sql) != 0) 
              return true;
            if((tier & ExpressionTier.Clr) != 0)
              return false;
            return false; 
 */ 
        }

        /// <summary>
        /// Checks any expression for a TableExpression, and eventually replaces it with the convenient columns selection
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual Expression CheckTableExpression(Expression expression, TranslationContext context)
        {
            if (expression is TableExpression)
                return GetSelectTableExpression((TableExpression)expression, context);
            return expression;
        }

        /// <summary>
        /// Replaces a table selection by a selection of all mapped columns (ColumnExpressions).
        /// ColumnExpressions will be replaced at a later time by the tier splitter
        /// </summary>
        /// <param name="tableExpression"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual Expression GetSelectTableExpression(TableExpression tableExpression, TranslationContext context)
        {
            var bindings = new List<MemberBinding>();
            foreach (var columnExpression in RegisterAllColumns(tableExpression, context))
            {
                var binding = Expression.Bind((MethodInfo) columnExpression.ColumnInfo.Member.ClrClassMemberInfo, columnExpression);
                bindings.Add(binding);
            }
            var newExpression = Expression.New(tableExpression.Type);
            return Expression.MemberInit(newExpression, bindings);
        }

        /// <summary>
        /// Returns the parameter name, if the Expression is a ParameterExpression, null otherwise
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual string GetParameterName(Expression expression)
        {
            if (expression is ParameterExpression)
                return ((ParameterExpression)expression).Name;
            return null;
        }

        /// <summary>
        /// Merges a parameter and a parameter list
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public virtual IList<Expression> MergeParameters(Expression p1, IEnumerable<Expression> p2)
        {
            var p = new List<Expression>();
            p.Add(p1);
            p.AddRange(p2);
            return p;
        }
    }
}
