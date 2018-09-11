using System;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Data.Driver;

namespace Vita.Testing.BasicTests.LinqBasic {

  [TestClass]
  public class LinqBasicTests {
    EntityApp _app;

    #region entities, entity app
    [Entity]
    public interface IArithmData {
      [PrimaryKey, Auto]
      Guid Id { get; set; }

      int I1 { get; set; }
      int I2 { get; set; }
      int I3 { get; set; }
      int I4 { get; set; }

      double D1 { get; set; }
      double D2 { get; set; }
      double D3 { get; set; }
      double D4 { get; set; }
    }

    // We skip defining custom entity module and use base EntityModule class
    public class MiscLinqEntityApp : EntityApp {
      public const string Schema = "linq";
      public MiscLinqEntityApp() {
        var area = AddArea(Schema);
        var mainModule = new EntityModule(area, "MainModule");
        mainModule.RegisterEntities(typeof(IArithmData));
      }
    }//class

    #endregion 

    public void DeleteAll() {
      Startup.DeleteAll(_app,  typeof(IArithmData)); 
    }

    [TestInitialize]
    public void Init() {
      if(_app == null) {
        Startup.DropSchemaObjects(MiscLinqEntityApp.Schema);
        _app = new MiscLinqEntityApp();
        Startup.ActivateApp(_app);
      }
    }

    [TestCleanup]
    public void TestCleanup() {
      if(_app != null)
        _app.Flush();
    }


    [TestMethod]
    public void TestLinqPrecedence() {
      var session = _app.OpenSession();
      var data = session.EntitySet<IArithmData>();


      // Precedence tests 
      // test * vs +
      object result = data.Where(d => d.I1 + d.I2 * d.I3 + d.I4 > 0).ToList();
      var where = GetLastWhere(session);
      Assert.AreEqual("I1+I2*I3+I4>0", where, "WHERE clause mismatch.");
      // Other precedence cases
      result = data.Where(d => d.I1 + d.I2 * (d.I3 + d.I4) > 0).ToList();
      where = GetLastWhere(session);
      Assert.AreEqual("I1+I2*(I3+I4)>0", where, "WHERE clause mismatch.");

      result = data.Where(d => d.I1 - (d.I2 - d.I3) > 0).ToList();
      where = GetLastWhere(session);
      Assert.AreEqual("I1-(I2-I3)>0", where, "WHERE clause mismatch.");

      //TODO: write more for math functions, date functions, etc
    }

    private string GetLastWhere(IEntitySession session) {
      var cmd = session.GetLastCommand();
      var sql = cmd.CommandText;
      var wpos = sql.IndexOf("WHERE") + "Where".Length;
      var where = sql.Substring(wpos + 1);
      var result = where.Replace("\"", string.Empty)
                        .Replace(" ", string.Empty)
                        .Replace(Environment.NewLine, string.Empty)
                        .Replace(";", string.Empty)
                        .ToUpper();
      return result; 
    }
  }//class
}
