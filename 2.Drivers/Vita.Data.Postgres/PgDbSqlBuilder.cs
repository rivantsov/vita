using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Data.Model;
using Vita.Data.Driver;
using System.Data;
using System.Collections;
using System.Reflection;
using NpgsqlTypes;


namespace Vita.Data.Postgres{
  public class PgDbSqlBuilder : DbSqlBuilder  {
    public PgDbSqlBuilder(DbModel model) : base(model) {

    }

    protected override SqlLimitClause GetSqlLimitClause(string count) {
      return new SqlLimitClause() { LimitClause = "LIMIT " + count };
    }

    public override DbCommandInfo BuildSelectAllPagedCommand(EntityCommand entityCommand) {
      //Note: PgSql cuts-off some trailing spaces on lines, and the stored proc comparison might fail in 
      // Db model comparer - identical procs are detected as different. So do not add trailing spaces anywhere
      const string SqlSelectAllPaged = @"
SELECT {0}
FROM   {1}
{2}
OFFSET {3}__skipRows
LIMIT {3}__maxRows;";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      string strOrderBy = (table.DefaultOrderBy == null) ? "ORDER BY (SELECT 1)" : BuildOrderBy(table.DefaultOrderBy);
      var prmPrefix = GetParameterPrefix();
      var sql = string.Format(SqlSelectAllPaged, strColumns, table.FullName, strOrderBy, prmPrefix);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectAllPaged");
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, sql);
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;
    }


    public override DbCommandInfo BuildSelectByKeyArrayCommand(EntityCommand entityCommand) {
      const string SqlSelectByFkTemplate = @"
SELECT {0} 
  FROM {1} 
  WHERE {2}
{3}";
      var table = DbModel.LookupDbObject<DbTableInfo>(entityCommand.TargetEntityInfo, throwNotFound: true);
      var dbKey = DbModel.LookupDbObject<DbKeyInfo>(entityCommand.SelectKey);
      Util.Check(dbKey.KeyColumns.Count == 1, "Cannot construct SelectByKeyArray command for composite keys. Key: {0}", dbKey);
      var keyCols = dbKey.KeyColumns.GetNames(removeUnderscores: true);
      var cmdName = ModelConfig.NamingPolicy.ConstructDbCommandName(entityCommand, table.TableName, "SelectByArrayOf", keyCols);
      var cmdInfo = CreateDbCommandInfo(entityCommand, cmdName, table, DbExecutionType.Reader, null);
      //Build column list
      var outColumns = table.Columns.GetSelectable();
      var strColumns = outColumns.GetSqlNameList();
      //build WHERE clause
      var whereExpr = string.Format(" \"{0}\" = Any({1}) ",  dbKey.KeyColumns[0].Column.ColumnName, cmdInfo.Parameters[0].Name);
      if (!string.IsNullOrWhiteSpace(entityCommand.Filter))
        whereExpr = whereExpr + " AND " + ProcessFilter(entityCommand, table);
      string orderByExpr = null;
      if (dbKey.KeyType == KeyType.PrimaryKey)
        orderByExpr = null;
      else
        orderByExpr = BuildOrderBy(table.DefaultOrderBy);
      var sql = string.Format(SqlSelectByFkTemplate, strColumns, table.FullName, whereExpr, orderByExpr);
      //Damn postgres reformats the SQL in stored proc body and this screws up comparison; so we are careful here
      sql = sql.Trim() + ";";
      cmdInfo.Sql = sql;
      cmdInfo.EntityMaterializer = CreateEntityMaterializer(table, outColumns);
      return cmdInfo;

    }//method


    public override DbCommandInfo BuildSqlInsertCommand(EntityCommand entityCommand) {
      var cmd = base.BuildSqlInsertCommand(entityCommand);
      //if command has OUT parameters for identity columns, modify the sql to return them 
      var outParams = cmd.Parameters.Where(p => p.Direction == ParameterDirection.Output).ToList();
      if (outParams.Count == 0)
        return cmd;
      var lstRetCols = new List<string>();
      foreach (var prm in outParams) {
        var col = prm.SourceColumn;
        if (col != null && col.Flags.IsSet(DbColumnFlags.Identity)) {
          lstRetCols.Add('"' + col.ColumnName + '"');
        }
      }

      if (lstRetCols.Count == 0)
        return cmd;
      var sql = cmd.Sql.TrimEnd(' ', '\r', '\n', ';'); //trim ending semicolon so that we can append RETURNING clause
      var strReturning = " RETURNING " + string.Join(", ", lstRetCols); // string.Join(", ", lstSelects) + " AS " + string.Join(", ", lstTargets);
      cmd.Sql = sql + strReturning + ";";
      return cmd;
    }

    public override bool ConvertToStoredProc(DbCommandInfo command) {
      switch (command.Kind) {
        case EntityCommandKind.SelectAll:
        case EntityCommandKind.SelectAllPaged:
        case EntityCommandKind.SelectByKey:
        case EntityCommandKind.SelectByKeyManyToMany:
          return ConvertQueryToStoredProc(command);
        case EntityCommandKind.SelectByKeyArray:
          return ConvertQueryToStoredProc(command);
        default:
          return ConvertNonQueryToStoredProc(command);
      }
    }

    // See examples of stored procs at the end of this file

    private bool ConvertQueryToStoredProc(DbCommandInfo command) {
      var table = command.Table;
      string ProcBodyTemplate =
@"{0}
DECLARE
    ref1 refcursor;
BEGIN
    OPEN ref1 FOR
    {1}
RETURN ref1;
{2}
END;
";
      string CreateProcTemplate = 
@"{0}
CREATE OR REPLACE FUNCTION {1} ({2}) RETURNS refcursor AS $$
{3}
$$ LANGUAGE plpgsql;
";
      command.Schema = table.Schema;
      //Build command that creates stored proc and execute it
      var listParams = new StringList();
      foreach (var prm in command.Parameters) {
        var strOut = (prm.Direction & ParameterDirection.Output) != 0 ? " OUT" : string.Empty;
        listParams.Add("    " + strOut + prm.Name + " " + prm.TypeInfo.SqlTypeSpec);
      }
      var strParams = listParams.Count > 0 ? "\r\n    " + string.Join(",\r\n    ", listParams) + "\r\n" : string.Empty;
      var header = BuildProcHeader(command);
      command.SourceHash = SourceHasher.ComputeHash(command.FullCommandName, strParams, command.Sql);
      command.StoredProcBody = string.Format(ProcBodyTemplate, header, command.Sql, SourceHasher.GetHashLine(command.SourceHash));
      command.StoredProcText =
        string.Format(CreateProcTemplate, header, command.FullCommandName, strParams, command.StoredProcBody);
      return true;
    }

    public override DbTypeInfo GetDbTypeInfo(Type clrType, int size) {
      var typeInfo = base.GetDbTypeInfo(clrType, size);
      if (typeInfo != null)
        return typeInfo; 
      Type elemType;
      if ((clrType.IsArray || clrType.IsGenericType) && clrType.IsListOfDbPrimitive(out elemType)) {
        var elemTypeInfo = base.GetDbTypeInfo(elemType, 0);
        var npgDbType = NpgsqlDbType.Array | (NpgsqlDbType)elemTypeInfo.VendorDbType.VendorDbType; //Pg requires combination of Array and elem type
        var arrVendDbType = new VendorDbTypeInfo(elemType.Name + "[]", DbType.Object, typeof(object), null, null, null, null, VendorDbTypeFlags.Array, null, null, (int)npgDbType);
        var arrTypeInfo = new DbTypeInfo(arrVendDbType, elemTypeInfo.SqlTypeSpec + "[]", false, 0, 0, 0);
        arrTypeInfo.PropertyToColumnConverter = (obj) => ConvertListToArray(elemType, obj); 
        //ConvertListToArray;
        return arrTypeInfo; 
      }
      Util.Throw("Failed to match DB type to CLR type {0}.", clrType);
      return null; 
    }

    private static object ConvertListToArray(Type elemType, object value) {
      var iEnum = value as IEnumerable;
      //Convert to list, then copy to array
      IList<object> list = new List<object>(); 
      foreach (var v in iEnum) 
        list.Add(v);
      //copy to array
      var arr = Array.CreateInstance(elemType, list.Count);
      for (int i = 0; i < list.Count; i++)
        arr.SetValue(list[i], i);
      return arr;
    }

    //Note: be careful not to introduce trailing spaces, esp. when some of the template args are empty;
    // latest PG driver cuts them off. 
    // TODO: need better solution for Postgres trailing spaces
    private bool ConvertNonQueryToStoredProc(DbCommandInfo command) {
      var table = command.Table;
      //TODO: refactor to return #of records
      string ProcBodyTemplate =
@"{0}
BEGIN
    {1}{2};
{3}
END;";
      const string CreateProcTemplate = @"
CREATE OR REPLACE FUNCTION {0}({1}) {2} AS
$$
{3}
$$
LANGUAGE plpgsql;
";
      // In Postgres, if function has output parameter(s), it must return scalar or record. If there's no out parameters, you must specify 
      // something as return type - we specify VOID. If we have out param(s), we skip 'returns' clause, and Postgres adds it automatically. 
      // We have OUT parameter for identity field; we retrieving Identity value for INSERT using ' ... RETURNING "Id" INTO p_id; ' clause.
      // Insert SQL has already "... RETURNING "Id" ' clause - we add only ' INTO p_Id ' extra.
      command.Schema = table.Schema;
      var listParams = new StringList();
      bool hasOut = false; 
      var lstTargets = new List<string>();
      foreach (var prm in command.Parameters) {
        string strOut = string.Empty;
        if ((prm.Direction & ParameterDirection.Output) != 0) {
          hasOut = true; 
          strOut = "OUT ";
          var col = prm.SourceColumn;
          if (col != null && col.Flags.IsSet(DbColumnFlags.Identity)) {
            lstTargets.Add(prm.Name);
          }
        }
        listParams.Add("    " + strOut + prm.Name + " " + prm.TypeInfo.SqlTypeSpec);
      }
      string strReturns = hasOut ? string.Empty : "RETURNS VOID";
      var strParams = listParams.Count > 0 ? "\r\n    " + string.Join(",\r\n    ", listParams) + "\r\n" : string.Empty;
      var header = BuildProcHeader(command);
      var strRetInto = string.Empty;
      if (lstTargets.Count > 0)
        strRetInto = "\r\n    INTO "  + string.Join(", ", lstTargets);
      var sql = command.Sql.TrimEnd(' ', '\r', '\n', ';'); //trim ending semicolon so that we can append RETURNING clause

      command.SourceHash = SourceHasher.ComputeHash(command.FullCommandName, strParams, sql);
      command.StoredProcBody = string.Format(ProcBodyTemplate, header, sql, strRetInto, SourceHasher.GetHashLine(command.SourceHash));
      command.StoredProcText =
        string.Format(CreateProcTemplate, command.FullCommandName, strParams, strReturns, command.StoredProcBody);
      return true;
    }

    private string BuildProcHeader(DbCommandInfo command) {
      var entCommand = command.EntityCommand;
      //table/category/operation; for ex: Product/CRUD/Update
      const string ProcHeaderLineTemplate = 
@"{0}{1}
-- Description: {2}
";
      return string.Format(ProcHeaderLineTemplate, DbDriver.GeneratedCrudProcTagPrefix, command.DescriptiveTag, entCommand.Description);
    }

    public override DbCommandInfo BuildSqlSequenceGetNextCommand(DbSequenceInfo sequence) {
      const string SqlTemplate = "SELECT nextval('{0}.\"{1}\"');"; //note sequence name in double quotes inside single-quote argument
      //Load by primary key
      var cmdName = sequence.Name + "_GetNextValue";
      var cmdInfo = new DbCommandInfo(DbModel, sequence.Schema, cmdName, null, null);
      cmdInfo.Sql = string.Format(SqlTemplate, sequence.Schema, sequence.Name);
      cmdInfo.ExecutionType = DbExecutionType.Scalar;
      return cmdInfo;
    }

  }//class

  /*
-- Example of SELECT with paging  
CREATE OR REPLACE FUNCTION ident."CarSelectAllPaged"(p___skiprows integer, p___maxrows integer)
  RETURNS refcursor AS
$BODY$ 
-- VITA:Generated;Tag=ident.Car/SelectAllPaged
-- Description: Selects all entities.
DECLARE
    ref1 refcursor;
BEGIN
    OPEN ref1 FOR
SELECT "Id", "Model", "Owner_Id" 
  FROM   "ident"."Car"
  ORDER BY "Id"
  OFFSET p___skiprows
  LIMIT p___maxrows; 
  RETURN ref1;
END;

$BODY$
  LANGUAGE plpgsql
    
    
--  Example of INSERT function with identity column
CREATE OR REPLACE FUNCTION ident."CarInsert"(OUT p_id bigint, IN p_model character varying, IN p_owner_id integer)
  RETURNS bigint AS
$BODY$

-- VITA:Generated;Tag=ident.Car/Insert
-- Description: Inserts a new entity.

BEGIN
    INSERT INTO "ident"."Car" 
  ("Model", "Owner_Id") 
  VALUES 
    (p_Model, p_Owner_Id) RETURNING "Id"
     INTO p_Id;
END;

$BODY$
  LANGUAGE plpgsql 
*/

}
