using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Entities.Runtime {

  [Flags]
  public enum EntitySessionOptions {
    None = 0,
    ReadOnly = 1,
    ConcurrentReadonly = 1 << 1, //allowed only for readonly sessions

    Default = None, 
  }

  public class EntitySessionSettings {
    //identifies the form/code area/location which creates the session; planned to use in the future to automatically adapt session behavior based 
    // on previous uses from the same location
    public string CodePath; 
    public EntitySessionOptions Options;

    public EntitySessionSettings() : this("default", EntitySessionOptions.Default) { }
    public EntitySessionSettings(string codePath, EntitySessionOptions options) {
      CodePath = codePath;
      Options = options; 
    }

    public static EntitySessionSettings Default = new EntitySessionSettings();
  }//class

  public static partial class EntitySessionExtensions {
    public static bool IsSet(this EntitySessionOptions options, EntitySessionOptions option) {
      return (options & option) != 0;
    }
  }
}
