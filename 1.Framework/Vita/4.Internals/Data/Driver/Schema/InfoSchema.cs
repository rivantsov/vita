using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using Vita.Entities.Utilities;
using Vita.Entities;

namespace Vita.Data.Driver.InfoSchema {
  // DataSet, DataTable, DataRow are not supported by .NET core. DbModelLoader returns meta data as tables/rows
  // so here are simple replacements
  // Update: DataSet is supported now, but we better go without it, it is too heavy
  public class InfoColumn {
    public readonly InfoTable Table;
    public string Name;
    public readonly Type DataType;
    public readonly int Index; 
    internal InfoColumn(InfoTable table, string name, Type dataType, int index) {
      Table = table;
      Name = name;
      DataType = dataType;
      Index = index;  
    }
  }

  public class InfoTable {
    public readonly IList<InfoColumn> Columns = new List<InfoColumn>();
    public IList<InfoRow> Rows = new List<InfoRow>();

    public InfoTable() { }

    public InfoTable (IDataReader reader) {
      for(int i=0; i < reader.FieldCount; i++) {
        AddColumn(reader.GetName(i), reader.GetFieldType(i));
      }
    }

    public void Load(IDataReader reader) {
      while(reader.Read()) {
        var row = AddRow(); 
        foreach(var col in Columns) {
          row[col.Index] = reader[col.Index];
        }
      }
    }

    public InfoColumn GetColumn(string name) {
      var col = FindColumn(name);
      Util.Check(col != null, "Column {0} not found.", name);
      return col; 
    }
    public InfoColumn FindColumn(string name) {
      name = name.ToLowerInvariant();
      return Columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    public InfoColumn AddColumn(string name, Type dataType) {
      var col = new InfoColumn(this, name, dataType, Columns.Count);
      Columns.Add(col);
      return col; 
    }
    public void AddColumns(string names, Type dataType = null) {
      dataType = dataType ?? typeof(string);
      var arrNames = names.Replace(" ", string.Empty).Split(',');
      foreach(var name in arrNames)
        AddColumn(name, dataType); 
    }

    public bool HasColumn(string name) {
      var col = FindColumn(name);
      return col != null; 
    }

    public InfoRow AddRow() {
      var row = new InfoRow(this);
      Rows.Add(row);
      return row; 
    }

    public InfoRow FindRow(string columnName, string value) {
      foreach(var row in this.Rows)
        if(value.Equals(row.GetAsString(columnName), StringComparison.OrdinalIgnoreCase))
          return row;
      return null; 
    }
   
  }

  public class InfoRow {
    public InfoTable Table;
    public object[] Values;

    internal InfoRow(InfoTable table) {
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
      return value + string.Empty; //safe ToString method 
    }

    public long GetAsLong(string name) {
      var value = this[name];
      if(value == DBNull.Value)
        return 0;
      return (long) Convert.ChangeType(value, typeof(long));
    }

    public int GetAsInt(string name, int nullValue = 0) {
      var value = this[name];
      if(value == DBNull.Value)
        return nullValue;
      return (int)Convert.ChangeType(value, typeof(int));
    }


    private InfoColumn GetColumn(string name) {
      var col = Table.GetColumn(name);
      if(col.Index >= Values.Length)
        Array.Resize(ref Values, Table.Columns.Count);
      return col; 
    }
    private InfoColumn GetColumn(int index) {
      var col = Table.Columns[index];
      if(col.Index >= Values.Length)
        Array.Resize(ref Values, Table.Columns.Count);
      return col;
    }

  }//class
}
