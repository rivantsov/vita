using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Entities.Utilities;
using Vita.Tools;
using Vita.Tools.Testing;

namespace Vita.Testing.BasicTests {

  class Program {

    public static int Main(string[] args) {
      Console.WindowWidth = 120;
      var noPause = args.Contains("/nopause");
      var runner = new TestRunner(errorFile: "_errors.log");
      Startup.InitAppConfig(); 
      var servTypesStr = Startup.AppConfig["ServerTypesForConsoleRun"];
      var serverTypes = StringHelper.ParseEnumList<DbServerType>(servTypesStr);
      int skipServerCount = 0;

      var testRuns = runner.RunTests(typeof(Program).Assembly, serverTypes,
        // init action for server type; must return true to run the tests
        servType => {
          Console.WriteLine("SERVER TYPE: " + servType);
          Startup.Reset(servType);
          // Check if server is available
          string error;
          if (DataUtility.TestConnection(Startup.Driver, Startup.ConnectionString, out error)) 
            return true; 
          runner.ConsoleWriteRed("  Connection test failed for connection string: {0}, \r\n   Error: {1}", Startup.ConnectionString, error);
          skipServerCount++;
          return false; 
        }, 
         null //finalizer
        );

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

  
  }
}
