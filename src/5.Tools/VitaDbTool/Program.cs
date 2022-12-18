using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Tools.DbFirst;
using Vita.Tools.DbUpdate;

namespace Vita.Tools.VdbTool {
  

  class Program {
    const string ErrorLogFile = "_vdbtool.error.log";
    static string[] _commands = { "dbfirst", "dbupdate" };
    //command line args
    static string _command;
    static string _configFile;
    static bool _showHelp;
    static bool _nowait;


    static int Main(string[] args) {
      Console.Title = "VITA DB Tool";
      //Console.WindowWidth = 120;
      Console.WriteLine("VITA DB Tool (https://github.com/rivantsov/vita) ");
      Console.WriteLine("  use -h switch for help ");
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
      args = args.Select(a => a.ToLower()).ToArray();
      //Read command line parameters
      UnpackArguments(args);
      if(_showHelp) {
        ShowHelp();
        Console.WriteLine();
        if(string.IsNullOrEmpty(_configFile))
          return 0;
      }
      if (!_commands.Contains(_command)) {
        WriteError($"Invalid command argument '{_command}' - expected 'dbfirst' or 'dbupdate'.");
        return -1;
      }
      //get config file name
      if (string.IsNullOrEmpty(_configFile)) {
        WriteError("Invalid arguments - missing config file path parameter.");
        ShowHelp();
        return -1; //return error
      }
      if(!File.Exists(_configFile)) {
        WriteError($"Config file '{_configFile}' not found.");
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
          var dbFirstConfig = new DbFirstConfig(xmlConfig);
          var success = dbFirst.GenerateEntityModelSources(dbFirstConfig);
          return success ? 0 : -1;
        case "dbupdate":
          Console.WriteLine("COMMAND: dbupdate");
          Console.WriteLine("Generating DB update scripts...");
          var dbUpdate = new DbUpdateProcessor(fback);
          var ok = dbUpdate.GenerateScripts(xmlConfig);
          return ok ? 0 : -1;
        default:
          WriteError(Util.SafeFormat(" Command type arg ({0}) is invalid or missing. Expected 'dbfirst' or 'dbupdate'. ", _command));
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
      if (args == null || args.Length < 2) {
        WriteError("Invalid parameters.");
        _showHelp = true; 
        return;
      }
      _showHelp = args.Any(arg => arg == "?" || arg == "-h");
      if (_showHelp)
        return; 
      _command = args[0];
      _configFile = args[1];
      _nowait = args.Any(arg => arg == "-nowait"); //no wait for input
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
      Console.WriteLine("  vdbtool <cmd> <config> [-nowait] [-h]");
      Console.WriteLine("Switches:");
      Console.WriteLine("    <cmd>          - command to execute:");
      Console.WriteLine("                      dbfirst   - generate entity model (c#) from database tables");
      Console.WriteLine("                      dbupdate  - generate DB update script");
      Console.WriteLine("    <config>       - configuration file path - XML file with parameters for the operation.");
      Console.WriteLine("    -nowait        - no wait for input, for unattended batch mode execution.");
      Console.WriteLine("    -h             - show help.");
      Console.WriteLine();
      Console.WriteLine("Example: ");
      Console.WriteLine("   vdbtool dbfirst books.vdb.cfg -nowait ");
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
