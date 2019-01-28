using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Driver.TypeSystem;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Data.Sql;

namespace Vita.Data.Oracle {
  public class OracleSqlDialect : DbSqlDialect {
    public TextSqlFragment ConcatOperator = new TextSqlFragment("||");
    public SqlTemplate SqlReturnIdentityTemplate = new SqlTemplate("RETURNING {0} INTO {1};");
    public SqlFragment SqlTermLockForUpdate = new TextSqlFragment(" FOR UPDATE");
    public SqlFragment SqlFromDual = new TextSqlFragment("FROM dual"); //fake From clause

    public OracleSqlDialect(OracleDbDriver driver) : base(driver) {
      base.DynamicSqlParameterPrefix = ":p";
      base.OffsetLimitTemplate = new SqlTemplate("OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY");
      base.OffsetTemplate = new SqlTemplate("OFFSET {0} ROWS");
      base.BatchBegin = new TextSqlFragment("BEGIN \r\n");
      base.BatchEnd = new TextSqlFragment("END;");
      base.BatchBeginTransaction = SqlTerms.Empty;
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;\r\n");
      base.LikeWildCardChars = new char[] { '_', '%', '\\' }; // [ and ] are not wildcards in Oracle
    }

  public override string GetTableExistsSql(DbTableInfo table) {
      var sql = $@"
SELECT Table_name 
FROM all_tables
WHERE owner='{table.Schema}' AND table_name = '{table.TableName}'
";
      return sql; 
    }

    public override void InitTemplates() {
      base.InitTemplates();
      AddTemplate("LENGTH({0})", SqlFunctionType.StringLength);
      AddTemplate("UPPER({0})", SqlFunctionType.ToUpper);
      AddTemplate("LOWER({0})", SqlFunctionType.ToLower);

      AddTemplate("BitAnd({0}, {1})", SqlFunctionType.AndBitwise);
      AddTemplate("BitOr({0}, {1})", SqlFunctionType.OrBitwise);
      AddTemplate("ROUND({0}, {1})", SqlFunctionType.Round);

      AddTemplate("TRUNC({0})", SqlFunctionType.Date);
      AddTemplate("EXTRACT(YEAR FROM {0})", SqlFunctionType.Year);
      AddTemplate("EXTRACT(MONTH FROM {0})", SqlFunctionType.Month);
      AddTemplate("EXTRACT(DAY FROM {0})", SqlFunctionType.Day);

      AddTemplate("SYS_GUID()", SqlFunctionType.NewGuid);
      AddTemplate("(CASE WHEN {0} THEN {1} ELSE {2} END)", SqlFunctionType.Iif);
      AddTemplate("(CASE WHEN {0} THEN 1 ELSE 0 END)", SqlFunctionType.ConvertBoolToBit);

      /*
      // Oracle does not allow EXISTS in output clause; 
      // attempt to replace with (SELECT Count>0 FROM <subquery> Fetch 1) - does not work, investigate this further
      AddTemplate(@"(
SELECT (COUNT (*) > 0) 
    FROM {0}
    FETCH NEXT 1 ROWS ONLY)", SqlFunctionType.Exists);
    */

    }

    // Used in 't.Col IN (<list>)' expressions when list is empty; does not work for all providers
    public override string GetEmptyListLiteral(DbTypeDef elemTypeDef) {
      return "SELECT NULL FROM dual WHERE 1=0"; 
    }


  } //class
} //ns
