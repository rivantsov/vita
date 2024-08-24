using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Sql;

namespace Vita.Data.MySql {

  public class MySqlDialect : DbSqlDialect {
    public SqlFragment SqlTermLockForUpdate = new TextSqlFragment(" FOR UPDATE");
    public SqlFragment SqlTermLockInShareMode = new TextSqlFragment(" LOCK IN SHARE MODE");
    public SqlFragment SqlSelectIdentity = new TextSqlFragment("SELECT LAST_INSERT_ID();");


    public MySqlDialect(MySqlDbDriver driver) : base(driver) {
      base.MaxParamCount = 32000; // no doc ref, just posting on forum: https://stackoverflow.com/questions/6581573
      // MySql does not have syntax for offset-only, so we set limit to 10 million recs
      base.OffsetTemplate = new SqlTemplate(" LIMIT 10000000 OFFSET {0} ");
      base.OffsetLimitTemplate = new SqlTemplate(" LIMIT {1} OFFSET {0} ");
    }

    public override void InitTemplates() {
      //we put these props here and not in the constructor, before base.InitTemplates and other templates, 
      // because we need to change LikeEscapeChar and use it in LIKE template. 
      // But InitTemplates is called by base constructor, and changing escape char in constructor is too late
      base.DynamicSqlParameterPrefix = "@P";
      base.BatchBeginTransaction = new TextSqlFragment("START TRANSACTION;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;");
      // MySql uses / as escape symbol in 
      base.LikeEscapeChar = '/';
      base.LikeWildCardChars = new char[] { '_', '%', '[', ']', LikeEscapeChar };

      base.InitTemplates();

      // UUID function returns string (new guid as string); we need to turn it into binary
      AddTemplate("UNHEX(REPLACE(UUID(),'-',''))", SqlFunctionType.NewGuid);
      AddTemplate("YEAR({0})", SqlFunctionType.Year);
      AddTemplate("MONTH({0})", SqlFunctionType.Month);
      AddTemplate("WEEK({0})", SqlFunctionType.Week);
      AddTemplate("DAY({0})", SqlFunctionType.Day);
      AddTemplate("DATE({0})", SqlFunctionType.Date);
      AddTemplate("TIME({0})", SqlFunctionType.Time);
      AddTemplate("HOUR({0})", SqlFunctionType.Hour);
      AddTemplate("MINUTE({0})", SqlFunctionType.Minute);

      var likeTemplate = "{0} LIKE {1} ESCAPE '" + LikeEscapeChar + "'";
      AddTemplate(likeTemplate, SqlFunctionType.Like); // replace '\' -> '/'
      AddTemplate("CHAR_LENGTH({0})", SqlFunctionType.StringLength);
      AddTemplate("({0})", SqlFunctionType.ConvertBoolToBit);

      AddTemplate("UPPER({0})", SqlFunctionType.ToUpper);
      AddTemplate("LOWER({0})", SqlFunctionType.ToLower);
      AddTemplate("LENGTH({0})", SqlFunctionType.StringLength);
    }

    public override IDbDataParameter AddDbParameter(IDbCommand command, SqlPlaceHolder ph, object value) {
      if (value != null && value != DBNull.Value) {
        var vt = value.GetType();
        //MySql does not like too precise timespan values, so we rough it to milliseconds
        if(vt == typeof(TimeSpan) || vt == typeof(TimeSpan?)) {
          var ts = (TimeSpan)value;
          value = new TimeSpan(ts.Hours, ts.Minutes, ts.Seconds);
        }

      }
      return base.AddDbParameter(command, ph, value);
    }

  } //class
}
