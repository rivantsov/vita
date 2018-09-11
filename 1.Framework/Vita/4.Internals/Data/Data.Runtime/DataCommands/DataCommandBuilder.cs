using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.SqlGen;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Runtime {

  public class DataCommandBuilder : ISqlValueFormatter {
    DbModel _dbModel; 
    DbDriver _driver;
    DbSqlDialect _sqlTemplates; 
    bool _batchMode;
    IDbCommand _dbCommand;
    SqlFormatOptions _formatOptions;
    public int SqlCount; 

    IList<BatchParamCopy> _paramCopyList; 
    IList<string> _sqlStrings = new List<string>();
    List<EntityRecord> _records = new List<EntityRecord>(); 

    public DataCommandBuilder(DbModel dbModel, bool batchMode = false, SqlFormatOptions formatOptions = SqlFormatOptions.Auto) {
      _dbModel = dbModel; 
      _driver = _dbModel.Driver;
      _sqlTemplates = _driver.SqlDialect; 
      _batchMode = batchMode;
      _formatOptions = formatOptions;
      _dbCommand = _driver.CreateCommand();
      if (_batchMode) //reserve spot#1 for Begin Trans command
        _sqlStrings.Add(string.Empty); 
    }

    public string GetSqlText() {
      return string.Join(string.Empty, _sqlStrings);
    }

    public  DataCommand CreateCommand(DataConnection connection, 
              DbExecutionType executionType, ISqlResultProcessor resultProcessor) {
      _dbCommand.CommandText = string.Join(string.Empty, _sqlStrings);
      var cmd = new DataCommand(connection, _dbCommand, executionType, resultProcessor, _records);
      _dbCommand = null;
      return cmd; 
    }

    public DataCommand CreateBatchCommand(DataConnection conn, bool encloseInTrans) {
      if(encloseInTrans) {
        // We have already reserved string #0, empty string is there; replace it with BeginTrans
        _sqlStrings[0] = _sqlTemplates.BatchBeginTransaction.Text;
        _sqlStrings.Add(SqlTerms.NewLine.Text);
        _sqlStrings.Add(_sqlTemplates.BatchCommitTransaction.Text);
      } 
      _dbCommand.CommandText = string.Join(string.Empty, _sqlStrings);
      var cmd = new DataCommand(conn, _dbCommand, DbExecutionType.NonQuery, null, _records, _paramCopyList);
      _dbCommand = null;
      return cmd;
    }

    public int ParameterCount => _dbCommand.Parameters.Count;

    public static void SetParamValues(IDbCommand cmd, object[] paramValues = null) {
      for(int i = 0; i < cmd.Parameters.Count; i++) {
        var p = (IDataParameter)cmd.Parameters[i];
        p.Value = paramValues[i] ?? DBNull.Value;
      }
    }
    /* for future, with SQL reuse
    public static void SetCrudParamValues(IDbCommand cmd, EntityRecord record, DbTableInfo table) {
      for(int i = 0; i < cmd.Parameters.Count; i++) {
        var prm = (IDataParameter)cmd.Parameters[i];
        var col = table.GetColumn(prm.SourceColumn);
        var v = record.GetValueDirect(col.Member);
        prm.Value = col.TypeInfo.PropertyToColumnConverter(v) ?? DBNull.Value;
      }
    }
    */

    public void AddSql(SqlStatement sql, IList<EntityRecord> records) {
      AddSql(sql);
      _records.AddRange(records); 
    }

    public void AddSql(SqlStatement sql, EntityRecord record) {
      var phStrings = FormatPlaceholders(sql, record);
      sql.WriteSql(_sqlStrings, phStrings);
      SqlCount++;
      _records.Add(record);
    }
    public void AddSql(SqlStatement sql) {
      var phStrings = FormatPlaceholders(sql, null);
      sql.WriteSql(_sqlStrings, phStrings);
      SqlCount++;
    }

    public void AddLinqSql(SqlStatement sql, object[] paramValues) {
      var phStrings = FormatLinqQueryPlaceholders(sql, paramValues);
      sql.WriteSql(_sqlStrings, phStrings);
      SqlCount++;
    }

    private IList<string> FormatPlaceholders(SqlStatement sql, EntityRecord record = null) {
      var phArgs = new string[sql.PlaceHolders.Count]; 
      // Placeholders are either of type: column-value-ref or parameters (returning identity or row version) 
      for(int i = 0; i < sql.PlaceHolders.Count; i++) {
        var ph = sql.PlaceHolders[i];
        string phArg = null; 
        switch(ph) {
          case SqlColumnRefPlaceHolder cph:
            Util.Check(record != null, 
                "Internal error: Invalid SqlStatement instance, SqlColumnRefPlaceHolder requires 'record' parameter.");
            // special case - referencing new identity
            if (_batchMode && record.EntityInfo.Flags.IsSet(EntityFlags.ReferencesIdentity) 
                 && CheckReferencesNewIdentity(record, cph.SourceColumn, out phArg)) {
              break;
            }
            var value = record.GetValue(cph.SourceColumn.Member);
            var colValue = cph.SourceColumn.TypeInfo.PropertyToColumnConverter(value); 
            phArg = AddParamOrLiteral(cph.SourceColumn.TypeInfo.StorageType, colValue);
            break;
          case SqlValueRefPlaceHolder vph:
            phArg = AddParamOrLiteral(vph.TypeDef, vph.Value); 
            break;
          case SqlParamPlaceHolder pph:
            var prm = AddParameter(pph.TypeDef, pph.Direction, null, pph.TargetColumn);
            if (pph.Direction != ParameterDirection.Input && record != null) {
              AssociateOutputParameterWithRecord(prm, pph.TargetColumn, record);
            }
            phArg = prm.ParameterName;
            break;
          case SqlArrayValuePlaceHolder avph:
            // TODO: refactor this crap, allow parameter and SQL reuse
            // this happens only for DeleteMany, ReadValue returns the array
            var arr = avph.ReadValue(null);  
            phArg = _dbModel.Driver.TypeRegistry.GetListLiteral(arr);
            break;
          default:
            Util.Throw($"Unexpected SQL placeholder type: {ph?.GetType()}");
            break; 
        } //switch ph
        phArgs[i] = phArg; 
      } //foreach
      return phArgs; 
    }

    private IList<string> FormatLinqQueryPlaceholders(SqlStatement sql, object[] paramValues) {
      var phStrings = new List<string>();
      // Make Selects reusable, so use parameter preferrably
      foreach (SqlPlaceHolder ph in sql.PlaceHolders) {
        switch(ph) {
          case SqlLinqParamPlaceHolder lph:
            var value = lph.ReadValue(paramValues);
            var phStr = AddParamOrLiteral(lph.TypeDef, value); 
            // var prm = AddParameter(lph.TypeInfo, ParameterDirection.Input, value);
            // var phStr = prm.ParameterName; 
            phStrings.Add(phStr); 
            break;
          case SqlArrayValuePlaceHolder avph:
            var arr = avph.ReadValue(paramValues);
           // phStr = AddParamOrLiteral(avph.)
            phStr = _dbModel.Driver.TypeRegistry.GetListLiteral(arr);
            phStrings.Add(phStr);
            break; 
          default:
            Util.Throw($"Unexpected SQL placeholder type: {ph?.GetType()}");
            break;
        }
      }
      return phStrings;
    }

    private IDataParameter AddParameter(DbStorageType typeDef, ParameterDirection direction, object value, DbColumnInfo targetColumn = null) {
      var prmName = _dbModel.GetSqlParameterName(_dbCommand.Parameters.Count); 
      var prm = _driver.AddParameter(_dbCommand, prmName, typeDef, direction, value);
      return prm; 
    }

    private void AssociateOutputParameterWithRecord(IDataParameter prm, DbColumnInfo column, EntityRecord rec) {
      if(rec.DbCommandData == null)
        rec.DbCommandData = new EntityRecordDBCommandData() { DbCommand = _dbCommand };
      rec.DbCommandData.OutputParameters.Add(new OutParamInfo() { Parameter = prm, Column = column });
      // Save identity parameter in dedicated field
      if (column != null && column.Flags.IsSet(DbColumnFlags.Identity))
        rec.DbCommandData.IdentityParameter = prm; 
    }

    private string AddParamOrLiteral(DbStorageType typeDef, object value) {
      var useParam = !_formatOptions.IsSet(SqlFormatOptions.NoParameters) &&
                (_formatOptions.IsSet(SqlFormatOptions.PreferParam) || ShouldUseParameterFor(typeDef, value));
      if (useParam) {
        var convValue = typeDef.ConvertToTargetType(value); //.PropertyToColumnConverter(value);  
        return AddParameter(typeDef, ParameterDirection.Input, convValue).ParameterName;
      } else {
        if(value == null || value == DBNull.Value)
          return SqlTerms.Null.Text;
        //return typeInfo.ToLiteral(value);
        return typeDef.ValueToLiteral(value);
      }
    }

    private bool CheckReferencesNewIdentity(EntityRecord rec, DbColumnInfo fkCol, out string idParamName) {
      idParamName = null;
      if(!rec.CheckReferencesNewIdentity(fkCol, out EntityRecord targetRec))
        return false; 
      Util.Check(targetRec.DbCommandData != null,
        "Fatal error: the target record of FK column {0} does not have {1} field set. Fault in record sequencing.",
           fkCol.ColumnName, nameof(EntityRecord.DbCommandData));
      // check if identity parameter belongs to the same data command - the case for very large batches that contaim multiple 
      // DbCommands (each containing mltiple update SQLs). It may happen that parameter returning identity is in different (prior)
      // DbCommand. Note: parameters cannot be reused accross DB commands 
      var idPrm = GetIdentityParameterOrCopy(targetRec, fkCol);
      idParamName = idPrm.ParameterName;
      return true;
    }

    private IDataParameter GetIdentityParameterOrCopy(EntityRecord targetRecord, DbColumnInfo fkCol) {
      var cmdData = targetRecord.DbCommandData; 
      Util.Check(cmdData != null, 
        "Fatal: insert-command-data not set for a record, cannot retrieve parameter returning identity.");
      if(cmdData.DbCommand == this._dbCommand) {
        // use the same parameter
        return cmdData.IdentityParameter;
      } else {
        // different command - create new parameter and add copy action
        var newPrm = AddParameter(fkCol.TypeInfo.StorageType, ParameterDirection.Input, null);
        _paramCopyList = _paramCopyList ?? new List<BatchParamCopy>();
        _paramCopyList.Add(new BatchParamCopy() { From = cmdData.IdentityParameter, To = newPrm });
        return newPrm;
      }
    }

    // ISqlValueFormatter implementation
    public SqlFragment FormatValue(DbStorageType typeDef, object value, SqlFormatOptions options) {
      string strV; 
      if(options.IsSet(SqlFormatOptions.PreferParam) || ShouldUseParameterFor(typeDef, value)) {
        strV = AddParameter(typeDef, ParameterDirection.Input, value).ParameterName;
      } else {
        if (value == null || value == DBNull.Value)
          return SqlTerms.Null;
        strV = typeDef.ValueToLiteral(value);
      }
      return new TextSqlFragment(strV); 
    }

    public SqlFragment FormatValue(EntityRecord record, DbColumnInfo column, SqlFormatOptions options) {
      if(_batchMode && CheckReferencesNewIdentity(record, column, out string paramName)) {
        return new TextSqlFragment(paramName); 
      }
      var value = record.GetValueDirect(column.Member);
      var colValue = column.TypeInfo.PropertyToColumnConverter(value);
      return FormatValue(column.TypeInfo.StorageType, colValue, options); 
    }

    public virtual bool ShouldUseParameterFor(DbStorageType typeDef, object value) {
      switch(value) {
        case DBNull dbNull:
          return false;
        case string str:
          return str.Length > _driver.SqlDialect.MaxLiteralLength;
        case byte[] bytes:
          return bytes.Length > _driver.SqlDialect.MaxLiteralLength;
        default:
          if(typeDef.IsList)
            return true;
          return false;
      }
    }



  } //class
}
