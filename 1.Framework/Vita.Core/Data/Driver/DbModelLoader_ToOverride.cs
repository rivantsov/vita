using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Model;

namespace Vita.Data.Driver {

  //Methods actually loading info from the database. Might be overridden in vendor-specific subclass. 
  public partial class DbModelLoader {

    //Builds schema filter from schemas listed in DbSettings. If this list is empty (which means 'all schemas), returns '1=1' expression
    protected virtual string GetSchemaFilter(string schemaColumn) {
      if (string.IsNullOrEmpty(_schemasListForFilter))
        return "(1=1)";
      return schemaColumn + " IN " + _schemasListForFilter;  
    }

    public virtual DbTable GetDatabases() {
      var sql = "SELECT DISTINCT CATALOG_NAME FROM INFORMATION_SCHEMA.SCHEMATA;";
      return ExecuteSelect(sql);
    }

    //Columns:CATALOG_NAME	SCHEMA_NAME	SCHEMA_OWNER	DEFAULT_CHARACTER_SET_CATALOG	DEFAULT_CHARACTER_SET_SCHEMA 
    //	DEFAULT_CHARACTER_SET_NAME  
    public virtual DbTable GetSchemas() {
      var filter = GetSchemaFilter("SCHEMA_NAME");
      var sql = string.Format("SELECT * FROM INFORMATION_SCHEMA.SCHEMATA WHERE {0};", filter);
      return ExecuteSelect(sql);
    }

    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME	TABLE_TYPE  
    public virtual DbTable GetTables() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sqlTemplate = @"
SELECT * FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE='{0}' AND {1}
ORDER BY TABLE_SCHEMA, TABLE_NAME;";
      var sql = string.Format(sqlTemplate, TableTypeTag, filter);
      return ExecuteSelect(sql);
    }

    //Used by DbInfo loader
    public virtual bool TableExists(string schema, string tableName) {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sqlTemplate = @"
SELECT * FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME='{0}' AND {1}";
      var sql = string.Format(sqlTemplate, tableName, filter);
      var dt = ExecuteSelect(sql);
      return dt.Rows.Count > 0;
    }

    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME VIEW_DEFINITION CHECK_OPTION IS_UPDATABLE  
    public virtual DbTable GetViews() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sqlTemplate = @"
SELECT * 
FROM INFORMATION_SCHEMA.VIEWS 
WHERE {0}
ORDER BY TABLE_SCHEMA, TABLE_NAME;";
      var sql = string.Format(sqlTemplate, filter);
      var dt = ExecuteSelect(sql);
      // add IS_MATERIALIZED column
      dt.AddColumn("IS_MATERIALIZED", typeof(string));
      return dt; 
    }

    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME	COLUMN_NAME	ORDINAL_POSITION	
    //COLUMN_DEFAULT	IS_NULLABLE	DATA_TYPE	CHARACTER_MAXIMUM_LENGTH	CHARACTER_OCTET_LENGTH	
    //NUMERIC_PRECISION	NUMERIC_PRECISION_RADIX	NUMERIC_SCALE	DATETIME_PRECISION	
    //CHARACTER_SET_CATALOG	CHARACTER_SET_SCHEMA	CHARACTER_SET_NAME	COLLATION_CATALOG	
    //COLLATION_SCHEMA	COLLATION_NAME	DOMAIN_CATALOG	DOMAIN_SCHEMA	DOMAIN_NAME
    public virtual DbTable GetColumns() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(@"SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
  WHERE {0}
  ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;", filter);
      return ExecuteSelect(sql);
    }

    // Constraints are primary keys and foreign keys
    //Columns: CONSTRAINT_CATALOG	CONSTRAINT_SCHEMA	CONSTRAINT_NAME	TABLE_CATALOG	
    //TABLE_SCHEMA	TABLE_NAME	CONSTRAINT_TYPE	IS_DEFERRABLE	INITIALLY_DEFERRED
    public virtual DbTable GetTableConstraints() {
      var filter = GetSchemaFilter("CONSTRAINT_SCHEMA");
      var sql = string.Format("SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE {0}", filter);
      return ExecuteSelect(sql);
    }
    
    //Columns: TABLE_CATALOG	TABLE_SCHEMA	TABLE_NAME	COLUMN_NAME ORDINAL_POSITION	CONSTRAINT_CATALOG	
    //CONSTRAINT_SCHEMA	CONSTRAINT_NAME
    public virtual DbTable GetTableConstraintColumns() {
      var filter = GetSchemaFilter("TABLE_SCHEMA");
      var sql = string.Format(@"SELECT * FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
   WHERE {0}
   ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
", filter);
      return ExecuteSelect(sql);
    }

    //Columns: CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME, UNIQUE_CONSTRAINT_CATALOG, UNIQUE_CONSTRAINT_SCHEMA, 
    //  UNIQUE_CONSTRAINT_NAME, MATCH_OPTION, UPDATE_RULE, DELETE_RULE
    // Not used, we use ext version below
    public virtual DbTable GetReferentialConstraints() {
      var filter = GetSchemaFilter("CONSTRAINT_SCHEMA");
      var sql = string.Format(@"SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
  WHERE {0}", filter);
      return ExecuteSelect(sql);
    }

    // Extended version with joins to bring Table names for both constraints
    //Columns: CONSTRAINT_CATALOG, CONSTRAINT_SCHEMA, CONSTRAINT_NAME, UNIQUE_CONSTRAINT_CATALOG, UNIQUE_CONSTRAINT_SCHEMA, 
    //  UNIQUE_CONSTRAINT_NAME, MATCH_OPTION, UPDATE_RULE, DELETE_RULE, C_TABLE, U_TABLE
    public virtual DbTable GetReferentialConstraintsExt() {
      var filter = GetSchemaFilter("rc.CONSTRAINT_SCHEMA");
      var sql = string.Format(@"
SELECT rc.*, tc1.TABLE_NAME as C_TABLE, tc2.TABLE_NAME AS U_TABLE 
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
  INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc1 
    ON tc1.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA AND 
       tc1.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
  INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc2 
    ON tc2.CONSTRAINT_SCHEMA = rc.UNIQUE_CONSTRAINT_SCHEMA AND 
       tc2.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME
WHERE {0};", filter);
      return ExecuteSelect(sql);
    }


    //All Columns: SPECIFIC_CATALOG	SPECIFIC_SCHEMA	SPECIFIC_NAME	ROUTINE_CATALOG	ROUTINE_SCHEMA 
    //	ROUTINE_NAME	ROUTINE_TYPE	MODULE_CATALOG	MODULE_SCHEMA	MODULE_NAME	UDT_CATALOG	
    //UDT_SCHEMA	UDT_NAME	DATA_TYPE	CHARACTER_MAXIMUM_LENGTH	CHARACTER_OCTET_LENGTH	
    //COLLATION_CATALOG	COLLATION_SCHEMA	COLLATION_NAME	CHARACTER_SET_CATALOG	
    //CHARACTER_SET_SCHEMA	CHARACTER_SET_NAME	NUMERIC_PRECISION	NUMERIC_PRECISION_RADIX	
    //NUMERIC_SCALE	DATETIME_PRECISION	INTERVAL_TYPE	INTERVAL_PRECISION	TYPE_UDT_CATALOG	
    //TYPE_UDT_SCHEMA	TYPE_UDT_NAME	SCOPE_CATALOG	SCOPE_SCHEMA	SCOPE_NAME	MAXIMUM_CARDINALITY	
    //DTD_IDENTIFIER	ROUTINE_BODY	ROUTINE_DEFINITION	EXTERNAL_NAME	EXTERNAL_LANGUAGE	
    //PARAMETER_STYLE	IS_DETERMINISTIC	SQL_DATA_ACCESS	IS_NULL_CALL	SQL_PATH	SCHEMA_LEVEL_ROUTINE	
    //MAX_DYNAMIC_RESULT_SETS	IS_USER_DEFINED_CAST	IS_IMPLICITLY_INVOCABLE	CREATED	LAST_ALTERED
    // Note: we select only schema, name, definition, type and routine_custom (null)
    //Custom_tag is vendor-specific info. In Postgres we put proc parameter string there - it is needed for 'Delete function' method.
    public virtual DbTable GetRoutines() {
      const string template = @"
  SELECT ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_DEFINITION, ROUTINE_TYPE, NULL AS ROUTINE_CUSTOM 
    FROM INFORMATION_SCHEMA.ROUTINES 
    WHERE ROUTINE_TYPE='{0}' AND {1} ";
      var filter = GetSchemaFilter("ROUTINE_SCHEMA");
      var sql = string.Format(template, RoutineTypeTag, filter);
      return ExecuteSelect(sql);
    }

    //INFORMATION_SCHEMA does not have a view for indexes, column defaults and identity columns -------------------

    //Drivers should override this method with particular implementations
    // Expected columns: table_schema, table_name, index_name, primary_key, clustered, unique, disabled, filter 
    public virtual DbTable GetIndexes() {
      return EmptyTable; 
    }
    // Expected columns: table_schema, table_name, index_name, column_name, column_ordinal_position, is_descending
    public virtual DbTable GetIndexColumns() {
      return EmptyTable;
    }
    // Expected columns: SEQUENCE_SCHEMA, SEQUENCE_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE, START_VALUE, INCREMENT
    public virtual DbTable GetSequences() {
      const string template = @"
  SELECT SEQUENCE_SCHEMA, SEQUENCE_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE, START_VALUE, INCREMENT 
    FROM INFORMATION_SCHEMA.SEQUENCES 
    WHERE {0} ";
      var filter = GetSchemaFilter("SEQUENCE_SCHEMA");
      var sql = string.Format(template, filter);
      return ExecuteSelect(sql);
    }

    // columns: TYPE_SCHEMA, TYPE_NAME
    //currently used on by MS SQL to load array-as-table user type, used for sending array as parameter
    public virtual DbTable GetUserDefinedTypes() {
      return EmptyTable; 
    }

    public virtual DbTypeInfo GetDbTypeInfo(DbRow columnRow) {
      var charSize = columnRow.GetAsLong("CHARACTER_MAXIMUM_LENGTH");
      var byteSize = columnRow.GetAsLong("CHARACTER_OCTET_LENGTH");
      var dataTypeString = columnRow.GetAsString("DATA_TYPE").ToLowerInvariant();
      var prec = columnRow.GetAsInt("NUMERIC_PRECISION");
      var scale = columnRow.GetAsInt("NUMERIC_SCALE");
      var isNullable = (columnRow.GetAsString("IS_NULLABLE") == "YES");
      bool isMemo = (charSize < 0 || byteSize < 0);
      var typeDef = Driver.TypeRegistry.FindVendorDbTypeInfo(dataTypeString);
      // if(typeDef == null)
      //    typeDef = Driver.TypeRegistry.Types.FirstOrDefault(td => td.Aliases.Contains(dataTypeString));
      if(typeDef == null)
        return null;
      var isAutoMemo = Driver.TypeRegistry.AutoMemoTypes.Contains(dataTypeString);
      var size = isAutoMemo? -1 : (charSize != 0 ? charSize : byteSize);
      var typeSpec = typeDef.FormatTypeSpec(size, prec, scale, isMemo);
      var typeInfo = new DbTypeInfo(typeDef, typeSpec, isNullable, size, (byte)prec, (byte) scale, typeDef.DefaultColumnInit);
      // AssignValueConverters(typeInfo); - no need for value converters
      return typeInfo;
    }

    // A chance for a driver to add vendor-specific information to the loaded column. 
    public virtual void OnColumnLoaded(DbColumnInfo column, DbRow columnRow) {

    }

    // A chance for a driver to add vendor-specific information to the loaded model. 
    public virtual void OnModelLoaded() {

    }



  }
}
