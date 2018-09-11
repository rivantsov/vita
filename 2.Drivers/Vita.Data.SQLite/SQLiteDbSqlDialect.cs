using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.SqlGen;

namespace Vita.Data.SQLite {

  public class SQLiteDbSqlDialect : DbSqlDialect {
    public SqlTemplate SqlTemplateStringEqualIgnoreCase = new SqlTemplate("({0} = {1} COLLATE NOCASE)", SqlPrecedence.HighestPrecedence);
    public SqlFragment SqlCollateNoCase = new TextSqlFragment(" COLLATE NOCASE");
    // there's no form for offset-only query, so we just set limit (rowcount) to 10 million
    public SqlTemplate SqlTemplateOffset = new SqlTemplate(" LIMIT 10000000 OFFSET {0} ");
    public SqlTemplate SqlTemplateLimitOffset = new SqlTemplate(" LIMIT {1} OFFSET {0} ");


    public SQLiteDbSqlDialect(SQLiteDbDriver driver) : base(driver) {
      base.MaxParamCount = 999;
      base.DynamicSqlParameterPrefix = "@P";
      base.BatchBeginTransaction = new TextSqlFragment("BEGIN;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;");
      // Change concat operation from Concat(a,b,c) -> a || b || c
      base.SqlTemplateConcatMany = new SqlTemplate("{0}");
      base.SqlConcatListDelimiter = new TextSqlFragment("||");
    }

    public override void InitTemplates() {
      base.InitTemplates();
      AddTemplate("Length({0})", SqlFunctionType.StringLength);
      AddTemplate("{0}", SqlFunctionType.ConvertBoolToBit);

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


  }
}
