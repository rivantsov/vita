using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.Login {

  //Generic interface, you can replace it with your own hashing algorithm. We use BCrypt hashing. 
  public interface IPasswordHasher {
    int WorkFactor { get; } // current work factor; might be lower for old records
    string HashPassword(string password, byte[] salt);
    bool VerifyPassword(string password, byte[] salt, int workFactor, string hash);
  }

}//ns
