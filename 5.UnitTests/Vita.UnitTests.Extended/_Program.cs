using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Common;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Tools;
using Vita.UnitTests.Common;

namespace Vita.UnitTests.Extended {
  class Program {

    public static int Main(string[] args) {
      Console.WindowWidth = 120;
      var noPause = args.Contains("/nopause");
      var runner = new TestRunner(errorFile: "_errors.log");
      var configs = BuildServerConfigs(); 
      int skipServerCount = 0;

      var testRuns = runner.RunTests(typeof(Program).Assembly, configs,
        // init action for server type; must return true to run the tests
        cfg => {
          Console.WriteLine(); 
          Console.WriteLine(cfg.ToString());
          SetupHelper.Reset(cfg);
          // Check if server is available
          string error;
          if (ToolHelper.TestConnection(SetupHelper.Driver, SetupHelper.ConnectionString, out error)) return true; 
          runner.ConsoleWriteRed("  Connection test failed for connection string: {0}, \r\n   Error: {1}", SetupHelper.ConnectionString, error);
          skipServerCount++;
          return false; 
        });

      //Report results
      var errCount = testRuns.Sum(tr => tr.Errors.Count) + skipServerCount;
      Console.WriteLine();
      if (errCount > 0)
        runner.ConsoleWriteRed("Errors: " + errCount);
      // stop unless there's /nopause switch
      if (!noPause) {
        Console.WriteLine("Press any key...");
        Console.ReadKey();
      }
      return errCount == 0 ? 0 : -1;
    }


    private static List<TestConfig> BuildServerConfigs() {
      var configs = new List<TestConfig>();
      var servTypesStr = ConfigurationManager.AppSettings["ServerTypesForConsoleRun"];
      var serverTypes = TestUtil.ParseServerTypes(servTypesStr);
      foreach (var servType in serverTypes) {
        configs.Add(new TestConfig() { ServerType = servType, UseStoredProcs = false, UseBatchMode = false, EnableCache = false});
        if (servType == DbServerType.SqlCe || servType == DbServerType.Sqlite)
          continue; 
        configs.Add(new TestConfig() { ServerType = servType, UseStoredProcs = true, UseBatchMode = false, EnableCache = false });
        configs.Add(new TestConfig() { ServerType = servType, UseStoredProcs = true, UseBatchMode = true, EnableCache = true });
      }
      return configs; 
    }
  
  }
}
