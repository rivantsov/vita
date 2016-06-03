using System;
using System.Data.SqlClient;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Vita.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Vita.UnitTests.Basic {

  [TestClass]
  public class MiscHelpersTests {




    [TestMethod]
    public void TestCompression() {
      const string testString = @"
Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
      var bytes = CompressionHelper.CompressString(testString);
      var resultString = CompressionHelper.DecompressString(bytes);
      Assert.AreEqual(testString, resultString, "Compress/decompress result does not match original.");
      Debug.WriteLine("String size: " + testString.Length + ", compressed size: " + bytes.Length);
    }

    [TestMethod]
    public void TestLoggingExtensions() {
      //We test how IDbCommand details are reported when it is included in exc.Data dictionary. VITA does include this info automatically when db exception occurs.
      var cmd = new SqlCommand();
      //We make sure we find _PROC_TAG_ inside exc report - to ensure that entire SQL text is reported
      // parameter values on the other hand might be trimmed in the middle, so _PARAM_TAG_ will not be there.
      cmd.CommandText = @"
SELECT SOME NONSENSE 
  Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
  Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum

_PROC_TAG_  

  Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
  Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum

";
      var prmValue = @"
  Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. 
  Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. 
_PARAM_TAG_
  Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. 
  Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum
";
      cmd.Parameters.Add(new SqlParameter("@p0", prmValue));
      var innerExc = new Exception("Inner exception");
      innerExc.Data["DbCommand"] = cmd.ToLogString(); 
      var exc = new Exception("Test exception", innerExc);
      var excObj = (object)exc;
      var logExc = excObj.ToLogString();
      Debug.WriteLine("Exception.ToLogString(): \r\n" + logExc);
      Assert.IsTrue(logExc.Contains("_PROC_TAG_"), "Exception log does not contain SQL control tag.");
      Assert.IsTrue(!logExc.Contains("_PARAM_TAG_"), "Exception contains param control tag.");
    }

  }
}
