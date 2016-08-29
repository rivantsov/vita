using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Common {
  public static class CompressionHelper {

    public static byte[] CompressString(string text) {
      byte[] buffer = Encoding.UTF8.GetBytes(text);
      return Compress(buffer);
    }//method

    public static string DecompressString(byte[] data) {
      var bytes = Decompress(data);
      var result = Encoding.UTF8.GetString(bytes);
      return result; 
    }//method

    public static byte[] Compress(byte[] bytes) {
      //Save to mem stream
      using(var outStream = new MemoryStream()) {
        using(var gStream = new GZipStream(outStream, CompressionMode.Compress)) {
          gStream.Write(bytes, 0, bytes.Length);
          // should be simply Close, but not supported in .NET core, so using Dispose instead
          // without it does not work
          // gStream.Close();          
          gStream.Dispose();
          var outbytes = outStream.ToArray();
          return outbytes;
        }
      }//using outStream
    }//method

    public static byte[] Decompress(byte[] data) {
      //Save to mem stream
      using(var inputStream = new MemoryStream()) {
        inputStream.Write(data, 0, data.Length);
        inputStream.Position = 0;
        using(var outStream = new MemoryStream()) {
          using(var gStream = new GZipStream(inputStream, CompressionMode.Decompress)) {
            gStream.CopyTo(outStream);
            gStream.Flush();
            return outStream.ToArray();
          } //using gStream
        }//using outStream
      }//using inputStream
    }//method

  }
}
