using System;
using System.Collections.Generic;
using System.Text;

using Vita.Entities;
using Vita.Data.Driver;
using Microsoft.Extensions.Configuration;
using Vita.Entities.Utilities;

namespace Vita.Testing.ExtendedTests {

  public class TestRunConfig {
    public DbServerType ServerType;
   // public bool EnableCache;
    public bool UseBatchMode;
    public string ConnectionString;
    public string LogConnectionString; 

    public override string ToString() {
      return $"SERVER TYPE: {ServerType} , BatchMode: {UseBatchMode}";
    }


  } //class
} // ns
