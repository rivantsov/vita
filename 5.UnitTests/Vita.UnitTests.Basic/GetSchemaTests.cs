using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data;
using Vita.Data;
using Vita.Data.Driver;

namespace Vita.UnitTests.Basic {
  [TestClass]
  public class GetSchemaTests {
    string _fileName;

    [TestMethod]
    public void TestGetSchema() {
      _fileName = "_schemas_" + SetupHelper.ServerType + ".log";
      System.IO.File.Delete(_fileName);
      WriteLine("--------------------------------------------------------------------------------------------------");
      WriteLine("Server type: " + SetupHelper.ServerType + Environment.NewLine);
      var dbStt = new DbSettings(SetupHelper.Driver, SetupHelper.DbOptions, SetupHelper.ConnectionString); 
      var conn = dbStt.ModelConfig.Driver.CreateConnection(SetupHelper.ConnectionString);

      //get col list
      var colList = GetCollectionNames(conn);

      //Report each collection
      foreach(var colName in colList) {
        WriteLine("Collection: " + colName);
        var dt = GetCollection(conn, colName);
        WriteLine("Columns: " + GetColumnNames(dt));

      }

    }

    private IList<string> GetCollectionNames(IDbConnection conn) {
      if(SetupHelper.ServerType == DbServerType.Postgres) {
        return new string[] { 
                               "Columns", "Databases", "ForeignKeys", "Indexes", "Tables", "IndexColumns", "Views"};

      }
      var dt = GetCollection(conn, "MetaDataCollections");
      WriteLine("Collection: " + "MetaDataCollection");
      WriteLine("Columns: " + GetColumnNames(dt));
      //get col list
      var colList = new List<string>();
      foreach(DataRow row in dt.Rows)
        colList.Add(row[0].ToString());
      colList.Sort();
      return colList;
    }

    private DataTable GetCollection(IDbConnection conn, string colName) {
      var conType = conn.GetType();
      var method = conType.GetMethod("GetSchema", new Type[] {typeof(string)});
      try {
        conn.Open();
        var result = method.Invoke(conn, new object[] { colName });
        var dt = (DataTable)result;
        return dt;
      } finally {
        conn.Close(); 
      }
    }

    private string GetColumnNames(DataTable table) {
      var list = new List<string>();
      foreach(DataColumn col in table.Columns)
        list.Add(col.ColumnName);
      return string.Join(", ", list);      
    }

    private void WriteLine(string text) {
      System.IO.File.AppendAllText(_fileName, text+Environment.NewLine);
      
    }

  }
}
