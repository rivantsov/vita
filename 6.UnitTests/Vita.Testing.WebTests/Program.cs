using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.UnitTests.Common;

namespace Vita.UnitTests.WebTests {
  class Program {
    // For now runs only for MsSql2008 (or 2012)
    public static int Main(string[] args) {
      Console.WindowWidth = 120;
      var noPause = args.Contains("/nopause");
      var runner = new TestRunner(errorFile: "_errors.log");

      var testRuns = runner.RunTests(typeof(Program).Assembly);

      //Report results
      var errCount = testRuns.Sum(tr => tr.Errors.Count);
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
