using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Vita.Data.Driver;
using Vita.Entities.Utilities;

namespace Vita.Testing.ExtendedTests {

  public static class TestRunConfigLoader {
    internal static TestRunConfig LoadForTestExplorer(IConfigurationRoot appSettings) {
      var servTypeStr = appSettings["ServerTypeForTestExplorer"];
      DbServerType servType;
      if(!Enum.TryParse<DbServerType>(servTypeStr, out servType))
        servType = DbServerType.MsSql;
      //var enableCache = appSettings["EnableCacheForTestExplorer"] == "true";
      var useBatchMode = appSettings["UseBatchModeForTestExplorer"] == "true";
      var connString = appSettings[servTypeStr + "ConnectionString"];
      var logConnString = appSettings[servTypeStr + "LogConnectionString"] ?? connString;
      connString = ReplaceBinFolderToken(connString);
      logConnString = ReplaceBinFolderToken(logConnString); 
      return new TestRunConfig() {
        ServerType = servType, //EnableCache = enableCache,
        UseBatchMode = useBatchMode, ConnectionString = connString, LogConnectionString = logConnString
      };
    } //method

    internal static List<TestRunConfig> LoadForConsoleRun(IConfigurationRoot appSettings) {
      var configs = new List<TestRunConfig>();
      var servTypesStr = appSettings["ServerTypesForConsoleRun"];
      var serverTypes = StringHelper.ParseEnumList<DbServerType>(servTypesStr);
      foreach(var servType in serverTypes) {
        var connString = appSettings[servType + "ConnectionString"];
        var logConnString = appSettings[servType + "LogConnectionString"] ?? connString;
        connString = ReplaceBinFolderToken(connString);
        logConnString = ReplaceBinFolderToken(logConnString);
        configs.Add(new TestRunConfig() {
          ServerType = servType, ConnectionString = connString, LogConnectionString = logConnString, UseBatchMode = false
        });
        if(servType == DbServerType.SQLite)
          continue;
        configs.Add(new TestRunConfig() {
          ServerType = servType, ConnectionString = connString, LogConnectionString = logConnString, UseBatchMode = true
        });
      }
      return configs;
    }

    private static string ReplaceBinFolderToken(string value) {
      if(value == null)
        return null;
      if(value.Contains("{bin}")) {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        var binFolder = Path.GetDirectoryName(asmPath);
        value = value.Replace("{bin}", binFolder);
      }
      return value;
    }

  } //class
}
