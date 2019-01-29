using System;
using System.Collections.Generic;
using System.IO; 
using System.Text;
using Vita.Data.Sql;

namespace Vita.Testing.ExtendedTests {
  // A simple logging hook to dump all calls to SqlCache log, for debugging/tuning
  // disabled by default, to enable - define SQL_CACHE_LOG cond compilation tag in project properties
  // Note: it is activated only in TestExplorer mode, not when run as console app
  public static class SqlCacheLogHelper {

    public static void SetupSqlCacheLog() {
#if SQL_CACHE_LOG
      if(File.Exists(_cacheLogFileName))
        File.Delete(_cacheLogFileName);
      SqlCache.Debug_OnLookup = LogSqlCacheAction;
#endif 
    }

    static List<string> _cacheLog = new List<string>();
    static object _lock = new object();
    const string _cacheLogFileName = "_SqlCacheLog.log";

    private static void LogSqlCacheAction(SqlCacheKey key, SqlStatement sql) {
      var res = sql == null ? "MISS" : " hit";
      var msg = $"{res} Key= {key.Key}";
      lock(_lock) {
        _cacheLog.Add(msg);
        if(_cacheLog.Count < 20)
          return;
        FlushSqlCacheLog();
      }
    }

    internal static void FlushSqlCacheLog() {
      File.AppendAllLines(_cacheLogFileName, _cacheLog);
      _cacheLog.Clear();
    }


  }
}
