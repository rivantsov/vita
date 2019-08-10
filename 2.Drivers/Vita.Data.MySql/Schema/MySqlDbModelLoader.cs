using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Logging;
using Vita.Data.Driver.InfoSchema;
using Vita.Data.Driver.TypeSystem;

namespace Vita.Data.MySql {
  class MySqlDbModelLoader : DbModelLoader {
    public MySqlDbModelLoader(DbSettings settings, ILog log) : base(settings, log) {
      base.TableTypeTag = "BASE TABLE";
      base.RoutineTypeTag = "PROCEDURE";
    }

    //INFORMATION_SCHEMA does not have a view for indexes, so we have to do it through MSSQL special objects
    public override InfoTable GetIndexes() {
      const string getIndexesTemplate = @"
SELECT DISTINCT TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, (0=1) AS PRIMARY_KEY, '' AS CLUSTERED, 
  (-NON_UNIQUE + 1) AS ""UNIQUE"", 0 AS DISABLED, '' as FILTER_CONDITION  
  from information_schema.STATISTICS
where
  INDEX_NAME != 'Primary' AND {0}
ORDER BY TABLE_SCHEMA, TABLE_NAME, INDEX_NAME
";
      var filter = GetSchemaFilter("TABLE_SCHEMA"); 
      var sql = string.Format(getIndexesTemplate, filter);
      return ExecuteSelect(sql);
    }

    public override InfoTable GetIndexColumns() {
      const string getIndexColumnsTemplate = @"
SELECT TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, COLUMN_NAME, 
  SEQ_IN_INDEX AS COLUMN_ORDINAL_POSITION, 0 AS IS_DESCENDING
  FROM information_schema.STATISTICS
where
  INDEX_NAME != 'Primary' AND {0}
ORDER BY TABLE_SCHEMA, TABLE_NAME, INDEX_NAME, COLUMN_ORDINAL_POSITION
";
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(getIndexColumnsTemplate, filter);
      return ExecuteSelect(sql);
    }

    public override DbTypeInfo GetColumnDbTypeInfo(InfoRow columnRow) {
      // For 'unsigned' types, Data_type column does not show this attribute, but Column_type does. 
      // We search matching by data_type column (and we register names with 'unsigned' included, like 'int unsigned'). 
      // So we just append the unsigned to data_type value, so lookup works. 
      var columnType = columnRow.GetAsString("COLUMN_TYPE").ToLowerInvariant();
      if(columnType.EndsWith(" unsigned")) {
        columnRow["DATA_TYPE"] += " unsigned";
      }
      //auto-set memo
      var dbTypeInfo = base.GetColumnDbTypeInfo(columnRow);
      if(dbTypeInfo != null && dbTypeInfo.ClrType == typeof(string) && dbTypeInfo.Size > 100 * 1000)
        dbTypeInfo.Size = -1;
      return dbTypeInfo; 
    }

    public override void OnModelLoaded() {
      base.OnModelLoaded();
      LoadIdentityColumnsInfo();
      //FixGuidColumnsTypeDefs();
    }

    private void LoadIdentityColumnsInfo() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(@"
SELECT table_schema, table_name, column_name
  FROM INFORMATION_SCHEMA.Columns 
  WHERE EXTRA = 'auto_increment' AND {0};", filter);
      var data = ExecuteSelect(sql);
      foreach(InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if(!base.IncludeSchema(schema))
          continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var colName = row.GetAsString("COLUMN_NAME");
        var table = Model.GetTable(schema, tableName);
        if(table == null) continue;
        var colInfo = table.Columns.FirstOrDefault(c => c.ColumnName == colName);
        if(colInfo != null)
          colInfo.Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      }
    }

    // In MySql constraint name is not globally unique, it is unique only in scope of the table. 
    // We have to add matching by table names in joins
    public override InfoTable GetReferentialConstraints() {
      var filter = GetSchemaFilter("tc1.CONSTRAINT_SCHEMA");
      var sql = string.Format(@"
SELECT rc.*, tc1.TABLE_NAME as C_TABLE, tc2.TABLE_NAME AS U_TABLE 
  FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc1 
    ON tc1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA AND 
       tc1.TABLE_NAME = rc.TABLE_NAME AND 
       tc1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc2 
    ON tc2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA AND 
       tc2.TABLE_NAME = rc.REFERENCED_TABLE_NAME AND 
       tc2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME
  WHERE {0};
", filter);
      return ExecuteSelect(sql);
    }

    protected override void LoadViews() {
      base.LoadViews();
      /*
      foreach (var t in Model.Tables) {
        if (t.Kind == EntityKind.View && t.ViewHash == null)
          LoadViewHashFromFormFile(t); 
      }
      */
    }

    /*
     Information_Schema.Views view returns completely reformatted/changed SQL for view.
     Example. Original view: 

SELECT  a$."FirstName" AS "FirstName", a$."LastName" AS "LastName", t0$."UserName" AS "UserName", t0$."Type" AS "UserType"
FROM "books"."Author" a$
     LEFT JOIN "books"."User" t0$ ON t0$."Id" = a$."User_Id";

     Returned by InformationSchema.Views: 

select `a$`.`FirstName` AS `FirstName`,`a$`.`LastName` AS `LastName`,`t0$`.`UserName` AS `UserName`,`t0$`.`Type` AS `UserType` 
from (`books`.`author` `a$` left join `books`.`user` `t0$` on((`t0$`.`Id` = `a$`.`User_Id`)))

      (linebreak added for readablility)

     */
    static char[] _viewCharsToIgnore = new[] { '\r', '\n', '`', '"', '(', ')', ';' };

    protected override string NormalizeViewScript(string script) {
      if(string.IsNullOrEmpty(script))
        return script;
      script = base.NormalizeViewScript(script); 
      // my sql strips INNER, replaces Count(*) with (0)
      script = script.Replace("INNER JOIN", "JOIN").Replace("(*)", "(0)");
      //remove newlines, backquotes, double quotes
      script = ReplaceChars(script, _viewCharsToIgnore, ' ');
      script = script.Replace(" ", string.Empty).ToLowerInvariant(); //remove all spaces
      return script;
    }

    private static string ReplaceChars(string s, char[] chars, char withChar) {
      return new string(s.Select(c => chars.Contains(c) ? withChar : c).ToArray());

    }

  }
}
