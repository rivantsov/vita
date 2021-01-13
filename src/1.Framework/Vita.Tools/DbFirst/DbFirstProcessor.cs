﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using System.Xml;
using System.IO;

using Vita.Entities;
using Vita.Data;
using Vita.Data.Driver;
using Vita.Data.Upgrades;
using Vita.Entities.Logging;
using Vita.Data.Model;
using Vita.Entities.Model.Construction;
using Vita.Data.Runtime;

namespace Vita.Tools.DbFirst {

  public class DbFirstProcessor {
    IProcessFeedback _feedback;
    public bool HasErrors;


    public DbFirstProcessor(IProcessFeedback feedback) {
      _feedback = feedback; 
    }


    public bool GenerateEntityModelSources(DbFirstConfig config) {
      _feedback.WriteLine("  Provider: " + config.ProviderType);
      _feedback.WriteLine("  Connection string: " + config.ConnectionString);
      _feedback.WriteLine();

      // create driver and check connection
      string error;
      if(!DataUtility.TestConnection(config.Driver, config.ConnectionString, out error)) {
        _feedback.WriteError(error);
        return false; 
      }

      _feedback.WriteLine("Generating entity definitions...");
      var appBuilder = new DbFirstAppBuilder(_feedback);
      var entAppInfo = appBuilder.Build(config);
      var srcWriter = new DbFirstSourceWriter(_feedback);
      srcWriter.WriteCsSources(entAppInfo, config);
      // check errors
      if(srcWriter.HasErrors)
        return false; 
      //Everything is ok
      _feedback.WriteLine("The source code is saved to: " + config.OutputPath);
      // Do test compile 
      _feedback.WriteLine("Verifying generated code - compiling...");
      var genSource = File.ReadAllText(config.OutputPath);
      var compilerResults = CompilerHelper.CompileSources(config.Driver.GetType(), genSource);
      if(!compilerResults.Success) {
        HasErrors = true;
        _feedback.WriteError("Compile errors detected.");
        foreach(var err in compilerResults.Messages) {
          _feedback.WriteError(err);
        }
        return false;
      } else 
      //Compare schema
      _feedback.WriteLine("Verifying generated entities. Running schema comparison ...");

      var nonProcActions = DbFirstProcessor.CompareDatabaseSchemas(config, compilerResults.Assembly);
      string schemaMessage;
      if(nonProcActions.Count == 0) {
        schemaMessage = "Schema verification completed, schemas are identical.";
      } else {
        HasErrors = true; 
        schemaMessage = Util.SafeFormat("Schema verification: detected {0} differences (schema update actions).\r\n",
                  nonProcActions.Count);
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
      var dbOptions = config.Driver.GetDefaultOptions() & ~DbOptions.AutoIndexForeignKeys;
      var dbSettings = new DbSettings(config.Driver, dbOptions, config.ConnectionString, 
                                      upgradeMode: DbUpgradeMode.Always,  
                                      upgradeOptions : DbUpgradeOptions.UpdateTables | DbUpgradeOptions.UpdateIndexes
                                      );
      //dbSettings.SetSchemas(config.Schemas);
      var log = new BufferedLog();
      var dbModelBuilder = new DbModelBuilder(entApp.Model, dbSettings.ModelConfig, log);
      var dbModel = dbModelBuilder.Build();
      var db = new Database(dbModel, dbSettings);
      var ds = new DataSource("main", db);
      var upgradeMgr = new DbUpgradeManager(db, log);
      var upgradeInfo = upgradeMgr.BuildUpgradeInfo();
      return upgradeInfo.AllScripts;
    }

  }
}
