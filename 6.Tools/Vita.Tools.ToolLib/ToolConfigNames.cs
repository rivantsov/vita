using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Tools {
  //Names of elements in config file; see sample .cfg files
  public static class ToolConfigNames {
    public const string Provider = "Provider";
    public const string ConnectionString = "ConnectionString";

    //DbFirst config elements
    public const string OutputPath = "OutputPath";
    public const string Namespace = "Namespace";
    public const string AppClassName = "AppClassName";
    public const string Schemas = "Schemas";
    public const string Options = "Options";
    public const string AutoValues = "AutoValues";
    public const string ForceDataTypes = "ForceDataTypes";
    public const string IgnoreTables = "IgnoreTables";

    public const string AssemblyPath = "AssemblyPath";
    public const string DbOptions = "DbOptions";
    public const string ModelUpdateOptions = "ModelUpdateOptions";

  }
}
