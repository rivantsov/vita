using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;

namespace Vita.Modules.Login {

  public class BCryptPasswordHasher : IPasswordHasher {
    int _workFactor; 

    public BCryptPasswordHasher(int workFactor = 10) {
      Util.Check(workFactor < 39, "WorkFactor should be below 39, typical value is 10.");
      _workFactor = workFactor; 
    }

    public int WorkFactor {
      get { return _workFactor; }
    }

    //Note: BCrypt does not use external salt. 
    public string HashPassword(string password, byte[] salt) {
      var hash = BCryptNet.BCrypt.HashPassword(password, _workFactor);
      return hash; 
    }
    public bool VerifyPassword(string password, byte[] salt, int workFactor, string hash) {
      //Note: in BCrypt workFactor is embedded into hash
      return BCryptNet.BCrypt.Verify(password, hash);
    }
  }

}//ns
