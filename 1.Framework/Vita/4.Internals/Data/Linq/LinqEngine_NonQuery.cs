using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Sql;
using Vita.Data.Linq.Translation;
using Vita.Data.Model;

namespace Vita.Data.Linq {
 
  partial class LinqEngine {

    public SqlStatement TranslateNonQuery(LinqCommand command) {
      var translCtx = new TranslationContext(_dbModel, command);
      // convert lambda params into an initial set of ExternalValueExpression objects; 
      foreach(var prm in command.Lambda.Parameters) {
        var inpParam = new ExternalValueExpression(prm);
        translCtx.ExternalValues.Add(inpParam);
      }
      //Analyze/transform base select query
      var selectExpr = TranslateSelectExpression(command.Lambda.Body, translCtx);
      var targetEnt = command.UpdateEntity;
      var targetTable = _dbModel.GetTable(targetEnt.EntityType);
      var nonQueryCmd = new NonQueryLinqCommand(command, targetTable, selectExpr);
      // Analyze base query output expression
      var targetTableExpr = new TableExpression(targetTable);
      var readerBody = selectExpr.RowReaderLambda.Body;
      switch(nonQueryCmd.Operation) {
        case LinqOperation.Update:
        case LinqOperation.Insert: 
          Util.Check(readerBody.NodeType == ExpressionType.New, 
                "Query for LINQ {0} command must return New object", nonQueryCmd.Operation);
          var newExpr = readerBody as NewExpression;
          var outValues = selectExpr.Operands.ToList();
          for(int i = 0; i < newExpr.Members.Count; i++) {
            var memberName = newExpr.Members[i].Name;
            var memberInfo = targetEnt.GetMember(memberName);
            Util.Check(memberInfo != null, "Member {0} not found in entity {1}.", memberName, targetEnt, targetEnt.EntityType);
            switch(memberInfo.Kind) {
              case EntityMemberKind.Column: 
                var col = _translator.CreateColumnForMember(targetTableExpr, memberInfo, translCtx);
                nonQueryCmd.TargetColumns.Add(col.ColumnInfo);
                nonQueryCmd.SelectOutputValues.Add(outValues[i]);
                break; 
              case EntityMemberKind.EntityRef:
                var fromKey = memberInfo.ReferenceInfo.FromKey;
                Util.Check(fromKey.ExpandedKeyMembers.Count == 1,
                  "References with composite keys are not supported in LINQ non-query operations. Reference: ", memberName);
                var pkMember = fromKey.ExpandedKeyMembers[0].Member; 
                var col2 = _translator.CreateColumnForMember(targetTableExpr, pkMember, translCtx);
                nonQueryCmd.TargetColumns.Add(col2.ColumnInfo);
                nonQueryCmd.SelectOutputValues.Add(outValues[i]);
                break; 
              default:
                Util.Throw("Property cannot be used in the context: {0}.", memberName);
                break; 
            }
          }
          break; 
        case LinqOperation.Delete:
          nonQueryCmd.SelectOutputValues.Add(readerBody); //should return single value - primary key
          break; 
      }
      // Build SQL
      var sqlBuilder = _dbModel.Driver.CreateLinqNonQuerySqlBuilder(_dbModel, nonQueryCmd);
      var stmt = sqlBuilder.BuildLinqNonQuerySql();
      return stmt; 
    } //method


  }//class
}//ns
