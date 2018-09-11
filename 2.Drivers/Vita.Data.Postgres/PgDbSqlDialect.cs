using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.SqlGen;

namespace Vita.Data.Postgres {
  public class PgDbSqlDialect : DbSqlDialect {
    public SqlFragment SqlLockForUpdate = new TextSqlFragment(" FOR UPDATE");
    public SqlFragment SqlLockInShareMode = new TextSqlFragment("  FOR SHARE");
    public SqlTemplate SqlOffsetTemplate = new SqlTemplate("OFFSET {0} ");
    public SqlTemplate SqlLimitTemplate = new SqlTemplate("LIMIT {0} ");
    public SqlTemplate SqlCrudTemplateInsertReturnIdentity = new SqlTemplate(
@"INSERT INTO {0} 
    ({1})
    VALUES 
    {2} 
    RETURNING {3};");


    public PgDbSqlDialect(PgDbDriver driver) : base(driver) {
      base.MaxParamCount = 32000; //reported also 65K
      base.DynamicSqlParameterPrefix = "@P"; 
      base.BatchBeginTransaction = new TextSqlFragment("START TRANSACTION;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;");
    }

    public override void InitTemplates() {
      base.InitTemplates();

      AddTemplate("DATE({0})", SqlFunctionType.Date);
      AddTemplate("DATE_PART('time', {0})", SqlFunctionType.Time);
      AddTemplate("EXTRACT(WEEK FROM {0})", SqlFunctionType.Week);

      AddTemplate("EXTRACT(YEAR FROM {0})", SqlFunctionType.Year);
      AddTemplate("EXTRACT(MONTH FROM {0})", SqlFunctionType.Month);
      AddTemplate("EXTRACT(DAY FROM {0})", SqlFunctionType.Day);

      //sequence name in double quotes inside single-quote argument
      AddTemplate("nextval('{0}')", SqlFunctionType.SequenceNextValue);
      AddTemplate("uuid_generate_v1()", SqlFunctionType.NewGuid);
      AddTemplate("{0} IN ({1})", SqlFunctionType.InArray);
      AddTemplate("char_length({0})", SqlFunctionType.StringLength);
    }

    // schema should not be quoted
    public override string FormatFullName(string schema, string name) {
      return schema + "." + base.LeftSafeQuote + name + base.RightSafeQuote;
    }

    SqlTemplate SqlTemplateLikeIgnoreCase = new SqlTemplate("{0} ILIKE {1} ESCAPE '\\'");
    SqlTemplate SqlTemplateStringEqualIgnoreCase = new SqlTemplate("({0} ILIKE {1} ESCAPE '\\')");

    public override SqlTemplate GetSqlFunctionTemplate(SqlFunctionExpression expr) {
      switch(expr.FunctionType) {
        case SqlFunctionType.Like:
          if(expr.ForceIgnoreCase)
            return SqlTemplateLikeIgnoreCase;
          break; //use default like
        case SqlFunctionType.StringEqual:
          if(expr.ForceIgnoreCase)
            return SqlTemplateStringEqualIgnoreCase;
          break;
      }//switch
      return base.GetSqlFunctionTemplate(expr);
    }



  }
}
