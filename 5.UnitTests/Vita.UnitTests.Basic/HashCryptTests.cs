using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using Vita.Data.Driver;


namespace Vita.UnitTests.Basic {

  [TestClass]
  public class HashCryptTests {

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
