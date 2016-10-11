using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Vita.Common;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Tools.DbFirst;
using Vita.Tools.DbUpdate;

namespace Vita.Tools.VdbTool {
  

  class Program {
    const string ErrorLogFile = "_vdbtool.error.log";
    //command line args
    static string _command;
    static string _configFile;
    static bool _showHelp;
    static bool _nowait;


    static int Main(string[] args) {
      Console.Title = "VITA DB Tool";
      Console.WindowWidth = 120;
      Console.WriteLine("VITA DB Tool (https://github.com/rivantsov/vita) ");
      Console.WriteLine("  use /h switch for help ");
      Console.WriteLine();
      int result = 0;
      try {
        result = Run(args); 
      } catch (Exception ex) {
        result = -1; 
        WriteException(ex); 
      }
      WaitPressKey(); 
      return result; //success
    }//main

    private static int Run(string[] args) {
      //Read command line parameters
      UnpackArguments(args);
      if(_showHelp) {
        ShowHelp();
        Console.WriteLine();
        if(string.IsNullOrEmpty(_configFile))
          return 0;
      }
      //get config file name
      if(string.IsNullOrEmpty(_configFile)) {
        WriteError("Invalid arguments - missing config file parameter (/cfg:<file>).");
        ShowHelp();
        return -1; //return error
      }
      if(!File.Exists(_configFile)) {
        WriteError("Config file not found: " + _configFile);
        return -1; //return error
      }
      //load config
      var xmlConfig = LoadConfig(_configFile);
      var fback = new ConsoleProcessFeedback();
      //execute command
      switch(_command) {
        case "dbfirst":
          Console.WriteLine("COMMAND: dbfirst");
          Console.WriteLine("Generating entity definitions from the database...");
          var dbFirst = new DbFirstProcessor(fback);
          var success = dbFirst.GenerateEntityModelSources(xmlConfig);
          return success ? 0 : -1;
        case "dbupdate":
          Console.WriteLine("COMMAND: dbupdate");
          Console.WriteLine("Generating DB update scripts...");
          var dbUpdate = new DbUpdateProcessor(fback);
          var ok = dbUpdate.GenerateScripts(xmlConfig);
          return ok ? 0 : -1;
        default:
          WriteError(StringHelper.SafeFormat(" Command type arg ({0}) is invalid or missing. Expected 'dbfirst' or 'dbupdate'. ", _command));
          ShowHelp();
          return -1;
      }
    }

    private static void WriteException(Exception ex) {
      var err = ex.ToLogString();
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine();
      Console.WriteLine("Exception: ");
      Console.WriteLine(err);
      Console.ResetColor();
      LogError(err + "\r\n");
    }

    private static void WriteError(string error) {
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine();
      Console.WriteLine("ERROR: " + error);
      Console.ResetColor();
      LogError(error + "\r\n");
    }

    private static void UnpackArguments(string[] args) {
      if(args.Length == 0)
        return; 
      _command = args[0];
      _showHelp = args.FirstOrDefault(arg => arg.StartsWith("/?") || arg.StartsWith("/h")) != null;
      _nowait = args.FirstOrDefault(arg => arg.StartsWith("/nowait")) != null; //no wait for input
      const string configTag = "/cfg:";
      var cfgArg = args.FirstOrDefault(arg => arg.StartsWith(configTag));
      if (cfgArg != null)
        _configFile = cfgArg.Substring(configTag.Length);  
    }


    private static XmlDocument LoadConfig(string configFileName) {
      Console.WriteLine("Loading config file: " + configFileName);
      Util.Check(System.IO.File.Exists(configFileName), "File not found: {0}", configFileName);
      var xDoc = new XmlDocument();
      xDoc.Load(configFileName);
      return xDoc;
    }


    private static void ShowHelp() {
      Console.WriteLine();
      Console.WriteLine("---------------------------HELP--------------------------------------------------------------");
      Console.WriteLine("This utility tool can perform various tasks over the database: ");
      Console.WriteLine("  * Generate entities from the existing database (DB-first scenario)");
      Console.WriteLine("  * Generate/apply DDL SQL scripts for upgrading the database schema ");
      Console.WriteLine();
      Console.WriteLine("Usage:");
      Console.WriteLine("  vdbtool <cmd> /cfg:<cfg-file> [/nowait] [/h]");
      Console.WriteLine("Switches:");
      Console.WriteLine("    <cmd>          - command to execute:");
      Console.WriteLine("                      dbfirst   - generate entity model (c#) from database tables");
      Console.WriteLine("                      dbupdate  - generate DB update script");
      Console.WriteLine("    /cfg:<path>    - configuration file path. See sample *.cfg files.");
      Console.WriteLine("    /nowait        - no wait for input, for unattended batch mode execution.");
      Console.WriteLine("    /h             - show help.");
      Console.WriteLine();
      Console.WriteLine("Example: ");
      Console.WriteLine("   vdbtool dbfirst /cfg:books.vdb.cfg /nowait");
      Console.WriteLine("---------------------------------------------------------------------------------------------");
      Console.WriteLine();
      WaitPressKey(); 
    }

    private static void WaitPressKey() {
      if (_nowait) return; //do not stop in batch mode
      Console.WriteLine("Press any key...");
      Console.ReadKey();
    }

    // If we are running in batch mode, try to log error
    private static void LogError(string message) {
      if (_nowait)
        try { System.IO.File.AppendAllText(ErrorLogFile, message + "\r\n"); } catch { }
    }

  }
}
