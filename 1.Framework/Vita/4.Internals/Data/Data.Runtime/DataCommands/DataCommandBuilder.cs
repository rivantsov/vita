using System;
using System.Collections;
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
using Vita.Entities.Utilities;

namespace Vita.Data.Runtime {

  public enum SqlGenMode {
    //Auto = 0,
    NoParameters,
    PreferParam,
    PreferLiteral,
  }

  public interface IColumnValueFormatter {
    string FormatColumnValuePlaceHolder(SqlColumnValuePlaceHolder ph, EntityRecord record);
  }

  public class DataCommandBuilder : IColumnValueFormatter {
    DbModel _dbModel; 
    DbDriver _driver;
    DbSqlDialect _sqlDialect; 
    IDbCommand _dbCommand;
    bool _batchMode; 
    SqlGenMode _genMode;
    public int SqlCount; 

    IList<BatchParamCopy> _paramCopyList; 
    IList<string> _sqlStrings = new List<string>();
    List<EntityRecord> _records = new List<EntityRecord>();
    int _maxLiteralLength;

    public int ParameterCount => _dbCommand.Parameters.Count;


    public DataCommandBuilder(DbModel dbModel, bool batchMode = false, SqlGenMode mode = SqlGenMode.PreferParam) {
      _dbModel = dbModel; 
      _driver = _dbModel.Driver;
      _sqlDialect = _driver.SqlDialect;
      _batchMode = batchMode; 
      _genMode = mode;
      if(batchMode)
        _genMode = SqlGenMode.PreferLiteral;
      _maxLiteralLength = _driver.SqlDialect.MaxLiteralLength; 
      _dbCommand = _driver.CreateCommand();
      //reserve spots: #0 for batch-begin (BEGIN; in ORACLE); #2 for Begin Trans
      _sqlStrings.Add(string.Empty);
      _sqlStrings.Add(string.Empty);
    }

    public  DataCommand CreateCommand(DataConnection connection, 
              DbExecutionType executionType, IDataCommandResultProcessor resultProcessor) {
      _dbCommand.CommandText = string.Join(string.Empty, _sqlStrings);
      var cmd = new DataCommand(connection, _dbCommand, executionType, resultProcessor, _records);
      _dbCommand = null;
      return cmd; 
    }

    public DataCommand CreateBatchCommand(DataConnection conn, bool encloseInTrans) {
      // we have two spots reserved in _sqlStrings list
      if (SqlCount > 1)
        _sqlStrings[0] = _sqlDialect.BatchBegin.Text;
      if(encloseInTrans) {
        // We have already reserved string #0, empty string is there; replace it with BeginTrans
        _sqlStrings[1] = _sqlDialect.BatchBeginTransaction.Text;
        _sqlStrings.Add(SqlTerms.NewLine.Text);
        _sqlStrings.Add(_sqlDialect.BatchCommitTransaction.Text);
      }
      if (SqlCount > 1)
      _sqlStrings.Add(_sqlDialect.BatchEnd.Text);
      _dbCommand.CommandText = string.Join(string.Empty, _sqlStrings);
      var cmd = new DataCommand(conn, _dbCommand, DbExecutionType.NonQuery, null, _records, _paramCopyList);
      _dbCommand = null;
      return cmd;
    }

    public string GetSqlText() {
      return string.Join(string.Empty, _sqlStrings);
    }

    public void AddLinqStatement(SqlStatement sql, object[] args) {
      AddStatement(sql, args);
    }

    public void AddUpdate(SqlStatement sql, EntityRecord rec) {
      _records.Add(rec);
      AddStatement(sql, rec); 
    }
    public void AddUpdates(SqlStatement sql, IList<EntityRecord> recs) {
      _records.AddRange(recs);
      AddStatement(sql, recs);
    }
    // Used by DeleteMany, arg is list of Ids
    public void AddUpdates(SqlStatement sql, IList<EntityRecord> recs, object arg) {
      _records.AddRange(recs);
      AddStatement(sql, arg);
    }

    private void AddStatement(SqlStatement sql, object args) {
      SqlCount++;
      var phArgs = new string[sql.PlaceHolders.Count];
      for(var i = 0; i < phArgs.Length; i++)
        phArgs[i] = FormatPlaceHolder(sql.PlaceHolders[i], args);
      sql.WriteSql(_sqlStrings, phArgs);
    }

    private string FormatPlaceHolder(SqlPlaceHolder placeHolder, object arg) {
      switch(placeHolder) {
        case SqlColumnValuePlaceHolder colPh:
          return FormatColumnValuePlaceHolder(colPh, (EntityRecord)arg);
        case SqlLinqParamPlaceHolder paramPh:
          return FormatLinqPlaceHolder(paramPh, arg);
        case SqlListParamPlaceHolder listPh:
          return FormatListPlaceHolder(listPh, arg);
        default:
          Util.Throw($"Unexpected SQL placeholder type {placeHolder.GetType()}");
          return null; 
      }
    }

    public string FormatColumnValuePlaceHolder(SqlColumnValuePlaceHolder cph, EntityRecord rec) {
      object memberValue = null;
      object colValue = null; 
      // If it is output, it must be parameters
      if (cph.ParamDirection != ParameterDirection.Input) {
        if(cph.ParamDirection == ParameterDirection.InputOutput) //do we need initial value
          memberValue = rec.GetValueDirect(cph.Column.Member);
        else
          memberValue = cph.Column.Member.DefaultValue; //this is important to setup prm.DbType for output parameter
        colValue = cph.Column.Converter.PropertyToColumn(memberValue); 
        return AddParameter(cph, colValue, rec).ParameterName;
      }
      // special case: column references new identity value for record inserted in the same transaction 
      if(_batchMode && CheckReferencesNewIdentity(rec, cph, out string prmName))
          return prmName;
      // get value and check if we can use literal 
      memberValue = rec.GetValueDirect(cph.Column.Member);
      colValue = cph.Column.Converter.PropertyToColumn(memberValue);
      if(_genMode == SqlGenMode.PreferLiteral && CanUseLiteral(memberValue, cph.Column))
          return FormatAsLiteral(cph, colValue);
      // use parameter
      return AddParameter(cph, colValue, rec).ParameterName;
    }

    private string FormatLinqPlaceHolder(SqlLinqParamPlaceHolder ph, object arg) {
      var value = ph.ValueReader((object[])arg);
      var dbValue = ph.ValueToDbValue(value) ?? DBNull.Value; //move it into converters? currently no-conv does not do this
      var useLiteral = _genMode == SqlGenMode.NoParameters || (_genMode == SqlGenMode.PreferLiteral && CanUseLiteral(dbValue));
      if(useLiteral)
        return FormatAsLiteral(ph, dbValue);
      else {
        var prm = _sqlDialect.AddDbParameter(_dbCommand, ph, dbValue); 
        return ph.FormatParameter(prm);
      }
    }

    private string FormatListPlaceHolder(SqlListParamPlaceHolder ph, object arg) {
      // read value from locals (linq); for DeleteMany: read list of IDs from list of records
      var list = ph.ListValueReader((object[])arg);
      var useLiteral = !_driver.Supports(DbFeatures.ArrayParameters) || _genMode == SqlGenMode.NoParameters;
      if(useLiteral) {
        return ph.FormatLiteral(list);
      } else {
        // convert to value that should be put in parameter; MSSQL: list of SqlDataRecord-s
        var prmValue = ph.ListToDbParamValue(list);
        var prm = _sqlDialect.AddDbParameter(_dbCommand, ph, prmValue); 
        return ph.FormatParameter(prm);
      }
    }

    private string FormatAsLiteral(SqlPlaceHolder ph, object value) {
      if(value == null || value == DBNull.Value)
        return SqlTerms.Null.Text; 
      if(ph.FormatLiteral == null) {
        var stype = _driver.TypeRegistry.GetDbTypeDef(value.GetType());
        ph.FormatLiteral = stype.ToLiteral;
      }
      return ph.FormatLiteral(value); 
    }

    private IDataParameter AddParameter(SqlColumnValuePlaceHolder cph, object colValue, EntityRecord record) {
      var prm = _sqlDialect.AddDbParameter(_dbCommand, cph, colValue); 
      if(prm.Direction != ParameterDirection.Input) {
        record.DbCommandData = record.DbCommandData ?? new EntityRecordDBCommandData() { DbCommand = _dbCommand };
        record.DbCommandData.OutputParameters.Add(new OutParamInfo() { Parameter = prm, Column = cph.Column });
      }
      return prm; 
    }

    private IDataParameter AddParameter(SqlPlaceHolder ph, object value) {
      return _sqlDialect.AddDbParameter(_dbCommand, ph, value); 
    }

    // identity/output parameters handling ===========================================================================
    // called only non-batch mode
    public bool CheckReferencesNewIdentity(EntityRecord rec, SqlColumnValuePlaceHolder cph, out string idParamName) {
      idParamName = null;
      if(!rec.CheckReferencesNewIdentity(cph.Column, out EntityRecord targetRec))
        return false;
      var targetCmdData = targetRec.DbCommandData; 
      Util.Check(targetCmdData != null, "Fatal error: the target record of FK column {0} does not have {1} field set. " + 
             "Fault in record sequencing.",  cph.Column.ColumnName, nameof(EntityRecord.DbCommandData));
      var idPrmInfo = targetCmdData.OutputParameters.First(op => op.Column.Flags.IsSet(DbColumnFlags.Identity));
      Util.Check(idPrmInfo != null, "Fatal error: the identity parameter is not found in referenced target record. " + 
                                        "Fault in record sequencing.");

      // Batch mode: check if identity parameter belongs to the same data command - the case for very large batches that contaim multiple 
      // DbCommands (each containing mltiple update SQLs). It may happen that parameter returning identity is in different (prior)
      // DbCommand. Note: parameters cannot be reused accross DB commands 
      if(targetRec.DbCommandData.DbCommand == this._dbCommand) {
        // use the same parameter
        idParamName = idPrmInfo.Parameter.ParameterName;
        return true;
      }
      // different batch command. Create new parameter and copy action 
      var newPrm = AddParameter(cph, 0, rec);
      idParamName = newPrm.ParameterName;
      _paramCopyList = _paramCopyList ?? new List<BatchParamCopy>();
      _paramCopyList.Add(new BatchParamCopy() { From = idPrmInfo.Parameter, To = newPrm });
      return true; 
    }

    public virtual bool CanUseLiteral(object value, DbColumnInfo column = null) {
      if(value == null || value == DBNull.Value)
        return true;
      if(column != null && !column.Flags.IsSet(DbColumnFlags.UseParamForLongValues))
        return true;
      switch(value) {
        case string str:
          return str.Length <= _maxLiteralLength;
        case byte[] bytes:
          return bytes.Length <= _maxLiteralLength;
        case Binary bin:
          return bin.Length <= _maxLiteralLength;
        default:
          return true;
      }
    } //method

  } //class
}
