using System;
using System.Runtime.InteropServices;

namespace Vita.Common {
  // NOT USED anymore; does not seem like they give any advantages.
  // Also in some cases using sequential GUIDs vs normal GUIDs may be a leak of certain information to the client

  public static class Externals {
    [DllImport("rpcrt4.dll", SetLastError = true)]
    static extern int UuidCreateSequential(out Guid guid);

    public static Guid NewSequentialGuid() {
      const int RPC_S_OK = 0;
      Guid g;
      int hr = UuidCreateSequential(out g);
      if(hr != RPC_S_OK)
        throw new ApplicationException
          ("UuidCreateSequential failed: " + hr);
      return g;
    }
  }

}//namespace
