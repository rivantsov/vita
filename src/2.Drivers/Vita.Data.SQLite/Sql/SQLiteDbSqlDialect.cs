using System;
using System.Collections.Generic;

using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;

namespace Vita.Data.SQLite {

  public class SQLiteDbSqlDialect : DbSqlDialect {
    public SqlTemplate SqlTemplateStringEqualIgnoreCase = new SqlTemplate("({0} = {1} COLLATE NOCASE)", SqlPrecedence.HighestPrecedence);
    public SqlFragment SqlCollateNoCase = new TextSqlFragment(" COLLATE NOCASE");

    public SQLiteDbSqlDialect(SQLiteDbDriver driver) : base(driver) {
      base.MaxParamCount = 999;
      // there's no form for offset-only query, so we just set limit (rowcount) to 10 million
      base.OffsetTemplate = new SqlTemplate(" LIMIT 10000000 OFFSET {0} ");
      base.OffsetLimitTemplate = new SqlTemplate(" LIMIT {1} OFFSET {0} ");

      base.DynamicSqlParameterPrefix = "@P";
      base.BatchBeginTransaction = new TextSqlFragment("BEGIN;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;");
      // Change concat operation from Concat(a,b,c) -> a || b || c
      base.SqlTemplateConcatMany = new SqlTemplate("{0}");
      base.SqlConcatListDelimiter = new TextSqlFragment("||");
    }

    public override void InitTemplates() {
      base.InitTemplates();
      AddTemplate("Upper({0})", SqlFunctionType.ToUpper);
      AddTemplate("Lower({0})", SqlFunctionType.ToLower);
      AddTemplate("Length({0})", SqlFunctionType.StringLength);
      AddTemplate("{0}", SqlFunctionType.ConvertBoolToBit);
      AddTemplate("date({0})", SqlFunctionType.Date);

      AddTemplate("time({0})", SqlFunctionType.Time);

      AddTemplate("strftime({0}, '%H')", SqlFunctionType.Hour);

    }

    public override SqlTemplate GetSqlFunctionTemplate(SqlFunctionExpression expr) {
      switch(expr.FunctionType) {
        case SqlFunctionType.StringEqual:
          var ignoreCase = true; // expr.ForceIgnoreCase
          if(ignoreCase)
            return SqlTemplateStringEqualIgnoreCase;
          break;
      }
      return base.GetSqlFunctionTemplate(expr);
    }

    // Used by DbInfo module
    public override string GetTableExistsSql(DbTableInfo table) {
      var sql = string.Format("select name from sqlite_master where type='table' AND name = '{0}'", table.TableName);
      return sql; 
    }

  }
}
