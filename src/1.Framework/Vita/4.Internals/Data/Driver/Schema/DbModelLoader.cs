using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Reflection;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Services;
using Vita.Entities.Logging;
using Vita.Data.Driver.InfoSchema;

namespace Vita.Data.Driver {

  /// <summary>Loads DB model from database. </summary>
  public partial class DbModelLoader {
    //returned by information_schema.Tables view as Table_type; for some db vendors it is different (postgres: BASE TABLE)
    protected string TableTypeTag = "TABLE";
    protected string ViewTypeTag = "VIEW";
    protected string RoutineTypeTag = "PROCEDURE";

    protected DbSettings Settings;
    protected DbDriver Driver;
    protected ILog Log;
    protected DbModel Model;

    //Static empty table, to use as return object for methods that return 'nothing' (not supported aspects)
    protected InfoTable EmptyTable = new InfoTable();

    //Schemas are initially copied from Settings. But app can reset them to desired list later.  
    //note: if _schemas is empty, it means - load all.
    private StringSet _schemasSubSet = new StringSet();

    public DbModelLoader(DbSettings settings, ILog log) {
      Settings = settings;
      Log = log;
      Driver = Settings.ModelConfig.Driver;
    }

    public virtual DbModel LoadModel() {
      Model = new DbModel(Settings.ModelConfig);
      LoadSchemas();
      LoadCustomTypes(); 
      LoadTables();
      if (Driver.Supports(DbFeatures.Views))
        LoadViews();
      LoadTableColumns();
      LoadTableConstraints();
      LoadTableConstraintColumns();
      LoadReferentialConstraints();
      LoadIndexes();
      LoadIndexColumns();
      LoadSequences();
      OnModelLoaded(); 
      return Model; 
    }//method


    protected virtual bool IncludeSchema(string schema) {
      if(!SupportsSchemas())
        return true;
      return _schemasSubSet.Contains(schema); 
    }

    protected bool SupportsSchemas() {
      return Driver.Supports(DbFeatures.Schemas);
    }

    public void SetSchemasSubset(IList<string> schemas) {
      _schemasSubSet.UnionWith(schemas); 
    }

    //Loads schemas into dbModel.Setup.Schemas only if it was originally empty
    protected virtual void LoadSchemas() {
      Model.Schemas.Clear(); //important - clear schemas that were copied from Setup
      if(!SupportsSchemas())
        return;
      var data = GetSchemas();
      foreach(InfoRow row in data.Rows) {
        var schema = row.GetAsString("SCHEMA_NAME");
        if(IncludeSchema(schema)) //add only schemas that are relevant to the model
          Model.Schemas.Add(new DbSchemaInfo(Model, schema));
      }
    }

    protected virtual void LoadCustomTypes() {
      var data = GetCustomTypes();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TYPE_SCHEMA");
        // if (!IncludeSchema(schema)) continue; -- do not filter by schema here, filter already applied in GetCustomTypes
        //   For MS SQL case, Vita_ArrayAsTable is in dbo schema (type is shared)
        var typeName = row.GetAsString("TYPE_NAME");
        var kind = row.GetAsInt("IS_TABLE_TYPE") == 1 ? DbCustomTypeKind.TableType : DbCustomTypeKind.Regular;
        var isNullable = row.GetAsInt("IS_NULLABLE") == 1;
        var size = row.GetAsInt("SIZE");
        var typeInfo = new DbCustomTypeInfo(Model, schema, typeName, kind, isNullable, size);
        //constructor adds it to Model.CustomTypes
      }
    }


    protected virtual void LoadTables() {
      var data = GetTables();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if (!IncludeSchema(schema)) continue; 
        var tableName = row.GetAsString("TABLE_NAME");
        var tblInfo = new DbTableInfo(Model, schema, tableName, null); 
      }
    }

    protected virtual void LoadViews() {
      var data = GetViews();
      var supportsMatViews = Driver.Supports(DbFeatures.MaterializedViews);
      foreach(InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if(!IncludeSchema(schema)) continue;
        var viewName = row.GetAsString("TABLE_NAME");
        var sql = row.GetAsString("VIEW_DEFINITION");
        var view = new DbTableInfo(Model, schema, viewName, null, DbObjectType.View);
        view.ViewSql = sql;
        if (supportsMatViews && row.GetAsString("IS_MATERIALIZED") == "Y")
          view.IsMaterializedView = true;
      }
    }

    protected virtual void LoadTableColumns() {
      DbTableInfo currTblInfo = null;
      var data = GetColumns();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if (!IncludeSchema(schema)) continue;
        var tableName = row.GetAsString("TABLE_NAME");
        if(currTblInfo == null || currTblInfo.Schema != schema || currTblInfo.TableName != tableName) {
          currTblInfo = Model.GetTable(schema, tableName);
        }
        if(currTblInfo == null) 
          continue; //it is a view
        var colName = row.GetAsString("COLUMN_NAME");
        var dataTypeString = row.GetAsString("DATA_TYPE");
        var isNullStr = row.GetAsString("IS_NULLABLE");
        var isNullable = (isNullStr == "YES" || isNullStr == "Y"); //Oracle->Y
        var typeInfo = GetColumnDbTypeInfo(row);
        if (typeInfo == null) {
          Log.LogError($"DbModelLoader: failed to find type mapping for DB data type '{dataTypeString}'. Table {tableName}, column {colName}.");
          continue;
        }
        var column = new DbColumnInfo(currTblInfo, colName, typeInfo, isNullable);
        column.DefaultExpression = row.GetAsString("COLUMN_DEFAULT");
        // Let schema manager add any provider-specific info
        OnColumnLoaded(column, row);
      }//foreach
    }

    protected virtual void LoadTableConstraints() {
      var data = GetTableConstraints();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if (!IncludeSchema(schema)) continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var table = Model.GetTable(schema, tableName);
        var constrName = row.GetAsString("CONSTRAINT_NAME");
        var constrTypeStr = row.GetAsString("CONSTRAINT_TYPE");
        if(table == null) continue;
        KeyType keyType;
        switch(constrTypeStr.Trim()) {
          case "PRIMARY KEY": keyType = KeyType.PrimaryKey;       break;
          case "FOREIGN KEY": keyType = KeyType.ForeignKey;       break;
          default: continue; //skip this constraint
        }
        var dbKey = new DbKeyInfo(constrName, table, keyType);
        if (keyType == KeyType.PrimaryKey)
          table.PrimaryKey = dbKey;
      }
      //sanity check - all tables must have PK
      foreach(var table in Model.Tables)
        if(table.PrimaryKey == null && table.Kind == EntityKind.Table) {
          //Just to have a line for a breakpoint
          //System.Diagnostics.Debug.WriteLine("DBModelLoader warning: Table without PK:" + table.TableName);
        }
    }
    protected virtual void LoadTableConstraintColumns() {
      var data = GetTableConstraintColumns();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if (!IncludeSchema(schema)) continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var table = Model.GetTable(schema, tableName);
        if (table == null)  continue; 
        var colName = row.GetAsString("COLUMN_NAME");
        var keyName = row.GetAsString("CONSTRAINT_NAME");
        var key = table.Keys.FirstOrDefault(k => k.Name == keyName); 
        if(key == null) continue;
        var col = key.Table.Columns.FirstOrDefault(c => c.ColumnName == colName);
        if(col == null)
          Log.LogError($"Failed to locate column {colName} in table {tableName}.");
        else 
          key.KeyColumns.Add(new DbKeyColumnInfo(col));
      }
      //Mark PK columns as no update
      foreach (var t in Model.Tables)
        if (t.PrimaryKey != null)
          foreach (var kc in t.PrimaryKey.KeyColumns)
            kc.Column.Flags |= DbColumnFlags.PrimaryKey | DbColumnFlags.NoUpdate; 
    }

    protected virtual void LoadReferentialConstraints() {
      var data = GetReferentialConstraints();
      foreach (InfoRow row in data.Rows) {
        //Load names for Foreign key and Unique key
        var fkSchema = row.GetAsString("CONSTRAINT_SCHEMA");
        var toSchema = row.GetAsString("UNIQUE_CONSTRAINT_SCHEMA");
        if (!IncludeSchema(toSchema) || !IncludeSchema(fkSchema)) 
          continue;
        var fkTableName = row.GetAsString("C_TABLE");
        var fkName = row.GetAsString("CONSTRAINT_NAME");
        var fromKey = FindKey(fkSchema, fkTableName, fkName);
        Util.Check(fromKey != null, "Key {0} for table {1} not found.", fkName, fkTableName);

        var toTableName = row.GetAsString("U_TABLE");
        var toTable = Model.GetTable(toSchema, toTableName);
        if(toTable == null)
          continue; // target table is being deleted, ignore this constraint

        var toKey = toTable.PrimaryKey; 
        bool cascadeDelete = row.GetAsString("DELETE_RULE") == "CASCADE";
        var refConstraint = new DbRefConstraintInfo(Model, fromKey, toKey, cascadeDelete); //will be added to list by constr
        // set FK flag
        foreach(var kCol in fromKey.KeyColumns)
          kCol.Column.Flags |= DbColumnFlags.ForeignKey; 
      }
    }

    protected virtual DbKeyInfo FindKey(string schema, string tableName, string keyName) {
      var table = Model.GetTable(schema, tableName);
      Util.Check(table != null, "FindKey: table {0}.{1} not found in DB Model.", schema, tableName);
      var key = table.Keys.FirstOrDefault(k => k.Name == keyName);
      return key; 
    }

    protected virtual void LoadIndexes() {
      var data = GetIndexes();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if (!IncludeSchema(schema)) continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var table = Model.GetTable(schema, tableName);
        if (table == null)
          continue;
        var indexName = row.GetAsString("INDEX_NAME");
        //indexName might be null for SQL Server (type_desc="HEAP") - just ignore these
        if (string.IsNullOrWhiteSpace(indexName)) 
          continue; 
        //primary keys are added through table constraints, so skip them here - except mark them as clustered
        var isPk = IsTrueOrNonZero(row, "primary_key");
        bool isClustered = Driver.Supports(DbFeatures.ClusteredIndexes) && IsTrueOrNonZero(row, "clustered");             
        if (isPk && isClustered) 
            table.PrimaryKey.KeyType = KeyType.ClusteredPrimaryKey; 
        if (isPk)
            continue; //PKs are added through constraints
        var isUnique = IsTrueOrNonZero(row, "unique");
        // Find existing, or create a new one
        var key = table.Keys.FindByName(indexName);
        //If key not exists yet, create it
        if (key == null)
          key = new DbKeyInfo(indexName, table, KeyType.Index);
        else
          key.KeyType |= KeyType.Index; 
        if (isClustered)
          key.KeyType |= KeyType.ClusteredIndex;
        if (isUnique)
          key.KeyType |= KeyType.UniqueIndex;
        var filter = row.GetAsString("FILTER_CONDITION");
        if(!string.IsNullOrEmpty(filter))
          key.Filter = new DbTableFilter() { DefaultSql = filter };
      }//while
    }

    protected virtual void LoadIndexColumns() {
      var data = GetIndexColumns();
      foreach (InfoRow row in data.Rows) {
        var schema = row.GetAsString("TABLE_SCHEMA");
        if (!IncludeSchema(schema)) continue;
        var tableName = row.GetAsString("TABLE_NAME");
        var table = Model.GetTable(schema, tableName);
        if (table == null) 
          continue;
        var index_name = row.GetAsString("INDEX_NAME");
        var colName = row.GetAsString("COLUMN_NAME");
        var colOrdPos = row.GetAsInt("column_ordinal_position");
        var key = table.Keys.FindByName(index_name);
        if (key == null) {
          //LogError("Loading index columns: failed to find owner index/key {0} for column {1}, table {2}.", index_name, colName, tableName);
          continue;
        }
        var dbCol = table.Columns.FindByName(colName);
        if (dbCol == null) continue;
        var desc = row.GetAsInt("IS_DESCENDING");
        bool isDesc = desc == 1;
        var oldKeyCol = key.KeyColumns.FirstOrDefault(kc => kc.Column == dbCol);
        if(oldKeyCol == null) {
          //MS SQL only - supports 'include' columns; for these columns value of ord position is zero
          if(colOrdPos == 0)
            key.IncludeColumns.Add(dbCol); 
          else 
            key.KeyColumns.Add(new DbKeyColumnInfo(dbCol, desc: isDesc));
        } else
          oldKeyCol.Desc = isDesc; 
      }//foreach row
    }//method

    protected virtual void LoadSequences() {
      if (!Settings.Driver.Supports(DbFeatures.Sequences))
        return; 
      var data = GetSequences();
      foreach(InfoRow row in data.Rows) {
        var schema = row.GetAsString("SEQUENCE_SCHEMA");
        var name = row.GetAsString("SEQUENCE_NAME");
        var dataTypeName = row.GetAsString("DATA_TYPE");
        var startValue = row.GetAsLong("START_VALUE");
        var incr = row.GetAsInt("INCREMENT");
        // add sequence
        var seq = new DbSequenceInfo(this.Model, name, schema, dataTypeName, startValue, incr);
        Model.AddSequence(seq); 
      }
    }

    protected static bool IsTrueOrNonZero(InfoRow row, string columnName) {
      var value = row[columnName];
      if (value == null || value == DBNull.Value) return false;
      var t = value.GetType();
      if (t == typeof(bool))
        return (bool)value;
      if (t.IsInt()) {
        var iv = Convert.ToInt32(value);
        return iv != 0;
      }
      if (t == typeof(string)) {
        var sv = ((string)value).ToUpperInvariant();
        if(sv == "Y" || sv == "YES")
          return true; 
      }
      return false;
    }

    public virtual InfoTable ExecuteSelect(string sql, params string[] args) {
      if (args != null && args.Length > 0)
        sql = string.Format(sql, args); 
      return Settings.Driver.ExecuteRawSelect(Settings.SchemaManagementConnectionString, sql);
    }

  }//class

}
