using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.SqlGen;

namespace Vita.Data.MySql {

  public class MySqlDialect : DbSqlDialect {
    public SqlFragment SqlTermForUpdate = new TextSqlFragment(" FOR UPDATE");
    public SqlFragment SqlTermLockInShareMode = new TextSqlFragment(" LOCK IN SHARE MODE");
    public SqlFragment SqlSelectIdentity = new TextSqlFragment("SELECT LAST_INSERT_ID();");
    // MySql does not have syntax for offset-only, so we set limit to 10 million recs
    public SqlTemplate OffsetTemplate = new SqlTemplate(" LIMIT 10000000 OFFSET {0} ");
    public SqlTemplate OffsetLimitTemplate = new SqlTemplate(" LIMIT {1} OFFSET {0} ");


    public MySqlDialect(MySqlDbDriver driver) : base(driver) {
      base.MaxParamCount = 32000; // no doc ref, just posting on forum: https://stackoverflow.com/questions/6581573
      base.DynamicSqlParameterPrefix = "@P";
      base.DefaultLikeEscapeChar = '/';
      base.BatchBeginTransaction = new TextSqlFragment("START TRANSACTION;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;");
    }
    public override void InitTemplates() {
      base.InitTemplates();
      // UUID function returns string (new guid as string); we need to turn it into binary
      AddTemplate("UNHEX(REPLACE(UUID(),'-',''))", SqlFunctionType.NewGuid);
      AddTemplate("YEAR({0})", SqlFunctionType.Year);
      AddTemplate("MONTH({0})", SqlFunctionType.Month);
      AddTemplate("WEEK({0})", SqlFunctionType.Week);
      AddTemplate("DAY({0})", SqlFunctionType.Day);
      AddTemplate("DATE({0})", SqlFunctionType.Date);
      AddTemplate("{0} LIKE {1} ESCAPE '/'", SqlFunctionType.Like); // replace '\' -> '/'
      AddTemplate("CHAR_LENGTH({0})", SqlFunctionType.StringLength);
      AddTemplate("({0})", SqlFunctionType.ConvertBoolToBit);
    }


  }
}
