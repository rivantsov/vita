﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Vita.Data;
using Vita.Data.Driver;
using Vita.Entities.Utilities;

namespace Vita.Tools.DbUpdate {

  public class DbUpdateConfig {
    public readonly string ProviderType; 
    public readonly DbDriver Driver;
    public readonly string ConnectionString;
    public readonly DbOptions DbOptions; 
    public readonly DbUpgradeOptions ModelUpdateOptions;
    public readonly string OutputPath;
    public readonly String AssemblyPath;
    public readonly string AppClassName;

    public DbUpdateConfig() { }

    public DbUpdateConfig(XmlDocument xmlConfig) {
      ProviderType = xmlConfig.GetValue(ToolConfigNames.Provider);
      var serverType = (DbServerType) Enum.Parse(typeof(DbServerType), ProviderType); 
      ConnectionString = xmlConfig.GetValue(ToolConfigNames.ConnectionString);
      Driver = DataUtility.CreateDriver(serverType);
      ModelUpdateOptions = StringHelper.ParseEnum<DbUpgradeOptions>(xmlConfig.GetValue(ToolConfigNames.ModelUpdateOptions));
      DbOptions = StringHelper.ParseEnum<DbOptions>(xmlConfig.GetValue(ToolConfigNames.DbOptions));
      AssemblyPath = xmlConfig.GetValue(ToolConfigNames.AssemblyPath);
      AppClassName = xmlConfig.GetValue(ToolConfigNames.AppClassName);
      OutputPath = xmlConfig.GetValue(ToolConfigNames.OutputPath);
    }

    public static DbUpdateConfig FromXml(string xml) {
      var xmlConfig = new XmlDocument();
      xmlConfig.LoadXml(xml);
      return new DbUpdateConfig(xmlConfig); 
    }


  }//class
}
