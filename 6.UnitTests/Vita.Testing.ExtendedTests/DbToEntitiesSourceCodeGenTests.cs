﻿using System;
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

using Vita.Entities;
using Vita.Tools;
using Vita.Tools.DbFirst;
using Vita.Data.Driver;

namespace Vita.Testing.ExtendedTests {

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
  <TableNames></TableNames> <!-- Explicit table list - use it in Oracle, we add it directly in config object below -->
</Settings>
";
      var sqliteForceTypes = "CreatedOn:System.DateTime,CreatedIn:System.Guid,UpdatedIn:System.Guid";
      var forceTypes = Startup.ServerType == DbServerType.SQLite? sqliteForceTypes : string.Empty;
      var driver = Startup.Driver;
      var xml = xmlConfigTemplate
        .Replace("@Provider@", Startup.ServerType.ToString())
        .Replace("@ConnString@", Startup.CurrentConfig.ConnectionString)
        .Replace("@ForceDataTypes@", forceTypes);
      var xmlConfig = new XmlDocument();
      xmlConfig.LoadXml(xml);
      var dbFirstConfig = new DbFirstConfig(xmlConfig);

      // For all servers except Oracle you don't need to specify explicit table list, loader will load all there and that's fine
      // Oracla had hundreds of system tables there in empty database, and it is impossible to separate these 
      //  so the easiest way is to provide table list explicitly
      if(driver.ServerType == DbServerType.Oracle) {
        var tableList = "Book,Publisher,Author,BookAuthor,BookReview,User,BookOrder,BookOrderLine,Coupon,Image," +
                     "DbInfo,DbModuleInfo,Login,SecretQuestion,SecretQuestionAnswer,TrustedDevice,LoginExtraFactor,LoginProcess";
          dbFirstConfig.TableNames.AddRange(tableList.Split(',')); 
      }

      var traceFbk = new TraceProcessFeedback();
      var dbfirst = new DbFirstProcessor(traceFbk);
      try {
        var success = dbfirst.GenerateEntityModelSources(dbFirstConfig);
        Assert.IsTrue(success, "Source generation failed.");
      } catch (Exception ex) {
        var err = ex.ToLogString();
        Debug.WriteLine(err);
        throw; 
      }
    }
  
  }//class
}