using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Vita.Data;
using Vita.Data.Runtime;
using Vita.Entities.Runtime;

namespace Vita.Entities {
  public static class DataExtensions {

    public static IDirectDbConnector GetDirectDbConnector(this IEntitySession session, bool admin = false) {
      Util.CheckParam(session, nameof(session));
      var conn = new DirectDbConnector((EntitySession) session, admin);
      return conn; 
    }

    public static void ExecuteNonQuery(this IEntitySession session, string sql, params object[] args) {
      var sqlStmt = string.Format(sql, args);
      var dbConn = session.GetDirectDbConnector().DbConnection;
      var cmd = dbConn.CreateCommand();
      cmd.CommandText = sqlStmt;
      var isClosed = dbConn.State != System.Data.ConnectionState.Open;
      if(isClosed)
        dbConn.Open();
      cmd.ExecuteNonQuery();
      if(isClosed)
        dbConn.Close();
    }

  }

}
