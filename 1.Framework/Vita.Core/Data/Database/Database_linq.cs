using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Data;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Linq;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Linq;

namespace Vita.Data {

  
  //Dynamic LINQ execution
  public partial class Database {

    public object ExecuteLinqCommand(EntitySession session, LinqCommand command) {
      var conn = GetLinqCommandConnection(session, command.Info.Flags);
      try {
        object result = command.CommandType == LinqCommandType.Select ?
          ExecuteLinqSelect(command, session, conn) : 
          ExecuteLinqNonQuery(command, session, conn);
        ReleaseConnection(conn);
        return result; 
      } catch (Exception dex) {
        ReleaseConnection(conn, inError: true);
        dex.AddValue(DataAccessException.KeyLinqQuery, command.QueryExpression);
        throw;
      } 
    }

    //we need to check if we require transaction (if we are setting LOCK)
    private DataConnection GetLinqCommandConnection(EntitySession session, LinqCommandFlags flags) {
      var conn = GetConnection(session);
      if (conn.DbTransaction == null && flags.IsSet(LinqCommandFlags.ReadLock | LinqCommandFlags.WriteLock)) {
        //We need to start transaction with proper isolation level. We also need to associate connection with session, 
        // so it will stay there and will be reused for all coming operations
        if (conn.Lifetime < ConnectionLifetime.Transaction)
          conn.Lifetime = ConnectionLifetime.Transaction; 
        session.CurrentConnection = conn;
        var isoLevel = _driver.GetIsolationLevel(flags); 
        conn.BeginTransaction(commitOnSave: true, isolationLevel: isoLevel);
      }
      return conn; 
    }

    private object ExecuteLinqNonQuery(LinqCommand linqCommand, EntitySession session, DataConnection connection) {
      var translCmd = GetTranslateLinqCommand(linqCommand); 
      var dbCommand = CreateLinqDbCommand(connection, linqCommand, translCmd);
      var result = ExecuteDbCommand(dbCommand, connection, DbExecutionType.NonQuery);
      return result;
    }

    public object ExecuteLinqSelect(LinqCommand linqCommand, EntitySession session, DataConnection conn) {
      var translCmd = GetTranslateLinqCommand(linqCommand); 
      //Locks require ongoing transaction
      object result;
      var dbCommand = CreateLinqDbCommand(conn, linqCommand, translCmd);
      IList resultList = translCmd.ResultListCreator();
      ExecuteDbCommand(dbCommand, conn, DbExecutionType.Reader, reader => {
        while(reader.Read()) {
          var row = translCmd.ObjectMaterializer(reader, session);
          //row might be null if authorization filtered it out or if it is empty value set from outer join
          if(row != null)
            resultList.Add(row);
        }
        return resultList.Count;
      });
      //Post processor is extra selection op from the query (Fist,Single,Last)
      var postProcessor = translCmd.ResultsPostProcessor;
      if (postProcessor != null)
        result = postProcessor.ProcessRows(resultList);
      else
        result = resultList; 
     return result;
    }

    // Looks up SQL query in query cache; if not found, builds SqlQuery object and saves in cache.
    private TranslatedLinqCommand GetTranslateLinqCommand(LinqCommand command) {
      var cmdInfo = command.Info; 
      //Lookup in cache SQL query or build it
      var translCmd = this.DbModel.QueryCache.Lookup(cmdInfo.CacheKey);
      if(translCmd != null)
        return translCmd;
      //Build sqlQuery if not found
      var engine = new Vita.Data.Linq.Translation.LinqEngine(this.DbModel);
      translCmd = engine.Translate(command);
      // save in cache
      var canCache = !cmdInfo.Options.IsSet(QueryOptions.NoQueryCache) && !translCmd.Flags.IsSet(LinqCommandFlags.NoQueryCache); 
      if (canCache) 
        DbModel.QueryCache.Add(cmdInfo.CacheKey, translCmd);
      return translCmd;
    }

    private IDbCommand CreateLinqDbCommand(DataConnection connection, LinqCommand linqCommand, TranslatedLinqCommand translatedCommand) {
      var cmd = connection.DbConnection.CreateCommand(); 
      cmd.CommandType = CommandType.Text;
      cmd.CommandText = translatedCommand.Sql;
      foreach (var qParam in translatedCommand.Parameters) {
        var value = qParam.ReadValue(linqCommand.ParameterValues) ?? DBNull.Value;
        var dbParam = cmd.CreateParameter(); //DbModel.Driver.AddParameter(cmd,  // 
        dbParam.ParameterName = qParam.Name;
        //Value and parameter may need some tweaking, depending on server type
        DbModel.LinqSqlProvider.SetDbParameterValue(dbParam, qParam.Type, value);
        cmd.Parameters.Add(dbParam);
      }
      return cmd;
    }

  }
}
