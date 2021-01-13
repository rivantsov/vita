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

namespace Vita.Testing.ExtendedTests {
  class Program {

    public static int Main(string[] args) {
      Console.WindowWidth = 120;
      var noPause = args.Contains("/nopause");
      Startup.InitAppSettings(); 
      var configs = TestRunConfigLoader.LoadForConsoleRun(Startup.AppSettings);
      var runner = new TestRunner(errorFile: "_errors.log");
      int skipServerCount = 0;

      var testRuns = runner.RunTests(typeof(Program).Assembly, configs,
        // init action for server type; must return true to run the tests
        cfg => {
          Console.WriteLine(); 
          Console.WriteLine(cfg.ToString());
          Startup.Reset(cfg);
          // Check if server is available
          string error;
          var connString = Startup.CurrentConfig.ConnectionString; 
          if (DataUtility.TestConnection(Startup.Driver, connString, out error))
            return true; 
          runner.ConsoleWriteRed("  Connection test failed for connection string: {0}, \r\n   Error: {1}", connString, error);
          skipServerCount++;
          return false; 
        },
        //finalizer: shutdown app; important - stop timers activity
        () => { Startup.BooksApp.Shutdown(); } 
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
