using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Vita.Common;

namespace Vita.Data.Driver {

  /// <summary>Helper class for hashing the source SQL of stored procedures and views. </summary>
  /// <remarks>For generated objects like stored procedures and Views, the system needs to detect if the object 
  /// needs to be updated in the database or not. 
  /// Comparing the source code (newly generated version vs currently in database) does not work -
  /// in most cases the database engine reformats the source, so the source code of view or procedure 
  /// returned by information schema query is slightly different from the CREATE SQL originally submitted. 
  /// So VITA uses hashing - it hashes the source of procedure or view and injects a comment line containing the hash. 
  /// This does not work for Views in MySql and Postgres - these insist on removing 
  /// all comments in view and saving it in 'canonical' form. Who needs docs and comments anyway, right?
  /// So we hash the code and store it in two ways. 
  /// 1. For views, if the app uses DbInfoModule, the hashes are saved in the DbVersionInfo.Values dictionary and 
  /// then saved in IDbInfo.Values column. At application startup the 'old' hash values are loaded and compared to newly
  /// computed hashes. If there's no hashes available in DbInfo record, system tries to get hash from source code comment
  /// from information_schema query (this is method #2). If this fails too, custom methods are used to retrieve original view source and hash from 
  /// comment line in it - method 2. Each server has its own set of tricks 
  /// 2. In special comment line. When source code is loaded, it extract the hash from this comment line. 
  /// </remarks>
  public class SqlSourceHasher {
    // 1. Space after '--' is important - MySql fails otherwise
    // 2. SQLite removes spaces when it returns view definition; hash line is added as first line of View SQL, so whole view appears as one line. 
    //    To properly extract hash, we enclose hash value into '*' and extract it
    public const string HashPrefix = "-- HASH:*"; 
    SHA256CryptoServiceProvider _hasher = new SHA256CryptoServiceProvider(); // using FIPS-compliant provider instead of MD5

    public string ComputeHash(params string[] sqls) {
      var sql = string.Join(Environment.NewLine, sqls); 
      var hash = HexUtil.ByteArrayToHex(_hasher.ComputeHash(Encoding.UTF8.GetBytes(sql)));
      return hash;
    }

    public string GetHashLine(string hash) {
      return HashPrefix + hash + "*"; 
    }

    public string ExtractHash(string sql) {
      if (string.IsNullOrWhiteSpace(sql))
        return null;
      var lines = sql.Split('\n');
      for (int i = lines.Length - 1; i >= 0; i--) {
        var line = lines[i].Trim(); 
        if (line.StartsWith(HashPrefix)) {
          var hash = line.Substring(HashPrefix.Length);
          var starIndex = hash.IndexOf("*");
          if (starIndex > 0)
            hash = hash.Substring(0, starIndex); 
          return hash.Trim();
        }
      }
      return null; 
    }

  }
}
