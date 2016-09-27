using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common.Graphs;
using Vita.Modules.Login;
using Vita.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using Vita.Data.Driver;


namespace Vita.UnitTests.Extended {

  [TestClass]
  public class HashCryptTests {

    [TestMethod]
    public void TestPasswordHashers() {
      //run it only for MS SQL, to avoid slowing down console run for all servers
      if(Startup.ServerType != DbServerType.MsSql)
        return; 

      IPasswordHasher hasher; 
      var salt = Guid.NewGuid().ToByteArray();
      var pwd = "MyPassword_*&^";
      long start, timeMs; bool match; string hash; 

      // You can use this test to approximate the 'difficulty' of hashing algorithm for your computer. 
      //  It prints the time it took to hash the pasword. This time should not be too low, desirably no less than 100 ms.
      hasher = new BCryptPasswordHasher(workFactor: 10); //each +1 doubles the effort; on my machine: 10 -> 125ms, 11->242ms
      start = Util.PreciseTicks;
      hash = hasher.HashPassword(pwd, salt);
      timeMs = Util.PreciseTicks - start; 
      match = hasher.VerifyPassword(pwd, salt, hasher.WorkFactor, hash);
      Assert.IsTrue(match, "BCrypt hasher failed.");
      Debug.WriteLine("BCrypt hasher time, ms: " + timeMs);

      hasher = new Pbkdf2PasswordHasher(iterationCount: 2000); // on my machine: 2000-> 13ms, 5000->32ms
      start = Util.PreciseTicks;
      hash = hasher.HashPassword(pwd, salt);
      timeMs = Util.PreciseTicks - start;
      match = hasher.VerifyPassword(pwd, salt, hasher.WorkFactor, hash);
      Assert.IsTrue(match, "Pbkdf hasher failed.");
      Debug.WriteLine("Pbkdf hasher time, ms: " + timeMs);
    }

    [TestMethod]
    public void TestGenerateCryptoKeys() {
      //It is not actually a test but simple convenience method to generate cryptokeys of appropriate length for various crypto algorithms
      // Run it when you need a key to setup encryption channel in EncryptedDataModule
      // Enable it only for MS SQL, to avoid slowing down console run for all servers
      if(Startup.ServerType != DbServerType.MsSql)
        return;
      GenerateKey(new RijndaelManaged());
      GenerateKey(new AesManaged());
      GenerateKey(new DESCryptoServiceProvider());
      GenerateKey(new RC2CryptoServiceProvider());
      GenerateKey(new TripleDESCryptoServiceProvider());
    }

    private void GenerateKey(SymmetricAlgorithm alg) {
      alg.GenerateKey();
      alg.GenerateIV();
      Debug.WriteLine("Algorithm: " + alg.GetType().Name.PadRight(30) + " keySize(bits): " + alg.KeySize + " key: " + HexUtil.ByteArrayToHex(alg.Key));
    }

  }//class
}//ns
