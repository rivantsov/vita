using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Vita.Common;
using Vita.Data.Driver;
using Vita.Entities;

namespace Vita.Tools.DbFirst {
  [Flags]
  public enum DbFirstOptions {
    None = 0,
    AutoOnGuidPrimaryKeys = 1 << 1,
    Binary16AsGuid = 1 << 2, //used by MySql which has no Guid type; Guids are binary(16) in the database
    BinaryKeysAsGuid = 1 << 3, // SQLite - assume Guid type for binary primary and foreign keys
    UtcDates = 1 << 4, // add Utc to all date times
    ChangeEntityNamesToSingular = 1 << 5, // Change plural to singular: table 'Orders' -> entity 'IOrder'

    AddOneToManyLists = 1 << 8,
    GenerateConsoleAppCode = 1 << 9,

    Default = AutoOnGuidPrimaryKeys | AddOneToManyLists | GenerateConsoleAppCode,
  }

  public class DbFirstConfig {
    public readonly string ProviderType; 
    public readonly string ConnectionString;
    public readonly List<string> Schemas;
    public readonly DbFirstOptions Options;
    public readonly string OutputPath;
    public readonly String Namespace;
    public readonly string AppClassName;
    public readonly IDictionary<string, AutoType> AutoValues;
    public readonly IDictionary<string, Type> ForceDataTypes;
    public readonly DbDriver Driver;
    public readonly HashSet<string> IgnoreTables;

    public DbFirstConfig() { }

    public DbFirstConfig(XmlDocument xmlConfig) {
      ProviderType = xmlConfig.GetValue(ToolConfigNames.Provider);
      ConnectionString = xmlConfig.GetValue(ToolConfigNames.ConnectionString);
      Driver = ToolHelper.CreateDriver(ProviderType, ConnectionString);
      Options = ReflectionHelper.ParseEnum<DbFirstOptions>(xmlConfig.GetValue(ToolConfigNames.Options));
      Schemas = xmlConfig.GetValueList(ToolConfigNames.Schemas);
      OutputPath = xmlConfig.GetValue(ToolConfigNames.OutputPath);
      Namespace = xmlConfig.GetValue(ToolConfigNames.Namespace);
      AppClassName = xmlConfig.GetValue(ToolConfigNames.AppClassName);

      var autoValueSpec = xmlConfig.GetValue(ToolConfigNames.AutoValues);
      AutoValues = ParseAutoValuesSpec(autoValueSpec);
      var dataTypesSpec = xmlConfig.GetValue(ToolConfigNames.ForceDataTypes);
      ForceDataTypes = ParseDataTypesSpec(dataTypesSpec);
      IgnoreTables = new StringSet();
      var ignoreList = xmlConfig.GetValue(ToolConfigNames.IgnoreTables);
      if(!string.IsNullOrWhiteSpace(ignoreList))
        IgnoreTables.UnionWith(ignoreList.Split(new [] {',', ';'}, StringSplitOptions.RemoveEmptyEntries));
    }

    public static DbFirstConfig FromXml(string xml) {
      var xmlConfig = new XmlDocument();
      xmlConfig.LoadXml(xml);
      return new DbFirstConfig(xmlConfig); 
    }

    public static Dictionary<string, AutoType> ParseAutoValuesSpec(string autoValuesSpec) {
      var autoValues = new Dictionary<string, AutoType>(StringComparer.OrdinalIgnoreCase);
      if(string.IsNullOrWhiteSpace(autoValuesSpec))
        return autoValues;
      AutoType autoType;
      var segments = autoValuesSpec.SplitNames(',');
      foreach(var segm in segments) {
        var arr = segm.SplitNames(':');
        Util.Check(arr.Length == 2, "Config error, invalid segment in AutoValues: '{0}'.", segm);
        var name = arr[0];
        var sType = arr[1];
        Util.Check(Enum.TryParse(sType, out autoType), "Invalid autoType attribute value: {0}", sType);
        autoValues[name] = autoType;
      }
      return autoValues;
    }
    public static Dictionary<string, Type> ParseDataTypesSpec(string spec) {
      var dict = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
      if(string.IsNullOrWhiteSpace(spec))
        return dict;
      var segments = spec.SplitNames(',');
      foreach(var segm in segments) {
        var arr = segm.SplitNames(':');
        Util.Check(arr.Length == 2, "Config error, invalid segment in ForceDataTypes: '{0}'.", segm);
        var name = arr[0];
        var sType = arr[1];
        var type = Type.GetType(sType);
        Util.Check(type != null, "Invalid type name:  {0}", sType);
        dict[name] = type;
      }
      return dict;
    }


  }//class
}
