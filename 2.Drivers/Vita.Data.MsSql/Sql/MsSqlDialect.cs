using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Microsoft.SqlServer.Server;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities.Utilities;

namespace Vita.Data.MsSql {
  public class MsSqlDialect : DbSqlDialect {
    public SqlFragment WithUpdateLockHint = new TextSqlFragment(" WITH(UpdLock) ");
    public SqlFragment WithNoLockHint = new TextSqlFragment(" WITH(NOLOCK) ");
    public SqlTemplate TopTemplate = new SqlTemplate("TOP({0})");
    public SqlTemplate ConcatTemplate = new SqlTemplate("CONCAT({0})"); // for multiple args, > 2
    public SqlTemplate SqlGetIdentityTemplate = new SqlTemplate("SET {0} = SCOPE_IDENTITY();");
    public SqlTemplate SqlGetRowVersionTemplate = new SqlTemplate("SET {0} = @@DBTS;");
    public SqlTemplate SqlCheckRowCountIsOne = new SqlTemplate(
@"IF @@RowCount <> 1
      RAISERROR({0}, 11, 111);
");

    public MsSqlDbDriver Driver; 

    public MsSqlDialect(MsSqlDbDriver driver) : base(driver) {
      this.Driver = driver; 
      base.MaxParamCount = 2100;
      base.MaxRecordsInInsertMany = 500; //actual is 1000, but just to be careful
      base.DynamicSqlParameterPrefix = "@P";
      base.BatchBeginTransaction = new TextSqlFragment("BEGIN TRANSACTION;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT TRANSACTION;");
      base.DDLSeparator = Environment.NewLine + "GO" + Environment.NewLine;
      //Change Count() to COUNT_BIG - COUNT is not allowed inside views, so we change default to Count_BIG
      base.SqlCountStar = new TextSqlFragment("COUNT_BIG(*)");

      base.OffsetTemplate = new SqlTemplate("OFFSET {0} ROWS");
      base.OffsetLimitTemplate = new SqlTemplate("OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY");
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
      AddTemplate("UPPER({0})", SqlFunctionType.ToUpper);
      AddTemplate("LOWER({0})", SqlFunctionType.ToLower);
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

    public override IDbDataParameter AddDbParameter(IDbCommand command, SqlPlaceHolder ph, object value) {
      var parameter = base.AddDbParameter(command, ph, value); 
      // force DbType to datetime2
      switch (parameter.Direction) {
        case ParameterDirection.Input:
        case ParameterDirection.InputOutput:
          if(parameter.DbType == DbType.DateTime)
            parameter.DbType = DbType.DateTime2;
          break;
      } //switch direction
      return parameter; 
    }

    #region ReviewSqlStatement
    public override void ReviewSqlStatement(SqlStatement sql, object metaObject) {
      base.ReviewSqlStatement(sql, metaObject);
      // additional configuration for list placeholders
      foreach(var ph in sql.PlaceHolders) {
        switch(ph) {
          case SqlListParamPlaceHolder lph:
            lph.PreviewParameter = ConfigureListParameter; //sets up special properties of DbParameter
            var template = GetSelectFromListParamTemplate(lph.ElementType);
            lph.FormatParameter = (prm) => string.Format(template, prm.ParameterName);
            break;
            
          case SqlColumnValuePlaceHolder cph:
            // Special case for binary, varbinary nullable column/param, you should use VarBinary.Null
            //  instead of plain DbNull.Value. Otherwise a strange and stupid error is thrown: 
            //  no implicit conv from nvarchar to varbinary
            var col = cph.Column; 
            if (cph.ParamDirection == ParameterDirection.Input && 
                col.Flags.IsSet(DbColumnFlags.Nullable) && 
                col.TypeInfo.DbTypeSpec.Contains("binary")) {
              cph.PreviewParameter = (prm, ph) => {
                if(prm.Value == DBNull.Value)
                  prm.Value = System.Data.SqlTypes.SqlBinary.Null; 
              }; //lambda
            } //if
            break;            
        }
      }
    }

    private string GetSelectFromListParamTemplate(Type elemType) {
      if(elemType.IsInt())
        return "SELECT CAST(\"Value\" AS INT) FROM {0}";
      else if(elemType == typeof(Guid))
        return "SELECT CAST(\"Value\" AS UNIQUEIDENTIFIER) FROM {0}";
      else if(elemType == typeof(string))
        return "SELECT CAST(\"Value\" AS NVARCHAR) FROM {0}";
      else
        return "SELECT \"Value\" FROM {0}";
    }


    // Parameters containing lists need special setup
    private void ConfigureListParameter(IDbDataParameter prm, SqlPlaceHolder ph) {
      var sqlPrm = (SqlParameter)prm;
      // convert to list of SqlDataRecord
      sqlPrm.Value = ConvertListParameterValue(sqlPrm.Value, (SqlListParamPlaceHolder)ph);
      sqlPrm.SqlDbType = SqlDbType.Structured;
      var msDriver = (MsSqlDbDriver)this.Driver;
      sqlPrm.TypeName = msDriver.SystemSchema + "." + MsSqlDbDriver.ArrayAsTableTypeName;
      /*
      // Table-valued parameters cannot be DBNull; for empty list set it to null
      if(sqlPrm.Value == DBNull.Value)
        sqlPrm.Value = null;
        */
    }
    #endregion

    public List<SqlDataRecord> ConvertListParameterValue(object value, SqlListParamPlaceHolder lph) {
      var list = value as IList;
      if(list == null || list.Count == 0)
        return null; //it should null, not DbNull.Value
      bool isEnum = lph.ElementType.IsEnum;
      var records = new List<SqlDataRecord>();
      var rowMetaData = new SqlMetaData("Value", SqlDbType.Variant);
      foreach(object elem in list) {
        var rec = new SqlDataRecord(rowMetaData);
        var v1 = isEnum ? ConvertHelper.EnumValueToInt(elem, lph.ElementType) : elem;
        rec.SetValue(0, v1);
        records.Add(rec);
      }
      if(records.Count == 0)
        return null; // with 0 rows throws error, advising to send NULL
      return records;
    }


  }
}
