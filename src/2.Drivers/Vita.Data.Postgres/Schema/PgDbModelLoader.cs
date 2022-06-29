using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Driver.InfoSchema;
using Vita.Data.Driver.TypeSystem;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;

namespace Vita.Data.Postgres {
  public class PgDbModelLoader : DbModelLoader {
    public PgDbModelLoader(DbSettings settings, ILog log) : base(settings, log) {
      // Value identifying tables in information_schema.Tables view; for Postgres it is 'BASE TABLE'
      base.TableTypeTag = "BASE TABLE";
      // Value identifying routines in information_schema.Routines view
      base.RoutineTypeTag = "FUNCTION";
    }

    public override DbTypeInfo GetColumnDbTypeInfo(InfoRow columnRow) {
      // workaround for view columns (mat views esp)
      var dt = columnRow["data_type"];
      if(dt == DBNull.Value)
        columnRow["data_type"] = columnRow["pg_data_type"];

      var typeInfo =  base.GetColumnDbTypeInfo(columnRow);
      if(typeInfo != null && typeInfo.DbTypeSpec == "text") {
        typeInfo.Size = -1;
      }
      return typeInfo; 
    }

    public override InfoTable GetColumns() {
      //Postgres does not return mat views' columns in information_schema.columns. 
      // So we retrieve basic column info from raw pg tables/views; 
      // we also join information_schema.columns to be able to get some additional attributes (numeric scale, precision)
      // that are hard (impossible) to find in raw tables
      // note: is_generated is varchar field
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
  cl.numeric_scale as numeric_scale,
  cl.is_generated as is_generated,
  cl.generation_expression
FROM pg_class t
     JOIN pg_attribute a ON a.attrelid = t.oid
     JOIN pg_type tp ON a.atttypid = tp.oid
     JOIN pg_namespace n ON n.oid = t.relnamespace
     LEFT OUTER JOIN information_schema.columns cl 
          ON n.nspname = cl.table_schema AND t.relname = cl.table_name AND a.attname = cl.Column_name
   WHERE 
         a.attnum > 0
         AND t.relkind in ('r', 'v', 'm')
         AND {0} 
ORDER BY table_schema, table_name, ordinal_position;
", filter);
      // t.RelKind values: m: mat view; r:table; v:view; i: index
      // !!! Note: view columns are too much trouble, we do not load them - we do not need them that much
      var colData = ExecuteSelect(sql); 
      return colData; 
    }

    public override void OnColumnLoaded(DbColumnInfo column, InfoRow columnRow) {
      base.OnColumnLoaded(column, columnRow);
      var isGen = columnRow.GetAsString("is_generated");
      if (isGen != "ALWAYS")
        return;
      column.ComputedKind = DbComputedKindExt.StoredColumn; // pg does not have virtual cols, only STORED
      column.ComputedAsExpression = columnRow.GetAsString("generation_expression");
    }


    // Postgres does not save original view definition. The SQL returned in View_definition column 
    // in information_schema.views is very much modified and refactored version of original script,
    // so it is pretty much useless for comparison (to detect if view needs to be upgraded)
    // So we use this only to load list of views to know which to drop when we reset the db in unit tests. 
    public override InfoTable GetViews() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(
@"SELECT TABLE_SCHEMA, TABLE_NAME, VIEW_DEFINITION, 'N' AS IS_MATERIALIZED
      FROM INFORMATION_SCHEMA.VIEWS
      WHERE {0};", filter);
      var dtViews = ExecuteSelect(sql);
      var dtMatViews = GetMaterializedViews();
      // just copy rows
      foreach(var row in dtMatViews.Rows)
        dtViews.Rows.Add(row); 
      return dtViews;
    }

    // Postgres does not return materialized views in information_schema.views - they say it's intentional (and stupid!)
    // so we have to use hand-crafted query; 
    private InfoTable GetMaterializedViews() {
      var filter = GetSchemaFilter("n.nspname");
      var sql = string.Format(@"
SELECT n.nspname AS TABLE_SCHEMA, c.relname as TABLE_NAME,  
  c.relkind as VIEW_KIND, 'Y' as IS_MATERIALIZED, '' as VIEW_DEFINITION
FROM pg_catalog.pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT OUTER JOIN pg_description d on d.objoid = c.oid
WHERE (c.relkind = 'm') AND {0};", filter);
      var dt = ExecuteSelect(sql);
      return dt; 
    }

    // Expected columns: table_schema, table_name, index_name, primary_key, clustered, unique, disabled 
    public override InfoTable GetIndexes() {
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

    // Output DbTable expected columns: table_schema, table_name, index_name, column_name, column_ordinal_position, is_descending
    public override InfoTable GetIndexColumns() {
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
    AND t.relkind IN('v', 'm', 'r', 'i') -- m: mat view; r:table; v:view; i: index
    AND {0}
ORDER BY table_schema, table_name, index_name, column_ordinal_position    
", filter);
      var colData = ExecuteSelect(sql);
      // our query returns a flag value from indoption array; this value is 3 for DESC column; 
      // change it to 1 as caller method expects
      foreach(InfoRow row in colData.Rows) {
        if (row.GetAsInt("is_descending") == 3)
          row["is_descending"] = 1;
      }//foreach indRow
      return colData;
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
