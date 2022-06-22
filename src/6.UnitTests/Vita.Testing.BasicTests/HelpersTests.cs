using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Vita.Entities;
using Vita.Entities.Utilities;
using Vita.Data.Driver;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace Vita.Testing.BasicTests.Helpers {

  [TestClass]
  public class HelpersTests {

    [TestMethod]
    public void TestHelpers_Compression() {
      const string testString = @"
Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
      var bytes = CompressionHelper.CompressString(testString);
      var resultString = CompressionHelper.DecompressString(bytes);
      Assert.AreEqual(testString, resultString, "Compress/decompress result does not match original.");
     // Debug.WriteLine("String size: " + testString.Length + ", compressed size: " + bytes.Length);
    }

    [TestMethod]
    public void TestHelpers_LoggingExtensions() {
      var sqlTag = "_SQL_TEXT_TAG_";
      var paramTag = "_PARAM_TAG_";
      
      //We test how IDbCommand details are reported when it is included in exc.Data dictionary. VITA does include this info automatically when db exception occurs.
      var cmd = new SqlCommand();
      //We make sure we find <SqlTag> inside exc report - to ensure that entire SQL text is reported
      // parameter values on the other hand might be trimmed in the middle, so <param-tag> will not be there.
      cmd.CommandText = $@"
SELECT SOME NONSENSE 
  Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
  Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum

{sqlTag}

  Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
  Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum

";
      var prmValue = $@"
  Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
{paramTag}
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
  Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum
";
      cmd.Parameters.Add(new SqlParameter("@p0", prmValue));
      var innerExc = new Exception("Inner exception");
      innerExc.Data["DbCommand"] = cmd.ToLogString(); 
      var exc = new Exception("Test exception", innerExc);
      var excObj = (object)exc;
      var excLogStr = excObj.ToLogString();
      Debug.WriteLine("Exception.ToLogString(): \r\n" + excLogStr);
      Assert.IsTrue(excLogStr.Contains(sqlTag), "Exception log does not contain SQL control tag.");
      Assert.IsTrue(!excLogStr.Contains(paramTag), "Exception contains param control tag.");
    }


    #region SCC test
    [TestMethod]
    public void TestHelpers_GraphSccAlgorithm() {
      //This test uses a simple graph from the wikipedia page about SCC: http://en.wikipedia.org/wiki/Strongly_connected_components
      // The copy of this image is in SccTestGraph.jpg file in this test project.
      var expected = "a, Scc=1; b, Scc=1; e, Scc=1; c, Scc=2; d, Scc=2; h, Scc=2; f, Scc=3; g, Scc=3"; //expected SCC indexes
      var gr = new SccGraph();
      SetupSampleGraph(gr);
      gr.BuildScc();
      // additionally sort by tags, so that result string matches
      var sortedVertexes = gr.Vertexes.OrderBy(v => v.SccIndex).ThenBy(v => (string)v.Source).ToList();
      var strOut = string.Join("; ", sortedVertexes);
      Assert.AreEqual(expected, strOut, "SCC computation did not return expected result.");
    }

    // Builds a sample graph from http://en.wikipedia.org/wiki/Strongly_connected_components
    private static void SetupSampleGraph(SccGraph gr) {
      var a = gr.AddVertex("a");
      var b = gr.AddVertex("b");
      var c = gr.AddVertex("c");
      var d = gr.AddVertex("d");
      var e = gr.AddVertex("e");
      var f = gr.AddVertex("f");
      var g = gr.AddVertex("g");
      var h = gr.AddVertex("h");
      //links
      a.AddLink(b);
      b.AddLink(e, f, c);
      c.AddLink(d, g);
      d.AddLink(c, h);
      e.AddLink(a, f);
      f.AddLink(g);
      g.AddLink(f);
      h.AddLink(g, d);
    }
    #endregion


    [TestMethod]
    public void TestHelpers_BitMask() {
      var mask = new BitMask(128);
      // set first hex digits to 0, 1, 2, 4, 8 by setting bits
      mask.Set(4, true);
      mask.Set(9, true);
      mask.Set(14, true);
      mask.Set(19, true);
     // set 1 bit far away
      mask.Set(64, true);

      // Set 4 bits to all 1's, so it will be F
      mask.Set(112, true);
      mask.Set(113, true);
      mask.Set(114, true);
      mask.Set(115, true);
      var hex = mask.ToHex();
      // Debug.Write($"Bit mask hex: {hex} ");
      var expected = "0008 4210 0000 0000 0000 0001 000F 0000".Replace(" ", string.Empty);
      Assert.AreEqual(expected, hex, "Hex value of mask does not match expected.");
    }

    [TestMethod]
    public void TestGenerateCryptoKeys() {
      //It is not actually a test but simple convenience method to generate cryptokeys of appropriate length for various crypto algorithms
      // Run it when you need a key to setup encryption channel in EncryptedDataModule
      // Enable it only for MS SQL, to avoid slowing down console run for all servers
      if(Startup.ServerType != DbServerType.MsSql)
        return;
      Debug.WriteLine("\r\n-------------------- TestGenerateCryptoKeys: generated Crypto Keys ---------------------------------\r\n");
      GenerateAndPrintKey(new RijndaelManaged());
      GenerateAndPrintKey(new AesManaged());
      GenerateAndPrintKey(new DESCryptoServiceProvider());
      GenerateAndPrintKey(new RC2CryptoServiceProvider());
      GenerateAndPrintKey(new TripleDESCryptoServiceProvider());
    }

    private void GenerateAndPrintKey(SymmetricAlgorithm alg) {
      alg.GenerateKey();
      alg.GenerateIV();
      var algName = alg.GetType().Name.PadRight(30);
      var hexKey = HexUtil.ByteArrayToHex(alg.Key);
      Debug.WriteLine($"Algorithm: {algName}, keySize(bits): {alg.KeySize}, key: {hexKey}");
    }


  }
}
