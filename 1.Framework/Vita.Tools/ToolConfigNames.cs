using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Tools {
  //Names of elements in config file; see sample .cfg files
  public static class ToolConfigNames {
    public const string Provider = nameof(Provider);
    public const string ConnectionString = nameof(ConnectionString);

    //DbFirst config elements
    public const string OutputPath = nameof(OutputPath);
    public const string Namespace = nameof(Namespace);
    public const string AppClassName = nameof(AppClassName);
    public const string Schemas = nameof(Schemas);
    public const string Options = nameof(Options);
    public const string AutoValues = nameof(AutoValues);
    public const string ForceDataTypes = nameof(ForceDataTypes);
    public const string IgnoreTables = nameof(IgnoreTables);
    public const string TableNames = nameof(TableNames);

    public const string AssemblyPath = nameof(AssemblyPath);
    public const string DbOptions = nameof(DbOptions);
    public const string ModelUpdateOptions = nameof(ModelUpdateOptions);

  }
}
