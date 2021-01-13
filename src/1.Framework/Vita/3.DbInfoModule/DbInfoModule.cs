using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities;
using Vita.Entities.Services;

namespace Vita.Entities.DbInfo {

  /// <summary>Entity module defining entities/tables for storing database version information.</summary>
  public partial class DbInfoModule : EntityModule {
    public static readonly Version CurrentVersion = new Version("2.0.0.0");

    public DbInfoModule(EntityArea area) : base(area, "DbInfo", version: CurrentVersion) {
      RegisterEntities(typeof(IDbInfo), typeof(IDbModuleInfo));
      App.RegisterService<IDbInfoService>(this); 
    }

  }//class
} //ns
