using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Linq;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Linq.Translation.SqlGen;

namespace Vita.Data.Linq.Translation {
 
  partial class LinqEngine {
    private TranslatedLinqCommand TranslateNonQuery(LinqCommand command) {
      LinqCommandPreprocessor.PreprocessCommand(_dbModel.EntityApp.Model, command);
      var rewriterContext = new TranslationContext(_dbModel, command);
      var cmdInfo = command.Info;
      // convert lambda params into an initial set of ExternalValueExpression objects; 
      foreach(var prm in cmdInfo.Lambda.Parameters) {
        var inpParam = new ExternalValueExpression(prm);
        rewriterContext.ExternalValues.Add(inpParam);
      }
      //Analyze/transform base select query
      var exprChain = ExpressionChain.Build(cmdInfo.Lambda.Body);
      var selectExpr = BuildSelectExpression(exprChain, rewriterContext);
      // Analyze external values (parameters?), create DbParameters
      var cmdParams = BuildParameters(command, rewriterContext);
      var flags = command.Info.Flags; 
      // If there's at least one parameter that must be converted to literal (ex: value list), we cannot cache the query
      bool canCache = !rewriterContext.ExternalValues.Any(v => v.SqlUse == ExternalValueSqlUse.Literal);
      if (!canCache)
        flags |= LinqCommandFlags.NoQueryCache; 

      // !!! Before that, everyting is the same as in TranslateSelect
      var targetEnt = command.TargetEntity;
      var targetTableInfo = _dbModel.GetTable(targetEnt.EntityType);
      TableExpression targetTable;
      bool isSingleTable = selectExpr.Tables.Count == 1 && selectExpr.Tables[0].TableInfo == targetTableInfo;
      if(isSingleTable) {
        targetTable = selectExpr.Tables[0];
      } else
        targetTable = _translator.CreateTable(targetEnt.EntityType, rewriterContext);
      var commandData = new NonQueryLinqCommandData(command, selectExpr, targetTable, isSingleTable);
      // Analyze base query output expression
      var readerBody = selectExpr.Reader.Body;
      switch(command.CommandType) {
        case LinqCommandType.Update:
        case LinqCommandType.Insert: 
          Util.Check(readerBody.NodeType == ExpressionType.New, "Query for LINQ {0} command must return New object", commandData.CommandType);
          var newExpr = readerBody as NewExpression;
          var outValues = selectExpr.Operands.ToList();
          for(int i = 0; i < newExpr.Members.Count; i++) {
            var memberName = newExpr.Members[i].Name;
            var memberInfo = targetEnt.GetMember(memberName);
            Util.Check(memberInfo != null, "Member {0} not found in entity {1}.", memberName, targetEnt, targetEnt.EntityType);
            switch(memberInfo.Kind) {
              case MemberKind.Column: 
                var col = _translator.CreateColumn(targetTable, memberName, rewriterContext);
                commandData.TargetColumns.Add(col);
                commandData.SelectOutputValues.Add(outValues[i]);
                break; 
              case MemberKind.EntityRef:
                var fromKey = memberInfo.ReferenceInfo.FromKey;
                Util.Check(fromKey.ExpandedKeyMembers.Count == 1,
                  "References with composite keys are not supported in LINQ non-query operations. Reference: ", memberName);
                var pkMember = fromKey.ExpandedKeyMembers[0].Member; 
                var col2 = _translator.CreateColumn(targetTable, pkMember.MemberName, rewriterContext);
                commandData.TargetColumns.Add(col2);
                commandData.SelectOutputValues.Add(outValues[i]);
                break; 
              default:
                Util.Throw("Property cannot be used in the context: {0}.", memberName);
                break; 
            }
          }
          break; 
        case LinqCommandType.Delete:
          commandData.SelectOutputValues.Add(readerBody); //should return single value - primary key
          break; 
      }
      // Build SQL
      var sqlBuilder = new SqlBuilder(_dbModel);
      var sqlStmt = sqlBuilder.BuildNonQuery(commandData);
      var sqlTemplate = sqlStmt.ToString();
      var defaultSql = FormatSql(sqlTemplate, cmdParams);
      return new TranslatedLinqCommand(sqlTemplate, defaultSql, cmdParams, flags);
    } //method

    private static void ThrowTranslationFailed(LinqCommand command, string message, params object[] args) {
      var msg = StringHelper.SafeFormat(message, args) + "\r\n Query:" + command.ToString(); 
      var exc = new LinqTranslationException(msg, command);
      throw exc; 
    }
  }//class
}//ns
