
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Vita.Data.Linq.Translation.Expressions;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.SqlGen;
using Vita.Entities.Locking;
using Vita.Data.Linq.Translation;
using System.Collections.Concurrent;
using Vita.Data.Runtime;

namespace Vita.Data.Linq {

  internal partial class LinqEngine {
    DbModel _dbModel;
    EntityModel _entityModel; 
    ExpressionTranslator _translator;

    public LinqEngine(DbModel dbModel) {
      _dbModel = dbModel;
      _entityModel = _dbModel.EntityModel; 
      _translator = new ExpressionTranslator(dbModel);
    }

    public SqlStatement Translate(EntityCommand command) {
      if(command.Info == null)
        QueryAnalyzer.Analyze(_entityModel, command);
      try {
        switch(command.Operation) {
          case EntityOperation.Select:
            return TranslateSelect(command);
          case EntityOperation.Update:
          case EntityOperation.Delete:
          case EntityOperation.Insert:
            return TranslateNonQuery(command);
          default:
            ThrowTranslationFailed(command, "Unsupported LINQ command type.");
            return null;

        }
      } catch(LinqTranslationException) {
        throw; // if it is alread Linq translation exception, pass it up.
      } catch(Exception ex) {
        var message = "Linq to SQL translation failed: " + ex.Message +
                   "\r\nPossibly facilities you are trying to use are not supported. " +
                   "\r\nTry to reformulate/simplify the query. Hint: do not use c# functions/methods inside query directly. ";
        throw new LinqTranslationException(message, command, ex);
      }
    }

    public SqlStatement TranslateSelect(EntityCommand command) {
      QueryPreprocessor.PreprocessCommand(_entityModel, command);
      var context = new TranslationContext(_dbModel, command); 
      var queryInfo = command.Info;
      // convert lambda params into an initial set of ExternalValueExpression objects; 
      foreach(var prm in queryInfo.Lambda.Parameters) {
        var inpParam = new ExternalValueExpression(prm);
        context.ExternalValues.Add(inpParam);
      }
      //Analyze/transform query expression
      var selectExpr = TranslateSelectExpression(queryInfo.Lambda.Body, context);
      /*
      // If there's at least one parameter that must be converted to literal (ex: value list), we cannot cache the query
      bool canCache = !context.ExternalValues.Any(v => v.SqlNodeType == SqlExpressionTy=pe..SqlMode == SqlValueMode.Literal);
      if(!canCache)
        command.Info.Flags |= QueryOptions.NoQueryCache;
        */
      //Build SQL, compile row reader
      var sqlStmt = BuildSelectStatement(context, selectExpr, queryInfo);
      var rowReader = CompileRowReader(context);
      var outType = context.CurrentSelect.ReaderOutputType;//.RowReaderLambda.Body.Type;
      var rowListCreator = GetListCreator(outType);
      //check if we need to create implicit result set processor
      var rowListProcessor = selectExpr.RowListProcessor;
      if (rowListProcessor == null) {
        var returnsResultSet = typeof(IQueryable).IsAssignableFrom(queryInfo.Lambda.Body.Type);
        if(!returnsResultSet)
          rowListProcessor = RowListProcessor.CreateFirstSingleLast("First", outType);
      }
      sqlStmt.ResultProcessor = new DataReaderProcessor() 
          { RowReader = rowReader, RowListCreator = rowListCreator, RowListProcessor = rowListProcessor };
      return sqlStmt;
    }

    protected virtual SelectExpression TranslateSelectExpression(Expression linqExpr, TranslationContext context) {
      var exprChain = ExpressionChain.Build(linqExpr);
      var tableExpr = _translator.ExtractFirstTable(exprChain[0], context);
      var selectExpression = _translator.Analyze(exprChain, tableExpr, context);
      // Check expected type - it will be used for final result conversion if query returns a single value (like Count() query)
      var resultType = exprChain[exprChain.Count - 1].Type;
      if(resultType.IsGenericQueryable())
        resultType = selectExpression.Type;
      _translator.BuildSelectResultReaderAndCutOutSql(selectExpression, context, resultType);

      BuildOffsetsAndLimits(context);
      // then prepare Parts for SQL translation
      CheckTablesAlias(context);
      CheckColumnNamesAliases(context);
      return context.CurrentSelect;
    }



    static ConcurrentDictionary<Type, Func<IList>> _listCreators = new ConcurrentDictionary<Type, Func<IList>>(); 
    private Func<IList> GetListCreator(Type rowType) {
      if(_listCreators.TryGetValue(rowType, out Func<IList> creator))
        return creator;
      creator = ReflectionHelper.GetCompiledGenericListCreator(rowType);
      _listCreators.TryAdd(rowType, creator);
      return creator; 
    }

    private SqlStatement BuildSelectStatement(TranslationContext context, SelectExpression selectExpr, QueryInfo queryInfo) {
      var sqlBuilder = _dbModel.Driver.CreateDbSqlBuilder(_dbModel, selectExpr.QueryInfo);
      var placeHolders = BuildExternalValuesPlaceHolders(queryInfo, context);
      var sql = sqlBuilder.BuildSelect(selectExpr, queryInfo.LockType);
      var sqlStmt = new SqlStatement(sql, placeHolders, DbExecutionType.Reader, 
                       _dbModel.Driver.SqlDialect.PrecedenceHandler, queryInfo.Options);
      sqlStmt.Fragments.Add(SqlTerms.Semicolon);
      sqlStmt.Fragments.Add(SqlTerms.NewLine);
      return sqlStmt; 
    }

    private SqlPlaceHolderList BuildExternalValuesPlaceHolders(QueryInfo queryInfo, TranslationContext context) {
      var placeHolders = new SqlPlaceHolderList();
      foreach(var extValue in context.ExternalValues) {
        if(extValue.SqlUseCount == 0)
          continue;
        BuildSqlPlaceHolderForExternalValue(queryInfo, extValue); 
        placeHolders.Add(extValue.SqlPlaceHolder);
      }//foreach extValue
      return placeHolders;
    }

    private void BuildSqlPlaceHolderForExternalValue(QueryInfo queryInfo, ExternalValueExpression extValue) {
      var dataType = extValue.SourceExpression.Type;
      var driver = _dbModel.Driver; 
      var typeRegistry = driver.TypeRegistry;
      var valueReader = BuildParameterValueReader(queryInfo.Lambda.Parameters, extValue.SourceExpression);
      if(dataType.IsListOfDbPrimitive(out var elemType)) {
        // list parameter
        var elemTypeDef = typeRegistry.GetDbTypeDef(elemType);
        Util.Check(elemTypeDef != null, "Failed to match DB type for CLR type {0}", elemType);
        extValue.SqlPlaceHolder = new SqlListParamPlaceHolder(elemType, elemTypeDef, valueReader,
                   listToDbParamValue: list => driver.SqlDialect.ConvertListParameterValue(list, elemType),
                   // ToLiteral
                   listToLiteral: list => driver.SqlDialect.ListToLiteral(list, elemTypeDef)
                   );
      } else {
        //regular linq parameter
        var typeDef = typeRegistry.GetDbTypeDef(dataType);
        Util.Check(typeDef != null, "Failed to find DB type for linq parameter of type {0}", dataType);
        var dbConv = typeRegistry.GetDbValueConverter(typeDef.ColumnOutType, dataType);
        Util.Check(dbConv != null, "Failed to find converter from type {0} to type {1}", dataType, typeDef.ColumnOutType);
        extValue.SqlPlaceHolder = new SqlLinqParamPlaceHolder(dataType, valueReader, dbConv.PropertyToColumn, typeDef.ToLiteral);
      }
    }

    private Func<object[], object> BuildParameterValueReader(IList<ParameterExpression> queryParams, Expression valueSourceExpression) {
      // One trouble - for Binary object, we need to convert them to byte[]
      if(valueSourceExpression.Type == typeof(Binary)) {
        var methGetBytes = typeof(Binary).GetMethod(nameof(Binary.GetBytes));
        valueSourceExpression = Expression.Call(valueSourceExpression, methGetBytes);
      }
      //Quick path: most of the time the source expression is just a lambda parameter
      if(valueSourceExpression.NodeType == ExpressionType.Parameter) {
        var prmSource = (ParameterExpression)valueSourceExpression;
        var index = queryParams.IndexOf(prmSource);
        return (object[] prms) => prms[index];
      }
      // There is some computation in valueSourceExpression; use dynamic invoke to evaluate it.
      // DynamicInvoke is not efficient but this case is rare enough, so it is not worth more trouble
      var valueReadLambda = Expression.Lambda(valueSourceExpression, queryParams);
      var compiledValueRead = valueReadLambda.Compile();
      return (object[] prms) => compiledValueRead.DynamicInvoke(prms);
    }

    protected virtual IList<string> GetAllAliases(TranslationContext context) {
      var aliases = new List<string>();
      aliases.AddRange(context.EnumerateAllTables().Select(t => t.Alias).Where(a => a != null));
      aliases.AddRange(context.EnumerateScopeColumns().Select(c => c.Alias).Where(a => a != null));
      return aliases;
    }

    protected virtual string GenerateTableAlias(IList<string> allAliases) {
      int index = 0;
      string alias;
      do {
        alias = "t" + index++;
      } while(allAliases.Contains(alias));
      allAliases.Add(alias);
      return alias;
    }

    /// <summary>
    /// Give all non-aliased tables a name
    /// </summary>
    /// <param name="context"></param>
    protected void CheckTablesAlias(TranslationContext context) {
      var tables = context.EnumerateAllTables().Distinct().ToList();
      // just to be nice: if we have only one table involved, there's no need to alias it
      if(tables.Count == 1) {
        tables[0].Alias = null;
      } else {
        var allAliases = GetAllAliases(context);
        foreach(var tableExpression in tables) {
          // if no alias, or duplicate alias
          if(string.IsNullOrEmpty(tableExpression.Alias) || allAliases.Count(s => s == tableExpression.Alias) > 1)
            tableExpression.Alias = GenerateTableAlias(allAliases);
          if(!tableExpression.Alias.EndsWith("$"))
            tableExpression.Alias = tableExpression.Alias + "$";
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
        if(col != null) {
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
    /// This is a hint for SQL generations
    /// </summary>
    /// <param name="context"></param>
    protected virtual void BuildOffsetsAndLimits(TranslationContext context) {
      foreach(var selectExpression in context.SelectExpressions) {
        //RI: because of some problems with DISTINCT+TOP combination, we always force using Skip/Take combination (avoiding TOP)
        if(selectExpression.Offset == null && selectExpression.Limit == null)
          continue;
        if(selectExpression.Offset == null)
          selectExpression.Offset = Expression.Constant(0);
        //Check that we have OrderBy - it is required if we have offset/limit
        if(selectExpression.OrderBy.Count == 0 && _dbModel.Driver.Supports(Driver.DbFeatures.SkipTakeRequireOrderBy)) {
          // We do not have OrderBy but using Skip/Take requires it. 
          // We have to make it up. First check if query uses distinct - distinct requires that OrderBy column(s) appear in select list
          var isDistinct = selectExpression.Group.Count == 1 && selectExpression.Group[0].IsDistinct;
          if(isDistinct) {
            // Grab the first column in out list
            var col0 = selectExpression.Group[0].Columns[0];
            selectExpression.OrderBy.Add(new OrderByExpression(false, col0));
            return;
          }
          if(_dbModel.Driver.Supports(Driver.DbFeatures.AllowsFakeOrderBy)) {
            selectExpression.Flags |= SelectExpressionFlags.NeedsFakeOrderBy;
            return;
          }
          // SQL CE case - OrderBy required, but fake orderBy not supported (fake: ORDER BY (SELECT 1) )
          //Take first column from output
          // var col1 = FindAnyColumnForFakeOrderBy(selectExpression, context);
          // selectExpression.OrderBy.Add(new OrderByExpression(false, col1));
        }
      }
    }

    /// <summary>
    /// Builds the delegate to create a row
    /// </summary>
    /// <param name="context"></param>
    protected Func<IDataRecord, EntitySession, object> CompileRowReader(TranslationContext context) {
      // fast track for reading entities
      var currSelect = context.CurrentSelect;
      if(currSelect.EntityReader != null)
        return currSelect.EntityReader.ReadEntity; 
      // anything other than entities
      var readerLambda = currSelect.RowReaderLambda;
      // RI: reader is lambda returning typed object; to easier manage the execution,
      // so we don't have to convert it to generic version, convert the result to 'object' 
      // Now, we might have already a convert inside body to strongly typed object
      // (Table row reader adds a call to AttachEntity which returns object). 
      // In this case just remove this last conversion
      var body = readerLambda.Body;
      var convExpr = body as UnaryExpression;
      if(body.NodeType == ExpressionType.Convert && convExpr.Operand.Type == typeof(object)) {
        readerLambda = Expression.Lambda(convExpr.Operand, readerLambda.Parameters);
      } else {
        var newBody = Expression.Convert(readerLambda.Body, typeof(object));
        readerLambda = Expression.Lambda(newBody, readerLambda.Parameters);
      }

      var rowReader = (Func<IDataRecord, EntitySession, object>)readerLambda.Compile();
      return rowReader;
    }

    /// <summary>
    /// Processes all expressions in query, with the option to process only SQL targetting expressions
    /// This method is generic, it receives a delegate which does the real processing
    /// </summary>
    /// <param name="processor"></param>
    /// <param name="processOnlySqlParts"></param>
    /// <param name="context"></param>
    protected virtual void ProcessExpressions(Func<Expression, TranslationContext, Expression> processor,
                                              bool processOnlySqlParts, TranslationContext context) {
      for(int scopeExpressionIndex = 0; scopeExpressionIndex < context.SelectExpressions.Count; scopeExpressionIndex++) {
        // no need to process the select itself here, all ScopeExpressions that are operands are processed as operands
        // and the main ScopeExpression (the SELECT) is processed below
        var scopeExpression = context.SelectExpressions[scopeExpressionIndex];

        // where clauses
        List<int> whereToRemove = new List<int>(); // List of where clausole evaluating TRUE (could be ignored and so removed)
        bool falseWhere = false; // true when the full where evaluate to FALSE
        for(int whereIndex = 0; whereIndex < scopeExpression.Where.Count; whereIndex++) {
          Expression whereClause = processor(scopeExpression.Where[whereIndex], context);
          ConstantExpression constWhere = whereClause as ConstantExpression;
          if(constWhere != null) {
            if(constWhere.Value.Equals(false)) {
              falseWhere = true;
              break;
            } else if(constWhere.Value.Equals(true)) {
              whereToRemove.Add(whereIndex);
              continue;
            }
          }
          scopeExpression.Where[whereIndex] = whereClause;
        }
        if(scopeExpression.Where.Count > 0) {
          if(falseWhere) {
            scopeExpression.Where.Clear();
            scopeExpression.Where.Add(Expression.Equal(Expression.Constant(true), Expression.Constant(false)));
          } else
            foreach(int whereIndex in whereToRemove)
              scopeExpression.Where.RemoveAt(whereIndex);
        }

        // limit clauses
        if(scopeExpression.Offset != null)
          scopeExpression.Offset = processor(scopeExpression.Offset, context);
        if(scopeExpression.Limit != null)
          scopeExpression.Limit = processor(scopeExpression.Limit, context);

        context.SelectExpressions[scopeExpressionIndex] = scopeExpression;
      }
      // now process the main SELECT
      if(processOnlySqlParts) {
        // if we process only the SQL Parts, these are the operands
        var newOperands = new List<Expression>();
        var oldOperands = context.CurrentSelect.Operands;
        foreach(var operand in oldOperands)
          newOperands.Add(processor(operand, context));
        context.CurrentSelect = context.CurrentSelect.ChangeOperands(newOperands, oldOperands);
      } else {
        // the output parameters and result builder
        context.CurrentSelect = (SelectExpression)processor(context.CurrentSelect, context);
      }
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
