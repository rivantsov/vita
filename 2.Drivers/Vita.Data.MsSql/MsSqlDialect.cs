using System;
using System.Collections.Generic;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.SqlGen;

namespace Vita.Data.MsSql {
  public class MsSqlDialect : DbSqlDialect {
    public SqlFragment WithUpdateLockHint = new TextSqlFragment(" WITH(UpdLock) ");
    public SqlFragment WithNoLockHint = new TextSqlFragment(" WITH(NOLOCK) ");
    public SqlTemplate OffsetLimitTemplate = new SqlTemplate("OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY");
    public SqlTemplate OffsetTemplate = new SqlTemplate("OFFSET {0} ROWS");
    public SqlTemplate TopTemplate = new SqlTemplate("TOP({0})");
    public SqlTemplate ConcatTemplate = new SqlTemplate("CONCAT({0})"); // for multiple args, > 2
    public SqlTemplate InArrayTemplateUntyped = new SqlTemplate("({0} IN (SELECT \"Value\" FROM {1}))");
    public SqlTemplate InArrayTemplateTyped = new SqlTemplate("({0} IN (SELECT CAST(\"Value\" AS {1} ) FROM {2}))");
    public SqlTemplate SqlGetIdentityTemplate = new SqlTemplate("SET {0} = SCOPE_IDENTITY();");
    public SqlTemplate SqlGetRowVersionTemplate = new SqlTemplate("SET {0} = @@DBTS;");


    public MsSqlDialect(MsSqlDbDriver driver) : base(driver) {
      base.MaxParamCount = 2100; 
      base.DynamicSqlParameterPrefix = "@P";
      base.BatchBeginTransaction = new TextSqlFragment("BEGIN TRANSACTION;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT TRANSACTION;");
      base.DDLSeparator = Environment.NewLine + "GO" + Environment.NewLine;
      //Change Count() to COUNT_BIG - COUNT is not allowed inside views, so we change default to Count_BIG
      base.SqlCountStar = new TextSqlFragment("COUNT_BIG(*)");
    }

    public override void InitTemplates() {
      base.InitTemplates();

      AggregateTemplates[AggregateType.Count] = new SqlTemplate("COUNT_BIG({0})");

      // Some custom functions
      AddTemplate("IIF({0}, {1}, {2})", SqlFunctionType.Iif);
      AddTemplate("LEN({0})", SqlFunctionType.StringLength);
      AddTemplate("NewId()", SqlFunctionType.NewGuid);
      AddTemplate("IIF({0}, 1, 0)", SqlFunctionType.ConvertBoolToBit);

      //AddTemplate("CHARACTER_LENGTH({0})", SqlFunctionType.StringLength);
      AddTemplate("LEN({0})", SqlFunctionType.StringLength);
      AddTemplate("UCASE({0})", SqlFunctionType.ToUpper);
      AddTemplate("LCASE({0})", SqlFunctionType.ToLower);
      AddTemplate("TRIM({0})", SqlFunctionType.Trim);
      AddTemplate("LTRIM({0})", SqlFunctionType.LTrim);
      AddTemplate("RTRIM({0})", SqlFunctionType.RTrim);
      AddTemplate("SUBSTR({0}, {1}, {2})", SqlFunctionType.Substring);
      AddTemplate("NewId()", SqlFunctionType.NewGuid);
      AddTemplate("POWER({0}, {1})", System.Linq.Expressions.ExpressionType.Power);
      AddTemplate("DATEPART(YEAR,{0})", SqlFunctionType.Year);
      AddTemplate("DATEPART(MONTH,{0})", SqlFunctionType.Month);
      AddTemplate("DATEPART(DAY,{0})", SqlFunctionType.Day);
      AddTemplate("DATEPART(HOUR,{0})", SqlFunctionType.Hour);
      AddTemplate("DATEPART(MINUTE,{0})", SqlFunctionType.Minute);
      AddTemplate("DATEPART(SECOND,{0})", SqlFunctionType.Second);
      AddTemplate("CONVERT(DATE, {0})", SqlFunctionType.Date);
      AddTemplate("CONVERT(TIME, {0})", SqlFunctionType.Time);
      AddTemplate("DATEPART(ISOWK,{0})", SqlFunctionType.Week);

      AddTemplate("NEXT VALUE FOR {0}", SqlFunctionType.SequenceNextValue);
    }


    // We use COUNT_BIG so return type is long
    public override Type GetAggregateResultType(AggregateType aggregateType, Type[] opTypes) {
      switch(aggregateType) {
        case AggregateType.Count:
          return typeof(long);
      }
      return base.GetAggregateResultType(aggregateType, opTypes);
    }


  }
}
