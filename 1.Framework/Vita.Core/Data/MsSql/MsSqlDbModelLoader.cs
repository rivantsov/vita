using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Runtime;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Services;

namespace Vita.Data.MsSql {

  public class MsSqlDbModelLoader : DbModelLoader {
    public MsSqlDbModelLoader(DbSettings settings, SystemLog log) : base(settings, log) {
      base.TableTypeTag = "BASE TABLE";
      base.RoutineTypeTag = "PROCEDURE";
    }

    //Columns:CATALOG_NAME	SCHEMA_NAME	SCHEMA_OWNER	DEFAULT_CHARACTER_SET_CATALOG	DEFAULT_CHARACTER_SET_SCHEMA 
    //	DEFAULT_CHARACTER_SET_NAME  
    public override DbTable GetSchemas() {
      var filter = GetSchemaFilter("SCHEMA_NAME");
      var sql = string.Format(
@"SELECT * FROM INFORMATION_SCHEMA.SCHEMATA 
WHERE SCHEMA_NAME NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest') 
AND {0};",   filter);
      return ExecuteSelect(sql);
    }


    //SQL Server INFORMATION_SCHEMA.ROUTINES view defines ROUTINE_DEFINITION column (proc body) as nvarchar(4000), 
    // so it is truncated for long SPs. We use a workaround to get full text
    public override DbTable GetRoutines() {
      var filter = GetSchemaFilter("R.ROUTINE_SCHEMA");
      var sql = string.Format(@"SELECT R.ROUTINE_SCHEMA, R.ROUTINE_NAME, 
       OBJECT_DEFINITION(OBJECT_ID(R.ROUTINE_SCHEMA + '.' + R.Routine_Name)) as ROUTINE_DEFINITION,
       r.ROUTINE_DEFINITION as ROUTINE_DEFINITION_TRUNCATED
FROM INFORMATION_SCHEMA.ROUTINES R
WHERE R.ROUTINE_TYPE='PROCEDURE' AND {0}", filter);
      return ExecuteSelect(sql);
    }

    //INFORMATION_SCHEMA does not have a view for indexes, so we have to do it through MSSQL special objects
    public override DbTable GetIndexes() {
      var filter = GetSchemaFilter("ss.name");
      //Slightly adjusting column names to match SQL CE Indexes view
      var sql = string.Format(@"
SELECT ss.name AS table_schema, st.name AS table_name, si.name AS index_name, 
       si.is_primary_key as [primary_key],  
       CASE si.type_desc 
         WHEN 'CLUSTERED'THEN 1
         ELSE 0
       END as [clustered], 
       si.is_unique as [unique], 
       si.filter_definition as FILTER_CONDITION
FROM   sys.indexes AS si INNER JOIN
       sys.objects AS st ON si.object_id = st.object_id INNER JOIN
       sys.schemas AS ss ON ss.schema_id = st.schema_id
WHERE (si.name IS NOT NULL) AND {0}
ORDER BY table_schema, table_name, index_name;", filter);
      return ExecuteSelect(sql);
    }

    public override DbTable GetIndexColumns() {
      var filter = GetSchemaFilter("ss.name");
      var sql = string.Format(@"
SELECT ss.name AS table_schema, st.name AS table_name, si.name AS index_name, 
       sc.name AS column_name, sic.key_ordinal as column_ordinal_position, 
       sic.is_descending_key as is_descending
FROM   sys.indexes AS si INNER JOIN
       sys.index_columns AS sic ON si.object_id = sic.object_id AND si.index_id = sic.index_id INNER JOIN
       sys.columns AS sc ON si.object_id = sc.object_id AND sc.column_id = sic.column_id INNER JOIN
       sys.objects AS st ON si.object_id = st.object_id INNER JOIN
       sys.schemas AS ss ON ss.schema_id = st.schema_id
WHERE {0}
ORDER BY table_schema, table_name, index_name, column_ordinal_position; ", filter);
      return ExecuteSelect(sql);
    }

    // MS SQL stores View definition with 'CREATE VIEW (name) AS ' header. We need to cut it off
    public override DbTable GetViews() {
      string col_def = "VIEW_DEFINITION";
      var views = base.GetViews();
      foreach(DbRow row in views.Rows) {
        var sql = row.GetAsString(col_def);
        var isMatView = sql != null && sql.Contains("WITH SCHEMABINDING");
        row["IS_MATERIALIZED"] = isMatView? "Y" : "N";
        row[col_def] = CutOffViewHeader(sql);
      }
      return views; 
    }

    private string CutOffViewHeader(string viewDef) {
      var nlPos = viewDef.IndexOf('\n');
      var result = viewDef.Substring(nlPos + 1).Trim();
      return result; 

    }

    public override void OnModelLoaded() {
      LoadIdentityColumnsInfo();
      LoadDefaultConstraintNames();
    }

    public override DbTable GetUserDefinedTypes() {
      var sql = @"
Select s.[name] AS TYPE_SCHEMA, tt.[Name] AS TYPE_NAME  
From sys.table_types tt 
   INNER JOIN sys.schemas s on tt.schema_id = s.schema_id; ";
      var data = ExecuteSelect(sql);
      return data;
    }

    private void LoadIdentityColumnsInfo() {
      var filter = GetSchemaFilter("s.name");
      var sql = string.Format(@"
SELECT s.name AS table_schema, t.name AS table_name, c.name AS column_name 
  FROM sys.columns c 
  INNER JOIN sys.tables t ON t.object_id = c.object_id
  INNER JOIN sys.schemas s ON s.schema_id = t.schema_id 
  WHERE c.is_identity = 1 AND {0};", filter);
      var data = ExecuteSelect(sql);
      foreach(DbRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        var tableName = row.GetAsString("TABLE_NAME");
        var colName = row.GetAsString("COLUMN_NAME");
        var table = Model.GetTable(schema, tableName);
        if(table == null) continue;
        var colInfo = table.Columns.FirstOrDefault(c => c.ColumnName == colName);
        if(colInfo != null)
          colInfo.Flags |= DbColumnFlags.Identity | DbColumnFlags.NoUpdate | DbColumnFlags.NoInsert;
      }
    }

    //Used only when deleting columns - MS SQL has annoying thing that if column has a default, then you have 
    // to delete default constraint before deleting a column. And to delete constraint you have know its name
    private void LoadDefaultConstraintNames() {
      const string sqlGetConstraints = @"
WITH Defaults AS (
  SELECT D.Name as ConstraintName,
       SCHEMA_NAME(D.schema_id) as TABLE_SCHEMA, 
       OBJECT_NAME(D.parent_object_id) AS TABLE_NAME, 
       C.Name as COLUMN_NAME, 
       D.Definition as DefaultExpression
FROM sys.default_constraints D INNER JOIN sys.columns C 
  ON D.parent_column_id = C.column_id AND D.parent_object_id = c.object_id
)
SELECT * From Defaults   
WHERE {0};
";
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(sqlGetConstraints, filter);
      var data = ExecuteSelect(sql);
      foreach(DbRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if(!base.IncludeSchema(schema))
          continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var colName = row.GetAsString("COLUMN_NAME");
        var table = Model.GetTable(schema, tableName);
        if(table == null) continue;
        var colInfo = table.Columns.FirstOrDefault(c => c.ColumnName == colName);
        if(colInfo != null)
          colInfo.DefaultConstraintName = row.GetAsString("ConstraintName");
        //DefaultExpression is loaded when we load column
      }
    }//method

  }//class

}
