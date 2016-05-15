
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Vita.Data.Linq.Translation.Expressions;

using Vita.Common;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Entities.Runtime;
using Vita.Data.Linq.Translation.SqlGen;
using System.Reflection;
using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Entities.Model;

namespace Vita.Data.Linq.Translation {


    /// <summary>
    /// Full query builder, with cache management
    /// 1. Parses Linq Expression
    /// 2. Generates SQL
    /// </summary>
    internal partial class LinqEngine  {
        private DbModel _dbModel; 

        ExpressionTranslator _translator;
        ExpressionOptimizer _optimizer;
        SpecialExpressionTranslator _specialExpressionTranslator;

        public LinqEngine(DbModel dbModel)  {
          _dbModel = dbModel; 
          _translator = new ExpressionTranslator(dbModel); 
          _optimizer = new ExpressionOptimizer(); 
          _specialExpressionTranslator = new SpecialExpressionTranslator(); 
        }

        public TranslatedLinqCommand Translate(LinqCommand command) {
          if (command.Info == null)
            LinqCommandAnalyzer.Analyze(_dbModel.EntityApp.Model, command); 
          try {
            switch(command.CommandType) {
              case LinqCommandType.Select: 
                return TranslateSelect(command);
              case LinqCommandType.Update:
              case LinqCommandType.Delete:
              case LinqCommandType.Insert:
                return TranslateNonQuery(command);
              default:
                ThrowTranslationFailed(command, "Unsupported LINQ command type.");
                return null; 

            }
          } catch(LinqTranslationException) {
            throw; // if it is alread Linq translation exception, pass it up.
          } catch (Exception ex) {
            var message = "Linq to SQL translation failed: " + ex.Message + 
                       "\r\nPossibly facilities you are trying to use are not supported. " +
                       "\r\nTry to reformulate/simplify the query. Hint: do not use c# functions/methods inside query directly. ";
            throw new LinqTranslationException(message, command, ex);
          }
        }

        private TranslatedLinqCommand TranslateSelect(LinqCommand command) {
          LinqCommandPreprocessor.PreprocessCommand(_dbModel.EntityApp.Model, command);
          var context = new TranslationContext(_dbModel, command);
          var cmdInfo = command.Info;
          // convert lambda params into an initial set of ExternalValueExpression objects; 
          foreach (var prm in cmdInfo.Lambda.Parameters) {
            var inpParam = new ExternalValueExpression(prm);
            context.ExternalValues.Add(inpParam);
          }
          //Analyze/transform query expression
          var exprChain = ExpressionChain.Build(cmdInfo.Lambda.Body);
          var selectExpr = BuildSelectExpression(exprChain, context); 
          // Analyze external values (parameters?), create DbParameters
          var commandParams = BuildParameters(command, context);
          // If there's at least one parameter that must be converted to literal (ex: value list), we cannot cache the query
          bool canCache = !context.ExternalValues.Any(v => v.SqlUse == ExternalValueSqlUse.Literal);
          if (!canCache)
            command.Info.Flags |= LinqCommandFlags.NoQueryCache;
          //Build SQL, compile object materializer
          var sqlBuilder = new SqlBuilder(_dbModel);
          var sqlStatement = sqlBuilder.BuildSelect(selectExpr);
          // Parameters are represented as {2}, {3}, etc. 
          // Braces in string literals are escaped and are represented as '{0}' and '{1}'  
          var sqlTemplate = sqlStatement.ToString();
          var sql = FormatSql(sqlTemplate, commandParams);
          var objMaterializer = CompileObjectMaterializer(context);
          var outType = context.CurrentSelect.Reader.Body.Type;
          var resultListCreator = ReflectionHelper.GetCompiledGenericListCreator(outType);
          //check if we need to create implicit result set processor
          if (selectExpr.ResultsProcessor == null) {
            var returnsResultSet = typeof(IQueryable).IsAssignableFrom(cmdInfo.Lambda.Body.Type);
            if (!returnsResultSet)
              selectExpr.ResultsProcessor = QueryResultsProcessor.CreateFirstSingleLast("First", outType);
          }
          var sqlQuery = new TranslatedLinqCommand(sqlTemplate, sql, commandParams, command.Info.Flags,
                  objMaterializer, selectExpr.ResultsProcessor, resultListCreator);
          return sqlQuery;
        }

        // analyzes external values and creates db parameters
        private List<LinqCommandParameter> BuildParameters(LinqCommand command, TranslationContext context) {
          var sqlProvider = _dbModel.LinqSqlProvider;
          var parameters = new List<LinqCommandParameter>();
          foreach (var extValue in context.ExternalValues) {
            if (extValue.SqlUseCount == 0)
              extValue.SqlUse = ExternalValueSqlUse.NotUsed;
            else
              _dbModel.LinqSqlProvider.CheckQueryParameter(extValue);
            switch (extValue.SqlUse) {
              case ExternalValueSqlUse.NotUsed:
                continue; //next value
              case ExternalValueSqlUse.Literal:
                // We cannot use this value as a parameter (ex: list/array of values with Contains() method); 
                // in this case we transform it into literal value - it will be embedded into SQL directly. 
                // The query becomes non-cacheable (because of this embedded literal)
                var paramReadValue = BuildParameterValueReader(command.Info.Lambda.Parameters, extValue.SourceExpression);
                extValue.LiteralValue = paramReadValue(context.Command.ParameterValues);
                break;
              case ExternalValueSqlUse.Parameter:
                var valueReader = BuildParameterValueReader(command.Info.Lambda.Parameters, extValue.SourceExpression);
                var dbParamName = _dbModel.LinqSqlProvider.GetParameterName("P" + parameters.Count);
                extValue.LinqParameter = new LinqCommandParameter(dbParamName, parameters.Count, extValue.SourceExpression.Type, valueReader);
                parameters.Add(extValue.LinqParameter);
                break;
            }//switch
          }//foreach extValue
          return parameters; 
        }

        private void CheckExternalValue(ExternalValueExpression extValue) {
        }

        private Func<object[], object> BuildParameterValueReader(IList<ParameterExpression> queryParams, Expression valueSourceExpression) {
          // One trouble - for Binary object, we need to convert them to byte[]
          if (valueSourceExpression.Type == typeof(Binary)) {
            var methGetBytes = typeof(Binary).GetMethod("GetBytes");
            valueSourceExpression = Expression.Call(valueSourceExpression, methGetBytes);
          }
          //Quick path: most of the time the source expression is just a lambda parameter
          if (valueSourceExpression.NodeType == ExpressionType.Parameter) {
            var prmSource = (ParameterExpression) valueSourceExpression;
            var index = queryParams.IndexOf(prmSource);
            return (object[] prms) => prms[index];
          }
          // There is some computation in valueSourceExpression; use dynamic invoke to evaluate it.
          // DynamicInvoke is not efficient but this case is rare enough, so it is not worth more trouble
          var valueReadLambda = Expression.Lambda(valueSourceExpression, queryParams);
          var compiledValueRead = valueReadLambda.Compile();
          return (object[] prms) => compiledValueRead.DynamicInvoke(prms);
        }

        /// <summary>
        /// Builds and chains the provided Expressions
        /// </summary>
        /// <param name="expressionChain"></param>
        /// <param name="context"></param>
        protected virtual SelectExpression BuildSelectExpression(ExpressionChain expressionChain, TranslationContext context) {
          var tableExpr = _translator.ExtractFirstTable(expressionChain[0], context);
          BuildSelectExpression(expressionChain, tableExpr, context);
          BuildOffsetsAndLimits(context);
          // then prepare Parts for SQL translation
          CheckTablesAlias(context);
          CheckColumnNamesAliases(context);
          // now, we optimize anything we can
          OptimizeQuery(context);
          context.CurrentSelect.CommandInfo = context.Command.Info; //copy command info to final select
          // in the very end, we keep the SELECT clause
          return context.CurrentSelect;
        }

        protected virtual IList<string> GetAllAliases(TranslationContext context) {
            var aliases = new List<string>();
            aliases.AddRange(context.EnumerateAllTables().Select(t => t.Alias));
            aliases.AddRange(context.EnumerateScopeColumns().Select(c => c.Alias));
            return aliases;
        }

        protected virtual string GenerateTableAlias(IList<string> allAliases)
        {
          int index = 0;
          string alias;
          do {
            alias = "t" + index++; 
          } while (allAliases.Contains(alias));
          allAliases.Add(alias);
          return alias; 
        }

        /// <summary>
        /// Give all non-aliased tables a name
        /// </summary>
        /// <param name="context"></param>
        protected virtual void CheckTablesAlias(TranslationContext context)
        {
            var tables = context.EnumerateAllTables().Distinct().ToList();
            // just to be nice: if we have only one table involved, there's no need to alias it
            if (tables.Count == 1)
            {
              tables[0].Alias = null;
            } else {
              var allAliases = GetAllAliases(context); 
              foreach (var tableExpression in tables)
              {
                    // if no alias, or duplicate alias
                    if (string.IsNullOrEmpty(tableExpression.Alias) || allAliases.Count(s => s == tableExpression.Alias) > 1)
                      tableExpression.Alias = GenerateTableAlias(allAliases);
                }
            }
        }

        //RI: added this to fix some SQL errors when multiple columns in the output have the same name
        protected void CheckColumnNamesAliases(TranslationContext context) {
          foreach(var select in context.SelectExpressions) {
            CheckAliases(context, select.Operands);
            foreach(var grp in select.Group)
              CheckAliases(context, grp.Columns);

          }
        } //method

        protected void CheckAliases(TranslationContext context, IEnumerable<Expression> outExpressions) {
          var sqlExpressions = outExpressions.OfType<SqlExpression>().ToList();
          var allNames = new StringSet();
          foreach(var outExpr in sqlExpressions) {
            string outName = null; 
            var col = outExpr as ColumnExpression;
            if (col != null) {
              //if (isView) col.Alias = col.Name;
              outName = col.Alias ?? col.Name;
            }
            var needsAlias = outName != null && allNames.Contains(outName);
            if(outName != null) 
              allNames.Add(outName); 
            if(needsAlias)  
              outExpr.Alias = CreateDefaultAlias(outExpr, allNames);
          }//foreach outExpr
        } //method

        private string CreateDefaultAlias(SqlExpression expr, HashSet<string> allNames) {
          var column = expr as ColumnExpression; 
          var baseName = column == null ? "_col" : column.Table.TableInfo.TableName + column.ColumnInfo.ColumnName;
          var alias = baseName; 
          int index = 0;
          while(allNames.Contains(alias))
            alias = baseName + (index++);
          allNames.Add(alias); 
          return alias; 
        }

        /// <summary>
        /// Builds the ExpressionQuery main Expression, given a Table (or projection) expression
        /// </summary>
        /// <param name="expressions"></param>
        /// <param name="tableExpression"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        protected Expression BuildSelectExpression(ExpressionChain expressions, Expression tableExpression, TranslationContext context)
        {
            // Check expected type - it will be used for final result conversion if query returns a single value (like Count() query)
            var resultType = expressions[expressions.Count - 1].Type;
            if (resultType.IsGenericQueryable())
              resultType = null; 
            var selectExpression = _translator.Analyze(expressions, tableExpression, context);
            _translator.BuildSelect(selectExpression, context, resultType);
            return selectExpression;
        }

        /// <summary>
        /// This is a hint for SQL generations
        /// </summary>
        /// <param name="context"></param>
        protected virtual void BuildOffsetsAndLimits(TranslationContext context)
        {
            foreach (var selectExpression in context.SelectExpressions)
            {
              //RI: because of some problems with DISTINCT+TOP combination, we always force using Skip/Take combination (avoiding TOP)
              bool hasAny = selectExpression.Offset != null || selectExpression.Limit != null;
              if (!hasAny) continue;
              if (selectExpression.Offset == null)
                selectExpression.Offset = Expression.Constant(0);
              //RI: old code; check for offset !=null is redundant now
              if (selectExpression.Offset != null && selectExpression.Limit != null)
              {
                  selectExpression.OffsetAndLimit = Expression.Add(selectExpression.Offset, selectExpression.Limit);
              }
              //Check that we have OrderBy - it is required if we have offset/limit
              if (selectExpression.OrderBy.Count == 0 && _dbModel.Driver.Supports(Driver.DbFeatures.SkipTakeRequireOrderBy)) {
                // We do not have OrderBy but using Skip/Take requires it. 
                // We have to make it up. First check if query uses distinct - distinct requires that OrderBy column(s) appear in select list
                var isDistinct = selectExpression.Group.Count == 1 && selectExpression.Group[0].IsDistinct;
                if (isDistinct) {
                  // Grab the first column in out list
                  var col0 = selectExpression.Group[0].Columns[0];
                  selectExpression.OrderBy.Add(new OrderByExpression(false, col0));
                  return; 
                }
                if(_dbModel.Driver.Supports(Driver.DbFeatures.AllowsFakeOrderBy)) {
                  selectExpression.OrderBy.Add(new OrderByExpression()); //add fake order by
                  return; 
                }
                // SQL CE case - OrderBy required, but fake orderBy not supported (fake: ORDER BY (SELECT 1) )
                //Take first column from output
                var col1 = FindAnyColumnForFakeOrderBy(selectExpression, context);                
                selectExpression.OrderBy.Add(new OrderByExpression(false, col1));
              }
            }
        }

        private ColumnExpression FindAnyColumnForFakeOrderBy(SelectExpression select, TranslationContext context) {
          var col = select.Operands.OfType<ColumnExpression>().FirstOrDefault();
          if (col != null)
            return col; 
          if (select.Columns.Count > 0)
            return select.Columns[0];
          //register column
          var tbl0 = select.Tables[0];
          col = _translator.RegisterColumn(tbl0, tbl0.TableInfo.PrimaryKey.KeyColumns[0].Column.ColumnName, context);
          return col; 
        }//method

        /// <summary>
        /// Builds the delegate to create a row
        /// </summary>
        /// <param name="context"></param>
        protected Func<IDataRecord, EntitySession, object> CompileObjectMaterializer(TranslationContext context) {
          var reader = context.CurrentSelect.Reader;
          // RI: reader is lambda returning typed object; to easier manage the execution,
          // so we don't have to convert it to generic version, convert the result to 'object' 
          // Now, we might have already a convert inside body to strongly typed object
          // (Table row reader adds a call to AttachEntity which returns object). 
          // In this case just remove this last conversion
          var body = reader.Body;
          var convExpr = body as UnaryExpression;
          if (body.NodeType == ExpressionType.Convert && convExpr.Operand.Type == typeof(object)) {
            reader = Expression.Lambda(convExpr.Operand, reader.Parameters);
          } else {
            var newBody = Expression.Convert(reader.Body, typeof(object));
            reader = Expression.Lambda(newBody, reader.Parameters); 
          }

          reader = (LambdaExpression)_specialExpressionTranslator.Translate(reader);
          reader = (LambdaExpression)_optimizer.Optimize(reader, context);
          var creator = (Func<IDataRecord, EntitySession, object>) reader.Compile();
          return creator; 
        }

        /// <summary>
        /// Processes all expressions in query, with the option to process only SQL targetting expressions
        /// This method is generic, it receives a delegate which does the real processing
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="processOnlySqlParts"></param>
        /// <param name="context"></param>
        protected virtual void ProcessExpressions(Func<Expression, TranslationContext, Expression> processor,
                                                  bool processOnlySqlParts, TranslationContext context)
        {
            for (int scopeExpressionIndex = 0; scopeExpressionIndex < context.SelectExpressions.Count; scopeExpressionIndex++)
            {
                // no need to process the select itself here, all ScopeExpressions that are operands are processed as operands
                // and the main ScopeExpression (the SELECT) is processed below
                var scopeExpression = context.SelectExpressions[scopeExpressionIndex];

                // where clauses
                List<int> whereToRemove = new List<int>(); // List of where clausole evaluating TRUE (could be ignored and so removed)
                bool falseWhere = false; // true when the full where evaluate to FALSE
                for (int whereIndex = 0; whereIndex < scopeExpression.Where.Count; whereIndex++)
                {
                    Expression whereClausole = processor(scopeExpression.Where[whereIndex], context);
                    ConstantExpression constantWhereClausole = whereClausole as ConstantExpression;
                    if (constantWhereClausole != null)
                    {
                        if (constantWhereClausole.Value.Equals(false))
                        {
                            falseWhere = true;
                            break;
                        }
                        else if (constantWhereClausole.Value.Equals(true))
                        {
                            whereToRemove.Add(whereIndex);
                            continue;
                        }
                    }
                    scopeExpression.Where[whereIndex] = whereClausole;
                }
                if (scopeExpression.Where.Count > 0)
                {
                    if (falseWhere)
                    {
                        scopeExpression.Where.Clear();
                        scopeExpression.Where.Add(Expression.Equal(Expression.Constant(true), Expression.Constant(false)));
                    }
                    else
                        foreach (int whereIndex in whereToRemove)
                            scopeExpression.Where.RemoveAt(whereIndex);
                }

                // limit clauses
                if (scopeExpression.Offset != null)
                    scopeExpression.Offset = processor(scopeExpression.Offset, context);
                if (scopeExpression.Limit != null)
                    scopeExpression.Limit = processor(scopeExpression.Limit, context);
                if (scopeExpression.OffsetAndLimit != null)
                    scopeExpression.OffsetAndLimit = processor(scopeExpression.OffsetAndLimit, context);

                context.SelectExpressions[scopeExpressionIndex] = scopeExpression;
            }
            // now process the main SELECT
            if (processOnlySqlParts)
            {
                // if we process only the SQL Parts, these are the operands
                var newOperands = new List<Expression>();
                var oldOperands = context.CurrentSelect.Operands;
                foreach (var operand in oldOperands)
                    newOperands.Add(processor(operand, context));
                context.CurrentSelect = context.CurrentSelect.ChangeOperands(newOperands, oldOperands);
            }
            else
            {
                // the output parameters and result builder
                context.CurrentSelect = (SelectExpression)processor(context.CurrentSelect, context);
            }
        }

        /// <summary>
        /// Optimizes the query by optimizing subexpressions, and preparsing constant expressions
        /// </summary>
        /// <param name="context"></param>
        protected virtual void OptimizeQuery(TranslationContext context)
        {
            ProcessExpressions(_optimizer.Optimize, false, context);
        }

        // Formatting SQL
        static object[] _defaultSqlFormattingArgs;
        private void InitDefaultSqlFormattingArgs() {
          const int MaxSqlFormattingArgs = 1000;
          var prefix = _dbModel.Driver.DynamicSqlParameterPrefix;
          _defaultSqlFormattingArgs = new object[MaxSqlFormattingArgs];
          _defaultSqlFormattingArgs[0] = "{";
          _defaultSqlFormattingArgs[1] = "}";
          for (int i = 2; i < MaxSqlFormattingArgs; i++)
            _defaultSqlFormattingArgs[i] = prefix + "P" + (i - 2);
        }

        private string FormatSql(string sqlTemplate, IList<LinqCommandParameter> parameters, int startParamIndex = 0) {
          if (_defaultSqlFormattingArgs == null)
            InitDefaultSqlFormattingArgs();
          var result = string.Format(sqlTemplate, _defaultSqlFormattingArgs);
          return result;
        }

      /*
              private Expression<Func<object[], object>> BuildPkReaderFromParameter(IList<ParameterExpression> queryParams, ParameterExpression parameter) {
                var arrayParam = Expression.Parameter(typeof(object[]), "Prms");
                var index = queryParams.IndexOf(parameter);
                var readParam = Expression.Convert(Expression.ArrayAccess(arrayParam, Expression.Constant(index)), parameter.Type);
                var ent = _dbModel.EntityApp.Model.GetEntityInfo(parameter.Type, throwIfNotFound: true);
                var pkMembers = ent.PrimaryKey.KeyMembers;
                if(pkMembers.Count > 1)
                  Util.Throw("Entities with composite key are not supported in this context. Expression: {0}.", parameter);
                var pkProp = pkMembers[0].Member.ClrMemberInfo;
                var pkRead = Expression.Convert(Expression.MakeMemberAccess(readParam, pkProp), typeof(object));
                var lambda = (Expression<Func<object[], object>>)Expression.Lambda(pkRead, arrayParam);
                return lambda;
              }
      */


    }//class
}
