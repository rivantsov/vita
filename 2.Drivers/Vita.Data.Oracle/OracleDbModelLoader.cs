using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Vita.Data.Driver;
using Vita.Data.Driver.InfoSchema;
using Vita.Data.Driver.TypeSystem;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Data.Oracle {
  public class OracleDbModelLoader : DbModelLoader {
    string _tableNamesList;
    string _viewNamesList;
    string _sequenceNamesList;
    OracleDbTypeRegistry _oracleTypes;

    public OracleDbModelLoader(DbSettings settings, IActivationLog log)
          : base(settings, log) {
      _oracleTypes = (OracleDbTypeRegistry) this.Driver.TypeRegistry;
    }

    public override DbModel LoadModel() {
      if(LoadFilter != null) {
        _tableNamesList = ToQuotedList(LoadFilter.Tables);
        _viewNamesList = ToQuotedList(LoadFilter.Views);
        _sequenceNamesList = ToQuotedList(LoadFilter.Sequences);
      } //if filter
      return base.LoadModel();
    }

    public override DbTypeInfo GetColumnDbTypeInfo(InfoRow columnRow) {
      var charSize = columnRow.GetAsLong("CHARACTER_MAXIMUM_LENGTH");
      var byteSize = columnRow.GetAsLong("CHARACTER_OCTET_LENGTH");
      long size = charSize != 0 ? charSize : byteSize;
      var dataTypeString = columnRow.GetAsString("DATA_TYPE").ToLowerInvariant();
      var prec = (byte)columnRow.GetAsInt("NUMERIC_PRECISION");
      var scale = (byte)columnRow.GetAsInt("NUMERIC_SCALE");
      var typeInfo = Driver.TypeRegistry.GetDbTypeInfo(dataTypeString, size, prec, scale);
      return typeInfo;
    }

    //Columns: TYPE_SCHEMA	TYPE_NAME	IS_TABLE_TYPE IS_NULLABLE SIZE 
    public override InfoTable GetCustomTypes() {
      // We do not query all types - there way too many in even 'empty' database
      // We query names of types that are actually used in columns of tables that we are interested in
      return GetCustomTypesUsedInColumns();
    }

    const string _getCustomTypesUsedInColumnsSql = @"
SELECT '' TYPE_SCHEMA, tps.DATA_TYPE TYPE_NAME, 0 IS_TABLE_TYPE, 0 IS_NULLABLE, 0 ""SIZE""
  FROM (
  SELECT DISTINCT DATA_TYPE  
    FROM all_tab_columns
    WHERE DATA_TYPE NOT IN (
          'NUMBER', 'FLOAT', 'DOUBLE', 'BINARY_DOUBLE', 'BINARY_FLOAT',
          'VARCHAR2', 'NVARCHAR2', 'CHAR', 'NCHAR',
          'BLOB', 'CLOB', 'NCLOB', 'LONG',  'RAW', 'LONG RAW', 'ROWID',
          'DATE', 'TIMESTAMP',
          'ANYDATA', 'XMLTYPE' )
          AND DATA_TYPE NOT LIKE 'TIMESTAMP(%'
          AND DATA_TYPE NOT LIKE 'INTERVAL %'
          AND {0}
) tps
";
    private InfoTable GetCustomTypesUsedInColumns() {
      var filter = GetTablesPredicate();
      var resTable = ExecuteSelect(_getCustomTypesUsedInColumnsSql, filter);
      return resTable; 
    }
    // useful info here:
    // https://www.alberton.info/oracle_meta_info.html

    const string _getTablesSql = @"
  SELECT
    tablespace_name,
    ''                   table_catalog, 
    ''                   table_schema,
    table_name            table_name,
    'BASE TABLE'          table_type         
  FROM 
    all_tables
  WHERE {0}
";
    public override InfoTable GetTables() {
      var filter = GetTablesPredicate();
      var result = ExecuteSelect(_getTablesSql, filter);
      return result; 
    }

    const string _getViewsSql = @"
    SELECT 
      ''           table_catalog,
      ''           table_schema,
      view_name     table_name, 
     'VIEW'         table_type, 
      view_type     view_type,
      READ_ONLY     read_only,
      'N'           is_materialized,  
      text_vc     view_definition
    FROM
      all_views
    WHERE {0}
";

    public override InfoTable GetViews() {
      var filter = GetViewsPredicate();
      var result = ExecuteSelect(_getViewsSql, filter);
      return result;
    }

    const string _getColumnsSql = @"
    SELECT  
       '' table_catalog,
       '' table_schema,
       table_name, 
       column_name, 
       column_id ordinal_position,
       data_type, 
       char_length CHARACTER_MAXIMUM_LENGTH, 
       data_length CHARACTER_OCTET_LENGTH, 
       data_precision numeric_precision,
       data_scale numeric_scale,
       nullable is_nullable,
       data_default column_default, 
       identity_column is_identity
    FROM all_tab_columns
    WHERE {0}
    ORDER BY table_name, column_id
";
    public override InfoTable GetColumns() {
      var filter = GetTablesPredicate();
      var result = ExecuteSelect(_getColumnsSql, filter);
      return result;
    }

    // Expected columns: table_schema, table_name, index_name, primary_key, clustered, unique, disabled, filter_condition 
    const string _getIndexesSql = @"
    SELECT 
      '' table_catalog,
      '' table_schema,
      table_name,
      index_name,
      (CASE WHEN uniqueness = 'UNIQUE' THEN 'Y' ELSE 'N' END) ""unique"",
      0 primary_key, 
      clustering_factor clustered, 
      0 disabled,
      NULL filter_condition
    FROM all_indexes
    WHERE {0}
";

    public override InfoTable GetIndexes() {
      var filter = GetTablesPredicate();
      var sql = string.Format(_getIndexesSql, filter); 
      var indexesTable = ExecuteSelect(sql);
      // PKs are included in indexes, so we need to exclude them
      var pkNames = this.Model.Tables.Select(t => t.PrimaryKey?.Name).Where(n => n != null);
      var pkNameSet = new HashSet<string>(pkNames); 
      for(int i = indexesTable.Rows.Count - 1; i >= 0; i--) {
        var row = indexesTable.Rows[i];
        var iname = row["index_name"];
        if(pkNameSet.Contains(iname))
          indexesTable.Rows.RemoveAt(i);
      }
      return indexesTable;
    }

    const string _getIndexColumnsSql = @"
    SELECT 
        '' table_schema,
        table_name, 
        index_name, 
        column_name,
        column_position column_ordinal_position,
        (CASE WHEN descend = 'ASC' THEN 0 else 1 END) is_descending
     FROM all_ind_columns
     WHERE {0}
     ORDER BY table_name, index_name, column_position
 ";

    public override InfoTable GetIndexColumns() {
      var filter = GetTablesPredicate(); 
      var result = ExecuteSelect(_getIndexColumnsSql, filter);
      return result;
    }

    const string _sqlGetTableConstraintsSql = @"
    SELECT 
      '' constraint_catalog,
      '' constraint_schema,
      constraint_name,
      '' table_schema,
      table_name,
      constraint_type,
      delete_rule,
      deferrable is_deferrable  
    FROM all_constraints
    WHERE (constraint_type IN ('P', 'U', 'R')) AND {0}
    ORDER BY table_name, constraint_name
";
    //Columns: CONSTRAINT_CATALOG	CONSTRAINT_SCHEMA	CONSTRAINT_NAME	TABLE_CATALOG	
    //TABLE_SCHEMA	TABLE_NAME	CONSTRAINT_TYPE	IS_DEFERRABLE
    // type: P - PK, R - ref
    public override InfoTable GetTableConstraints() {
      const string ConstrType = "CONSTRAINT_TYPE";
      var filter = GetTablesPredicate();
      var result = ExecuteSelect(_sqlGetTableConstraintsSql, filter);
      // adjust constraint type
      foreach(var row in result.Rows) {
        var ct = row.GetAsString(ConstrType);
        switch(ct) {
          case "P": row[ConstrType] = "PRIMARY KEY"; break;
          case "R": row[ConstrType] = "FOREIGN KEY"; break; 
        }
      }
      return result;
    }

    //Columns: CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME, UNIQUE_CONSTRAINT_CATALOG, UNIQUE_CONSTRAINT_SCHEMA, 
    //  UNIQUE_CONSTRAINT_NAME, MATCH_OPTION, UPDATE_RULE, DELETE_RULE, C_TABLE, U_TABLE
    const string _sqlGetTableRefConstraintsSql = @"
SELECT 
      '' constraint_catalog,
      '' constraint_schema,
      c.constraint_name,
      c.r_constraint_name unique_constraint_name,
      '' unique_constraint_schema,
      t.table_name U_TABLE,
      c.table_name c_table,
      c.constraint_type,
      c.delete_rule,
      c.deferrable is_deferrable  
    FROM all_constraints c INNER JOIN all_constraints t 
       on c.r_constraint_name = t.constraint_name
    WHERE (c.constraint_type IN ('R')) AND {0}
    ORDER BY c.table_name, c.constraint_name";

    public override InfoTable GetReferentialConstraints() {
      var filter = GetTablesPredicate( "c.table_name");
      var result = ExecuteSelect(_sqlGetTableRefConstraintsSql, filter);
      /*
      // Query does not provide target table name. The calling method (LoadRefConstraints)
      // uses it to lookup table. So let's supplement it by looking up by target key name - which are target primary keys
      var allKeys = Model.Tables.Select(t => t.PrimaryKey).ToList();
      var keyLkp = allKeys.ToDictionary(k => k.Name, k => k);
      foreach(var row in result.Rows) {
        var cn = row.GetAsString("unique_constraint_name");
        if(keyLkp.TryGetValue(cn, out DbKeyInfo key))
          row["u_table"] = key.Table.TableName; 
      }
      */
      return result;
    }

    const string _getTableConstraintColumnsSql = @"
    SELECT 
      constraint_name,
      '' table_schema,
      table_name,
      column_name 
    FROM all_cons_columns
    WHERE {0}
    ORDER BY table_name, constraint_name, position
";

    public override InfoTable GetTableConstraintColumns() {
      var filter = GetTablesPredicate();
      var result = ExecuteSelect(_getTableConstraintColumnsSql, filter);
      return result;
    }

    const string _getSequencesSql = @"
    SELECT 
      '' sequence_schema,
      sequence_name,
      'numeric' data_type,
      min_value start_value,
      increment_by ""increment""
    FROM all_sequences
    WHERE {0}
";

    public override InfoTable GetSequences() {
      var filter = GetSequencesPredicate(); 
      var result = ExecuteSelect(_getSequencesSql, filter);
      return result;
    }


    // Oracle does not like ending semicolon in SQLs, so we cut it off
    public override InfoTable ExecuteSelect(string sql, params string[] args) {
      sql = sql.TrimEnd(' ', '\r', '\n', ';');
      return base.ExecuteSelect(sql, args);
    }

    #region Filters
    private string ToQuotedList(IList<string> names) {
      if(names == null || names.Count == 0)
        return null;
      return string.Join(",", names.Select(s => s.Quote()));
    }

    private string GetTablesPredicate(string tableNameCol = "TABLE_NAME") {
      if(string.IsNullOrEmpty(_tableNamesList))
        return GetDefaultNameFilter(tableNameCol);
      return $" {tableNameCol} IN ({_tableNamesList})";
    }
    private string GetViewsPredicate(string viewNameCol = "VIEW_NAME") {
      if(string.IsNullOrEmpty(_viewNamesList))
        return "1=0";
      return $" {viewNameCol} IN ({_viewNamesList})";
    }
    private string GetSequencesPredicate(string seqNameCol = "SEQUENCE_NAME") {
      if(string.IsNullOrEmpty(_sequenceNamesList))
        return "1=0";
      return $" {seqNameCol} IN ({_sequenceNamesList})";
    }

    /*
   Note:    Filtering out system tables; we use the following: 
        For regular model load (with entity model): Table_name IN (<table names in entity model>)
        For DbFirst: (name NOT LIKE sysPattern1) and (name not LIKE sysPattern2) .... 
     */

    private string GetDefaultNameFilter(string name) {
      //var sysNamePatterns = new string[] { "AQ$%", "DEF$%", "LOGMNR%", "LOGSTD%", "MVIEW%", "REPCAT%", "OL$%" };
      var sysNamePatterns = new string[] { "%$%", "LOGMNR%", "LOGSTD%", "MVIEW%", "REPCAT%", "BSLN_%" };
      var matchList = sysNamePatterns.Select(p => $@"
    ({name} NOT LIKE '{p}')");
      var result = string.Join(" AND ", matchList);
      return result;
    }

    #endregion

  } //class
}
