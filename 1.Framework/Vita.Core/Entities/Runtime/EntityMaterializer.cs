using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Model;
using Vita.Entities.Model;

namespace Vita.Entities.Runtime {

  // Encodes information for creating entities from data records; used in CRUD select statements and in LINQ queries
  public class EntityMaterializer {
    #region OutColumMapping nested class
    public class OutColumnMapping {
      public DbColumnInfo DbColumn;
      public int ReaderColumnIndex;
    }
    #endregion

    public static MethodInfo ReadMethodInfo;

    DbTableInfo _tableInfo;
    //Primary key columns are used for checking if entire entity is null (entity comes from null reference in join query)
    List<OutColumnMapping> _primaryKeyColumns = new List<OutColumnMapping>();
    List<OutColumnMapping> _columns = new List<OutColumnMapping>();

    static EntityMaterializer() {
      ReadMethodInfo = typeof(EntityMaterializer).GetMethod("ReadEntity", BindingFlags.Instance | BindingFlags.Public);
    }

    public EntityMaterializer(DbTableInfo tableInfo) {
      _tableInfo = tableInfo;
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
          dbValue = dataRecord[colMap.ReaderColumnIndex];
          //System.Diagnostics.Debug.WriteLine(colMap.DbColumn.ColumnName + " " + dbValue + "(" + dbValue.GetType() + ")");
          var conv = colMap.DbColumn.TypeInfo.ColumnToPropertyConverter;
          if(dbValue != null && conv != null)
            dbValue = conv(dbValue);
          entRec.ValuesOriginal[colMap.DbColumn.Member.ValueIndex] = dbValue;
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
