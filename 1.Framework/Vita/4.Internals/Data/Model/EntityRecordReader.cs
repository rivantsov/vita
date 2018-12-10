using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Data.Model;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities;

namespace Vita.Data.Model {

  // Encodes information for creating entities from data records; used in CRUD select statements and in LINQ queries
  public class EntityRecordReader {
    #region OutColumMapping nested class
    [DebuggerDisplay("{DbColumn},#{ReaderColumnIndex}")]
    public class OutColumnMapping {
      public DbColumnInfo DbColumn;
      public int ReaderColumnIndex;
    }
    #endregion

    public static MethodInfo ReadMethodInfo;
    public Type EntityType;

    DbTableInfo _tableInfo;
    //Primary key columns are used for checking if entire entity is null (entity comes from null reference in join query)
    List<OutColumnMapping> _primaryKeyColumns = new List<OutColumnMapping>();
    List<OutColumnMapping> _columns = new List<OutColumnMapping>();

    static EntityRecordReader() {
      ReadMethodInfo = typeof(EntityRecordReader).GetMethod("ReadEntity", BindingFlags.Instance | BindingFlags.Public);
    }

    public EntityRecordReader(DbTableInfo tableInfo) {
      _tableInfo = tableInfo;
      EntityType = _tableInfo.Entity.EntityType; 
    }

    public OutColumnMapping AddColumn(DbColumnInfo column, int readerColumnIndex = -1) {
      if (readerColumnIndex == -1)
        readerColumnIndex = _columns.Count;
      var colMapping = new OutColumnMapping() { ReaderColumnIndex = readerColumnIndex, DbColumn = column };
      _columns.Add(colMapping);
      if (column.Flags.IsSet(DbColumnFlags.PrimaryKey))
        _primaryKeyColumns.Add(colMapping);
      return colMapping; 
    }

    // Used for efficient reading entities in linq queries
    public object ReadEntity(IDataRecord dataRecord, EntitySession session) {
      var entRec = ReadRecord(dataRecord, session);
      if (entRec == null)
        return null;
      return entRec.EntityInstance;
    }

    public EntityRecord ReadRecord(IDataRecord dataRecord, EntitySession session) {
      // Some outer join queries may produce entities that are null; so we first try to read Primary key values - if they're all null, we return null. 
      if (_primaryKeyColumns.Count > 0 && PrimaryKeyIsNull(dataRecord))
        return null;
      var entRec = new EntityRecord(_tableInfo.Entity, EntityStatus.Loading);
      object dbValue = null;
      OutColumnMapping colMap = null; 
      //for-i loop is more efficient than foreach
      for (int i = 0; i < _columns.Count; i++) {
        try {
          colMap = _columns[i];
          var isNull = dataRecord.IsDBNull(colMap.ReaderColumnIndex);
          if(isNull)
            dbValue = DBNull.Value;
          else {
            dbValue = colMap.DbColumn.TypeInfo.ColumnReader(dataRecord, colMap.ReaderColumnIndex);
            var conv = colMap.DbColumn.Converter.ColumnToProperty;
            if(dbValue != null && conv != null)
              dbValue = conv(dbValue);
          }
          var member = colMap.DbColumn.Member;
          entRec.ValuesOriginal[member.ValueIndex] = dbValue; 
        } catch (Exception ex) {
          ex.AddValue("DataRecord", dataRecord);
          ex.AddValue("ColumnName", colMap.DbColumn.ColumnName);
          ex.AddValue("DbValue", dbValue);
          throw;
        }

      }
      var sessionRec = session.Attach(entRec); //might return different, previously loaded record
      return sessionRec;
    }

    private bool PrimaryKeyIsNull(IDataRecord record) {
      //for-i loop is more efficient than foreach
      for (int i = 0; i < _primaryKeyColumns.Count; i++) {
        var v = record[_primaryKeyColumns[i].ReaderColumnIndex];
        if (v != DBNull.Value)
          return false; 
      }
      return true; 
    }

  }//class
}
