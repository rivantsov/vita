using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using Vita.Common;

namespace Vita.Data.Driver {
  // DataSet, DataTable, DataRow are not supported by .NET core. DbModelLoader returns meta data as tables/rows
  // so here are simple replacements
  public class DbColumn {
    public readonly DbTable Table;
    public readonly string Name;
    public readonly Type DataType;
    public readonly int Index; 
    internal DbColumn(DbTable table, string name, Type dataType, int index) {
      Table = table;
      Name = name;
      DataType = dataType;
      Index = index;  
    }
  }

  public class DbTable {
    public readonly IList<DbColumn> Columns = new List<DbColumn>();
    public readonly IList<DbRow> Rows = new List<DbRow>();

    public DbTable() { }

    public DbTable (IDataReader reader) {
      for(int i=0; i < reader.FieldCount; i++) {
        AddColumn(reader.GetName(i), reader.GetFieldType(i));
      }
    }

    public void Load(IDataReader reader) {
      while(reader.Read()) {
        var row = AddRow(); 
        foreach(var col in Columns) 
          row[col.Index] = reader[col.Index];
      }
    }

    public DbColumn GetColumn(string name) {
      var col = FindColumn(name);
      Util.Check(col != null, "Column {0} not found.", name);
      return col; 
    }
    public DbColumn FindColumn(string name) {
      name = name.ToLowerInvariant();
      return Columns.FirstOrDefault(c => c.Name == name);
    }
    public DbColumn AddColumn(string name, Type dataType) {
      var col = new DbColumn(this, name.ToLowerInvariant(), dataType, Columns.Count);
      Columns.Add(col);
      return col; 
    }

    public bool HasColumn(string name) {
      var col = FindColumn(name);
      return col != null; 
    }

    public DbRow AddRow() {
      var row = new DbRow(this);
      Rows.Add(row);
      return row; 
    }
   
  }

  public class DbRow {
    public DbTable Table;
    public object[] Values;

    internal DbRow(DbTable table) {
      Table = table;
      Values = new object[Table.Columns.Count];
    }
    public object this[string name] {
      get {
        var col = GetColumn(name);
        return Values[col.Index];
      }
      set {
        var col = GetColumn(name);
        Values[col.Index] = value; 
      }
    }
    public object this[int index] {
      get {
        var col = GetColumn(index);
        return Values[col.Index];
      }
      set {
        var col = GetColumn(index);
        Values[col.Index] = value;
      }
    }

    public string GetAsString(string name) {
      var value = this[name];
      if(value == DBNull.Value)
        return null;
      return (string)value; 
    }

    public long GetAsLong(string name) {
      var value = this[name];
      if(value == DBNull.Value)
        return 0;
      return (long) Convert.ChangeType(value, typeof(long));
    }

    public int GetAsInt(string name) {
      var value = this[name];
      if(value == DBNull.Value)
        return 0;
      return (int)Convert.ChangeType(value, typeof(int));
    }


    private DbColumn GetColumn(string name) {
      var col = Table.GetColumn(name);
      if(col.Index >= Values.Length)
        Array.Resize(ref Values, Table.Columns.Count);
      return col; 
    }
    private DbColumn GetColumn(int index) {
      var col = Table.Columns[index];
      if(col.Index >= Values.Length)
        Array.Resize(ref Values, Table.Columns.Count);
      return col;
    }

  }//class
}
