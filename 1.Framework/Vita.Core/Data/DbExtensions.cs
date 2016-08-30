using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

using Vita.Entities;
using Vita.Data.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;

namespace Vita.Data {

  public static class DbExtensions {

    //Enum extensions
    public static bool IsSet(this DbOptions options, DbOptions option) {
      return (options & option) != 0;
    }
    public static bool IsSet(this DbFeatures features, DbFeatures feature) {
      return (features & feature) != 0;
    }
    public static bool IsSet(this DbUpgradeOptions options, DbUpgradeOptions option) {
      return (options & option) != 0;
    }

    public static bool IsSet(this VendorDbTypeFlags flags, VendorDbTypeFlags flag) {
      return (flags & flag) != 0;
    }

    public static bool IsSet(this ConnectionFlags flags, ConnectionFlags flag) {
      return (flags & flag) != 0;
    }



  }//class

}//namespace
