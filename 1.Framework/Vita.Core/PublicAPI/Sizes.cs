using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities {

  /// <summary>A container for standard string size values for use in the <c>Size</c> attribute.</summary>
  public static class Sizes {
    public class SizeTable : Dictionary<string, int> {
      public SizeTable() : base(StringComparer.InvariantCultureIgnoreCase) { }
    }

    public const string Name = "Name";
    public const string LongName = "LongName";
    public const string Description = "Description";
    public const string Initial = "Initial";
    public const string PrefixSuffix = "PrefixSuffix"; //prefix/suffix for names like Mr... Jr.
    public const string UserName = "UserName";
    public const string Email = "Email";
    public const string IPv6Address = "IPv6Address";

    public static SizeTable GetDefaultSizes() {
        return new 
          SizeTable() { {Name, 50}, {LongName, 100}, {Description, 250}, {Initial, 1}, {PrefixSuffix, 10}, {UserName, 50}, {Email, 100}, {IPv6Address, 50}
          
        };
    }

    public static string GetFullSizeCode(string ns, string code) {
      return ns + "#" + code; 
    }

  }//class
}//ns
