using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging; 

namespace Vita.Data.Postgres {
  public class PgDbModelLoader : DbModelLoader {
    public PgDbModelLoader(DbSettings settings, MemoryLog log) : base(settings, log) {
      // Value identifying tables in information_schema.Tables view; for Postgres it is 'BASE TABLE'
      base.TableTypeTag = "BASE TABLE";
      // Value identifying routines in information_schema.Routines view
      base.RoutineTypeTag = "FUNCTION";
    }

    public override DbTypeInfo GetDbTypeInfo(DataRow columnRow) {
      var typeInfo =  base.GetDbTypeInfo(columnRow);
      if(typeInfo.SqlTypeSpec == "text") {
        typeInfo.Size = -1;
      }
      return typeInfo; 
    }
    public override DataTable GetColumns() {
      //Postgres does not return mat views' columns in information_schema.columns. 
      // So we retrieve basic column info from raw pg tables/views; 
      // we also join information_schema.columns to be able to get some additional attributes (numeric scale, precision)
      // that are hard (impossible) to find in raw tables
      var filter = GetSchemaFilter("n.nspname");
      var sql = string.Format(@"
SELECT n.nspname AS table_schema, t.relname AS table_name,  a.attname AS column_name,
  a.attnum AS ordinal_position, 
  cl.data_type AS data_type, 
  tp.typname AS pg_data_type, 
  cl.character_maximum_length AS character_maximum_length,
  cl.character_octet_length AS character_octet_length,
  cl.column_default as column_default,
  cl.is_nullable as is_nullable,
  cl.numeric_precision as numeric_precision,
  cl.numeric_scale as numeric_scale
FROM pg_class t
     JOIN pg_attribute a ON a.attrelid = t.oid
     JOIN pg_type tp ON a.atttypid = tp.oid
     JOIN pg_namespace n ON n.oid = t.relnamespace
     LEFT OUTER JOIN information_schema.columns cl 
          ON n.nspname = cl.table_schema AND t.relname = cl.table_name AND a.attname = cl.Column_name
   WHERE 
         a.attnum > 0
         AND t.relkind IN('v', 'm', 'r') -- m: mat view; r:table; v:view; i: index
         AND {0} 
ORDER BY table_schema, table_name, ordinal_position;
", filter);
      var colData = ExecuteSelect(sql); 
      foreach(DataRow row in colData.Rows) {
        // for mat views data_type coming from outer join might end up empty; fill it in with default; 
        //  exact type does not matter, we only use col names in view
        var dataType = row.GetAsString("DATA_TYPE");
        if(string.IsNullOrWhiteSpace(dataType))
          row["DATA_TYPE"] = "character varying";
      }
      return colData; 
    }

    // Postgres does not return materialized views in information_schema.views - they say it's intentional (and stupid!)
    // so we have to use hand-crafted query; another trouble - Postgres reformats the view SQL substantially - 
    // the SQL returned as View definition differs from original CREATE VIEW sql. One annoying (and stupid!!!!) thing is that it removes all comments 
    // from View. 
    // For other servers we keep view hash in special comment.So for postgres we save hash in view description (special attribute in Postgres),
    // and for loading views construct artificial definition consisting only of hash comment line
    public override DataTable GetViews() {
      var filter = GetSchemaFilter("n.nspname");
      var sql = string.Format(@"
SELECT n.nspname AS TABLE_SCHEMA, c.relname as TABLE_NAME,  
  c.relkind as VIEW_KIND, ' ' as IS_MATERIALIZED, ('{1}' || d.Description || '*') as VIEW_DEFINITION
FROM pg_catalog.pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT OUTER JOIN pg_description d on d.objoid = c.oid
WHERE (c.relkind = 'm' OR c.relkind = 'v') AND {0};", filter, SqlSourceHasher.HashPrefix);
      var dt = ExecuteSelect(sql);
      // set Is_materialized column
      foreach (DataRow row in dt.Rows) {
        var kind = row.GetAsString("VIEW_KIND");
        if (kind == "m")
          row["IS_MATERIALIZED"] = "Y";
      }
      return dt; 
    }

    public override DataTable GetRoutines() {
      var filter = GetSchemaFilter("n.nspname");
      var sql = string.Format(@"
SELECT p.proname AS ROUTINE_NAME,
       n.nspname AS ROUTINE_SCHEMA,
       p.prosrc AS ROUTINE_DEFINITION,
       'FUNCTION' AS ROUTINE_TYPE,       
       pg_get_function_identity_arguments(p.oid) AS ROUTINE_CUSTOM
  FROM   pg_proc p join pg_namespace n on p.pronamespace = n.oid
  WHERE {0};", filter);
      return ExecuteSelect(sql);
    }

    // Expected columns: table_schema, table_name, index_name, primary_key, clustered, unique, disabled 
    public override DataTable GetIndexes() {
      var filter = GetSchemaFilter("n.nspname");
      var sql = string.Format(@"
SELECT
     n.nspname  as table_schema
    ,t.relname  as table_name
    ,a.relname  as index_name
    ,i.indisprimary as primary_key
    ,i.indisclustered as clustered
    ,i.indisunique as unique
    , '' as FILTER_CONDITION
    , pg_get_indexdef(a.oid) as index_def 
FROM pg_class a
    JOIN pg_namespace n ON n.oid  = a.relnamespace
    JOIN pg_catalog.pg_index i ON i.indexrelid = a.oid
    JOIN pg_catalog.pg_class t ON i.indrelid   = t.oid
WHERE a.relkind = 'i'
    AND n.nspname not in ('pg_catalog', 'pg_toast')
    AND {0}
ORDER BY n.nspname,t.relname,a.relname;", filter);
      return ExecuteSelect(sql);
    }

    // We use index definition string to get columns and DESC flags for each index. GetIndexes call returns index definition in a separate column.
    // Just did not find any other way to get columns with IsDescending flag. 
    // Output datatable expected columns: table_schema, table_name, index_name, column_name, column_ordinal_position, is_descending
    public override DataTable GetIndexColumns() {
      var filter = GetSchemaFilter("n.nspname");
      var sql = string.Format(@"
CREATE EXTENSION IF NOT EXISTS intarray;
SELECT n.nspname TABLE_SCHEMA, t.relname TABLE_NAME, i.relname as INDEX_NAME, a.attname COLUMN_NAME, 
       ix.indKey#a.attNum as column_ordinal_position,
       ix.indoption[(ix.indKey#a.attNum)-1] AS is_descending
FROM  pg_class t
      JOIN pg_index ix ON t.oid = ix.indrelid
      JOIN pg_class i ON i.oid = ix.indexrelid
      JOIN pg_attribute a ON a.attrelid = t.oid
      JOIN pg_catalog.pg_namespace n ON n.oid = t.relnamespace
where
    a.attnum = ANY(ix.indkey)
    AND t.relkind IN('v', 'm', 'r') -- m: mat view; r:table; v:view; i: index
    AND {0}
ORDER BY table_schema, table_name, index_name, column_ordinal_position    
", filter);
      var colData = ExecuteSelect(sql);
      // our query returns a flag value from indoption array; this value is 3 for DESC column; 
      // change it to 1 as caller method expects
      foreach(DataRow row in colData.Rows) {
        if (row.GetAsInt("is_descending") == 3)
          row["is_descending"] = 1;
      }//foreach indRow
      return colData;
    }

    class IndexColumn {
      public string ColumnName;
      public bool IsDescending;
    }
    // sample index def: 
    // CREATE INDEX "IX_LastNameFirstName" ON books."Author" USING btree ("FirstName" DESC, "LastName")
    private List<IndexColumn> ParseIndexDef(string indexDef) { 
      var list = new List<IndexColumn>();
      var p1 = indexDef.IndexOf('(');
      var p2 = indexDef.IndexOf(')');
      var colsStr = indexDef.Substring(p1 + 1, p2 - p1 - 1);
      var cols = colsStr.Split(',');
      foreach(var col in cols) {
        var colParts = col.Trim().Split(' ');
        var colName = colParts[0];
        var colNameNoQuoute = colName.Substring(1, colName.Length - 2);
        bool isDesc = colParts.Length > 1 && colParts[1].ToLowerInvariant() == "desc";
        list.Add(new IndexColumn() { ColumnName = colNameNoQuoute, IsDescending = isDesc });
      }
      return list;
    }

    /* The following query will bring index columns, but there are 2 problems. 1 - column order in index has to be derived from indKey field 
     * (list of numbers which point to attnum values). 2 - it does not bring ASC/DESC info for columns, and I could not find a way to get it. 
     * I gave up on this query, and parsing index definition instead.
     Here's not so working query:
        const string template = @"
    select  n.nspname  as table_schema, t.relname as table_name,  i.relname as index_name,  a.attname as column_name,  a.attnum, ix.indkey
    from  pg_namespace n, pg_class t,  pg_class i, pg_index ix,  pg_attribute a
    where   n.oid = i.relnamespace  and t.oid = ix.indrelid and i.oid = ix.indexrelid and a.attrelid = t.oid
        and a.attnum = ANY(ix.indkey) and t.relkind = 'r' and i.relkind = 'i'
       {0}
    order by n.nspname, t.relname,i.relname;";
      */


  }
}
