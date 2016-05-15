using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using System.Xml;
using System.IO;
using Microsoft.CSharp;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model.Construction;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Upgrades;

namespace Vita.Tools.DbFirst {

  public class DbFirstProcessor {
    IProcessFeedback _feedback;
    public bool HasErrors;


    public DbFirstProcessor(IProcessFeedback feedback) {
      _feedback = feedback; 
    }


    public bool GenerateEntityModelSources(XmlDocument xmlConfig) {
      var config = new DbFirstConfig(xmlConfig);
      _feedback.WriteLine("  Provider: " + config.ProviderType);
      _feedback.WriteLine("  Connection string: " + config.ConnectionString);
      _feedback.WriteLine();

      // create driver and check connection
      string error;
      if(!ToolHelper.TestConnection(config.Driver, config.ConnectionString, out error)) {
        _feedback.WriteError(error);
        return false; 
      }

      _feedback.WriteLine("Generating entity definitions...");
      var appBuilder = new DbFirstAppBuilder(_feedback);
      var entApp = appBuilder.Build(config);
      var srcWriter = new DbFirstSourceWriter(_feedback);
      srcWriter.WriteCsSources(entApp, config);
      // check errors
      if(srcWriter.HasErrors)
        return false; 
      //Everything is ok
      _feedback.WriteLine("The source code is saved to: " + config.OutputPath);
      // Do test compile 
      CompilerResults compilerResults = null;
      _feedback.WriteLine("Verifying generated code - compiling...");
      var genSource = File.ReadAllText(config.OutputPath);
      compilerResults = DbFirstProcessor.CompileSources(config.Driver.GetType(), genSource);
      if(compilerResults.Errors.Count > 0) {
        HasErrors = true;
        _feedback.WriteError("Compile errors detected.");
        foreach(CompilerError err in compilerResults.Errors) {
          var strErr = "  " + err.ErrorText + " Line: " + err.Line;
          _feedback.WriteError(strErr);
        }
        return false;
      } else 
      //Compare schema
      _feedback.WriteLine("Verifying generated entities. Running schema comparison ...");

      var nonProcActions = DbFirstProcessor.CompareDatabaseSchemas(config, compilerResults.CompiledAssembly);
      string schemaMessage;
      if(nonProcActions.Count == 0) {
        schemaMessage = "Schema verification completed, schemas are identical.";
      } else {
        HasErrors = true; 
        schemaMessage = StringHelper.SafeFormat("Schema verification: detected {0} differences (schema update actions).\r\n", nonProcActions.Count);
        schemaMessage += "Schema update actions:\r\n  ";
        schemaMessage += string.Join("\r\n  ", nonProcActions);

        schemaMessage += @"

Note: Non-empty update action list represents the delta between the original database schema and 
      the schema from the generated entities. Ideally this list should be empty. If it is not, 
      then probably some features in your database are not currently supported by VITA. 
";
      }
      //Dump the message to the source file as comment:
      System.IO.File.AppendAllText(config.OutputPath, "\r\n/*\r\n" + schemaMessage + "\r\n*/");
      _feedback.WriteLine();
      _feedback.WriteLine(schemaMessage);
      return !HasErrors; 
    }



    // Used for verification that generated c# entities produce identical database objects
    public static List<DbUpgradeScript> CompareDatabaseSchemas(DbFirstConfig config, Assembly assembly) {
      var fullModelName = config.Namespace + "." + config.AppClassName;
      var modelType = assembly.GetType(fullModelName);
      return CompareDatabaseSchemas(config, modelType); 
    }

    public static List<DbUpgradeScript> CompareDatabaseSchemas(DbFirstConfig config, Type modelType) {
      var entApp = Activator.CreateInstance(modelType) as EntityApp;
      entApp.Init(); 
      // important - do not use DbOptions.AutoIndexForeignKeys - which is recommended for MS SQL, but is not helpful here.
      // This will create a bunch of extra indexes on FKs in entities schema and result in extra differences with original schema.
      //  We ignore stored procs 
      var dbOptions = DbOptions.Default & ~DbOptions.AutoIndexForeignKeys;
      var dbSettings = new DbSettings(config.Driver, dbOptions, config.ConnectionString, 
                                      upgradeMode: DbUpgradeMode.Always,  
                                      upgradeOptions : DbUpgradeOptions.UpdateTables | DbUpgradeOptions.UpdateIndexes
                                      );
      dbSettings.SetSchemas(config.Schemas);
      var db = new Database(entApp, dbSettings);
      var ds = new DataSource("main", db);
      var upgradeMgr = new DbUpgradeManager(ds);
      var upgradeInfo = upgradeMgr.BuildUpgradeInfo();
      return upgradeInfo.AllScripts;
    }

    public static CompilerResults CompileSources(Type driverType, params string[] sources) {
      var csProvider = new CSharpCodeProvider();
      var driverLoc = driverType.Assembly.Location;
      var driverAssembly = System.IO.Path.GetFileName(driverLoc);
      var compilerParams = new CompilerParameters(new string[] { "System.dll", "System.Core.dll", "System.Data.dll", 
                                                                 "Vita.dll", driverAssembly });
      var results = csProvider.CompileAssemblyFromSource(compilerParams, sources);
      return results;
    }




  }
}
