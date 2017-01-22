using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;

namespace Vita.UnitTests.Common {

  public class TestRun {
    public object TestClassInstance;
    public TestClassInfo Info;
    public List<Exception> Errors = new List<Exception>();
  }

  public class TestRunner {
    public string ErrorLogFile;
    public bool WriteToConsole;
    public bool StopOnError;

    public TestRunner(bool writeToConsole = true, bool stopOnError = false, string errorFile = null) {
      ErrorLogFile = errorFile;
      WriteToConsole = writeToConsole;
      StopOnError = stopOnError;
    }

    public List<TestRun> RunTests(Assembly assembly) {
      ConsoleWriteLine("Running tests in assembly " + assembly.GetName().Name + " ...");
      var testRuns = new List<TestRun>(); 
      var types = assembly.GetTypes();
      foreach (var type in types)
        if (TestUtil.IsTestClass(type)) {
          var testRun = RunTests(type);
          testRuns.Add(testRun); 
        }
      ConsoleWriteLine("--------------------------------------------------------------");
      return testRuns; 
    }//method

    public TestRun RunTests(Type testClass) {
      var info = new TestClassInfo(testClass);
      ConsoleWrite("  running tests in " + testClass.Name);
      var testClassObj = Activator.CreateInstance(testClass);
      var testRun = new TestRun() { Info = info, TestClassInstance = testClassObj };
      if (info.Init != null)
        RunMethod(testRun, info.Init);
      var start = Environment.TickCount;
      foreach (var test in info.Tests) {
        RunMethod(testRun, test); 
      }
      var end = Environment.TickCount; 
      if (info.Cleanup != null)
        RunMethod(testRun, info.Cleanup);
      ConsoleUpdate("                                     "); //clear all info
      Console.CursorLeft = 50;
      ConsoleWriteLine("    " + info.Tests.Count + " test(s), " + testRun.Errors.Count + " error(s),    " + (end - start) + " ms");
      return testRun; 
    }

    public List<TestRun> RunTests<TData>(Assembly assembly, IEnumerable<TData> data, 
        Func<TData, bool> init, Action done) {
      var allRuns = new List<TestRun>();
      foreach (var dv in data) {
        if (init != null && !init(dv)) {
          ConsoleWriteLine("  Init func returned false, skipping tests.");
          continue;
        }
        var runs = RunTests(assembly);
        if(done != null)
          done(); 
        allRuns.AddRange(runs); 
      }
      return allRuns;
    }


    public bool RunMethod(TestRun testRun, MethodInfo method) {
      try {
        ConsoleUpdate("  Running " + method.Name + "...");
        method.Invoke(testRun.TestClassInstance, null);
        return true; 
      } catch (Exception ex) {
        testRun.Errors.Add(ex);
        var excStr = ex.ToLogString(); 
        LogWrite(excStr);
        Trace.WriteLine(excStr); 
        ConsoleWriteError(ex);
        return false; 
      }
    }//method

    public void ConsoleWriteLine(string message) {
      if (!WriteToConsole)
        return;
      Console.WriteLine(message); 
    }

    public void ConsoleWrite(string message) {
      if (!WriteToConsole)
        return;
      Console.Write(message);
    }

    public void ConsoleUpdate(string message) {
      if (!WriteToConsole)
        return;
      var top = Console.CursorTop;
      var left = Console.CursorLeft;
      Console.Write(message + "                                  ");
      Console.SetCursorPosition(left, top);
    }
    public void ConsoleWriteError(Exception ex) {
      if (!WriteToConsole)
        return;
      var saveColor = Console.ForegroundColor; 
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine();
      Console.WriteLine("================ ERRROR ====================================");
      Console.WriteLine(ex.Message);
      if (ex.InnerException != null)
        Console.WriteLine(ex.InnerException.Message);
      Console.ForegroundColor = saveColor;
      if (StopOnError) {
        Console.WriteLine("  Press any key to continue...");
        Console.ReadKey();
      }
    }
    public void ConsoleWriteRed(string message, params object[] args) {
      if (!WriteToConsole)
        return;
      var saveColor = Console.ForegroundColor;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine();
      message = StringHelper.SafeFormat(message, args); 
      Console.WriteLine(message);
      Console.ForegroundColor = saveColor;
    }

    public void LogWrite(string message) {
      if (string.IsNullOrEmpty(ErrorLogFile))
        return;
      try {
        System.IO.File.AppendAllText(ErrorLogFile, message);
      } catch (Exception ex) {
        Debug.WriteLine("Error writing exception to file log: " + ex.Message); 
      }
    }

  } //class
} //ns
