using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.SqlGen;
using Vita.Data.Linq.Translation;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq {
 
  partial class LinqEngine {

    public SqlStatement TranslateNonQuery(EntityCommand command) {
      QueryPreprocessor.PreprocessCommand(_entityModel, command);
      var translCtx = new TranslationContext(_dbModel, command);
      var queryInfo = command.Info;
      // convert lambda params into an initial set of ExternalValueExpression objects; 
      foreach(var prm in queryInfo.Lambda.Parameters) {
        var inpParam = new ExternalValueExpression(prm);
        translCtx.ExternalValues.Add(inpParam);
      }
      //Analyze/transform base select query
      var selectExpr = TranslateSelectExpression(queryInfo.Lambda.Body, translCtx);
      // Analyze external values (parameters?), create DbParameters
      var cmdParams = BuildExternalValuesPlaceHolders(queryInfo, translCtx);
      // If there's at least one parameter that must be converted to literal (ex: value list), we cannot cache the query
      /*
      bool canCache = !translCtx.ExternalValues.Any(v => v.SqlMode == SqlValueMode.Literal);
      if (!canCache)
        flags |= QueryOptions.NoQueryCache; 
        */
      // !!! Before that, everyting is the same as in TranslateSelect
      var targetEnt = command.TargetEntity;
      var targetTable = _dbModel.GetTable(targetEnt.EntityType);
      var targetTableExpr = new TableExpression(targetTable);
      var commandData = new NonQueryLinqCommandData(command, selectExpr, targetTableExpr);
      // Analyze base query output expression
      var readerBody = selectExpr.RowReaderLambda.Body;
      switch(command.Operation) {
        case EntityOperation.Update:
        case EntityOperation.Insert: 
          Util.Check(readerBody.NodeType == ExpressionType.New, "Query for LINQ {0} command must return New object", commandData.Operation);
          var newExpr = readerBody as NewExpression;
          var outValues = selectExpr.Operands.ToList();
          for(int i = 0; i < newExpr.Members.Count; i++) {
            var memberName = newExpr.Members[i].Name;
            var memberInfo = targetEnt.GetMember(memberName);
            Util.Check(memberInfo != null, "Member {0} not found in entity {1}.", memberName, targetEnt, targetEnt.EntityType);
            switch(memberInfo.Kind) {
              case EntityMemberKind.Column: 
                var col = _translator.CreateColumnForMember(targetTableExpr, memberInfo, translCtx);
                commandData.TargetColumns.Add(col);
                commandData.SelectOutputValues.Add(outValues[i]);
                break; 
              case EntityMemberKind.EntityRef:
                var fromKey = memberInfo.ReferenceInfo.FromKey;
                Util.Check(fromKey.ExpandedKeyMembers.Count == 1,
                  "References with composite keys are not supported in LINQ non-query operations. Reference: ", memberName);
                var pkMember = fromKey.ExpandedKeyMembers[0].Member; 
                var col2 = _translator.CreateColumnForMember(targetTableExpr, pkMember, translCtx);
                commandData.TargetColumns.Add(col2);
                commandData.SelectOutputValues.Add(outValues[i]);
                break; 
              default:
                Util.Throw("Property cannot be used in the context: {0}.", memberName);
                break; 
            }
          }
          break; 
        case EntityOperation.Delete:
          commandData.SelectOutputValues.Add(readerBody); //should return single value - primary key
          break; 
      }
      // Build SQL
      var sqlBuilder = _dbModel.Driver.CreateDbSqlBuilder(_dbModel, queryInfo);
      var stmt = sqlBuilder.BuildLinqNonQuery(commandData);
      return stmt; // new TranslatedLinqCommand(command, sql, cmdParams, command.Info.Flags);
    } //method

    private static void ThrowTranslationFailed(EntityCommand command, string message, params object[] args) {
      var msg = Util.SafeFormat(message, args) + "\r\n Query:" + command.ToString(); 
      var exc = new LinqTranslationException(msg, command);
      throw exc; 
    }
  }//class
}//ns
