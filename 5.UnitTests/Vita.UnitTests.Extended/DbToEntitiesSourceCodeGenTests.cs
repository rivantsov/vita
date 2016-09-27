using System;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;

using Vita.Common;
using Vita.Entities;
using Vita.Data;
using Vita.Data.Model;
using Vita.Tools;
using Vita.Tools.DbFirst;
using Vita.Data.Driver;

namespace Vita.UnitTests.Extended {

  [TestClass]
  public class DbToEntitiesSourceCodeGenTests {

    [TestInitialize]
    public void TestInit() {
      Startup.InitApp();
    }

    [TestMethod]
    public void TestDbToEntitiesSourceGenerator() {
      var xmlConfigTemplate =
@"<Settings>
  <Provider>@Provider@</Provider>
  <ConnectionString>@ConnString@</ConnectionString>
  <OutputPath>_Generated_BookEntities_@Provider@.cs</OutputPath><!-- Will go into bin folder. -->
  <Namespace>Vita.Samples.BooksGenerated</Namespace>
  <AppClassName>BookStoreApp</AppClassName>
  <Schemas>books</Schemas>
  <Options>Binary16AsGuid, BinaryKeysAsGuid, AutoOnGuidPrimaryKeys, AddOneToManyLists,GenerateConsoleAppCode,UtcDates</Options>
  <AutoValues>CreatedOn:CreatedOn</AutoValues>
  <ForceDataTypes>@ForceDataTypes@</ForceDataTypes> <!-- for SQLite -->
</Settings>
";
      var sqliteForceTypes = "CreatedOn:System.DateTime,CreatedIn:System.Guid,UpdatedIn:System.Guid";
      var forceTypes = Startup.ServerType == DbServerType.Sqlite? sqliteForceTypes : string.Empty;
      var driver = Startup.Driver; 
      var xml = xmlConfigTemplate
        .Replace("@Provider@", Startup.ServerType.ToString())
        .Replace("@ConnString@", Startup.ConnectionString)
        .Replace("@ForceDataTypes@", forceTypes);
      var xmlConfig = new XmlDocument();
      xmlConfig.LoadXml(xml);

      var traceFbk = new TraceProcessFeedback();
      var dbfirst = new DbFirstProcessor(traceFbk);
      try {
        var success = dbfirst.GenerateEntityModelSources(xmlConfig);
        Assert.IsTrue(success, "Source generation failed.");
      } catch (Exception ex) {
        var err = ex.ToLogString();
        Debug.WriteLine(err);
        throw; 
      }
    }
  
  }//class
}
