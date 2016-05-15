using System;
using System.Linq;
using System.Collections.Generic;

using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Common;

namespace Vita.Entities {

  public static class ValidationMessages {
    public static string ValueMissing = "Value of {0} may not be empty.";
    public static string ValueTooLong = "Value of {0} exceeds maximum length ({1}).";
    public static string ObjectNotFound = "Object not found";


  }//class

}//ns
