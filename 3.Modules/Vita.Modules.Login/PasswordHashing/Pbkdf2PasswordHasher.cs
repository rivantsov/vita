using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Modules.Login {

  public class Pbkdf2PasswordHasher : IPasswordHasher {
    public readonly int HashSize;
    int _iterationCount;

    public Pbkdf2PasswordHasher(int iterationCount = 2000, int hashSize = 16) {
      _iterationCount = iterationCount;
      HashSize = hashSize; 
    }

    public int WorkFactor {
      get { return _iterationCount; }
    }

    public string HashPassword(string password, byte[] salt) {
      var strHash = ComputeHash(password, salt, _iterationCount, HashSize);
      return strHash; 
    }
    public bool VerifyPassword(string password, byte[] salt, int oldIterationCount, string hash) {
      var newHash = ComputeHash(password, salt, oldIterationCount, HashSize);
      return newHash == hash;
    }

    private static string ComputeHash(string password, byte[] salt, int iterationCount, int hashSize) {
      var hasher = new System.Security.Cryptography.Rfc2898DeriveBytes(password, salt, iterationCount);
      var bytes = hasher.GetBytes(hashSize);
      var strHash = HexUtil.ByteArrayToHex(bytes);
      return strHash;
    }

  }
}//ns
