using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data.Linq.Translation.Expressions;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Sql;
using Vita.Entities.Locking;
using Vita.Data.Linq.Translation;
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

    // Note: command expected to be analyzed already
    public SqlStatement Translate(LinqCommand command) {
      // pre-process; special commands do not produce lambda when command is created - only SqlCacheKey so that SQL 
      // is looked up in cache and we do not need anything else; building actual query lambda is encoded in setup Action, 
      // which we invoke here;
      // also dynamic linq queries postpone rewrite (changing locals to parameters) until late time
      switch(command) {
        case SpecialLinqCommand specCmd: 
          specCmd.SetupAction?.Invoke(specCmd);
          break;
        case DynamicLinqCommand dynCmd:
          if(command.Lambda == null)
            LinqCommandRewriter.RewriteToLambda(dynCmd);
          break;
      } //switch

      try {
        switch(command.Operation) {
          case LinqOperation.Select:
            return TranslateSelect(command);
          case LinqOperation.Update:
          case LinqOperation.Delete:
          case LinqOperation.Insert:
          default:
            return TranslateNonQuery((DynamicLinqCommand) command);
        } //switch
      } catch(LinqTranslationException) {
        throw; // if it is already Linq translation exception, pass it up.
      } catch(Exception ex) {
        var message = "Linq to SQL translation failed, invalid expression: " + ex.Message;
        throw new LinqTranslationException(message, command, ex);
      }
    }

    public SqlStatement TranslateSelect(LinqCommand command) {
      var context = new TranslationContext(_dbModel, command); 
      // convert lambda params into an initial set of ExternalValueExpression objects; 
      foreach(var prm in command.Lambda.Parameters) {
        var inpParam = new ExternalValueExpression(prm);
        context.ExternalValues.Add(inpParam);
      }
      //Analyze/transform query expression
      var selectExpr = TranslateSelectExpression(command, context);
      //Build SQL, compile row reader
      var sqlBuilder = _dbModel.Driver.CreateLinqSqlBuilder(_dbModel, command);
      var sqlStmt = sqlBuilder.BuildSelectStatement(selectExpr);
      var rowReader = CompileRowReader(selectExpr);
      var outType = context.CurrentSelect.ReaderOutputType;
      var rowListCreator = GetListCreator(outType);
      //check if we need to create implicit result set processor. 
      var rowListProcessor = selectExpr.RowListProcessor;
      if (outType != typeof(object) && rowListProcessor == null) { //object type is special Md1 case
        var returnsResultSet = typeof(IQueryable).IsAssignableFrom(command.Lambda.Body.Type);
        if(!returnsResultSet)
          rowListProcessor = RowListProcessor.CreateFirstSingleLast("First", outType);
      }
      sqlStmt.ResultProcessor = new DataReaderProcessor() 
          { RowReader = rowReader, RowListCreator = rowListCreator, RowListProcessor = rowListProcessor };
      return sqlStmt;
    }

    protected virtual SelectExpression TranslateSelectExpression(LinqCommand cmd, TranslationContext context) {
      if (cmd.SelectExpression != null) { //special case for Md1, Select is provided in command
        context.CurrentSelect = cmd.SelectExpression;
        return cmd.SelectExpression; 
      }
      var linqExpr = cmd.Lambda.Body; 
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
      cmd.SelectExpression = context.CurrentSelect;
      return context.CurrentSelect;
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
        foreach(var tableExpression in tables)
            AssignTableAlias(tableExpression, allAliases);
      }
    }

    protected virtual void AssignTableAlias(TableExpression table, IList<string> allAliases) {
      // Note: table might be sub-select, without table.TableInfo
      string dftAlias = table.TableInfo?.DefaultSqlAlias ?? "t";
      table.Alias = dftAlias;
      int index = 0;
      while(allAliases.Contains(table.Alias))
        table.Alias =  dftAlias + index++;
      allAliases.Add(table.Alias);
      // append $ to guarantee no collision with table or column name
      table.Alias += '$'; 
    }

    protected virtual IList<string> GetAllAliases(TranslationContext context) {
      var aliases = new List<string>();
      aliases.AddRange(context.EnumerateAllTables().Select(t => t.Alias).Where(a => a != null));
      aliases.AddRange(context.EnumerateScopeColumns().Select(c => c.Alias).Where(a => a != null));
      return aliases;
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
    protected Func<IDataRecord, EntitySession, object> CompileRowReader(SelectExpression selectExpr) {
      // fast track for reading entities
      if(selectExpr.EntityReader != null)
        return selectExpr.EntityReader.ReadEntity; 
      // anything other than entities
      var readerLambda = selectExpr.RowReaderLambda;
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

    static ConcurrentDictionary<Type, Func<IList>> _listCreators = new ConcurrentDictionary<Type, Func<IList>>();
    private static Func<IList> GetListCreator(Type rowType) {
      if(_listCreators.TryGetValue(rowType, out Func<IList> creator))
        return creator;
      creator = ReflectionHelper.GetCompiledGenericListCreator(rowType);
      _listCreators.TryAdd(rowType, creator);
      return creator;
    }

  }//class
}
