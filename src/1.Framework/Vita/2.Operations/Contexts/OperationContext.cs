﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

using Vita.Entities.Api;
using System.Linq.Expressions;
using System.Threading;

using Vita.Entities.Model;
using Vita.Entities.Logging;
using Vita.Data.Runtime;

namespace Vita.Entities {

  /// <summary>Determines if DB connection should be kept open in EntitySession. </summary>
  public enum DbConnectionReuseMode {
    /// <summary>Do not reuse connection, close it immediately after operation completes. </summary>
    NoReuse,
    /// <summary>
    /// Do not close connection, wait for next operation. Best for Web applications. 
    /// At the end of web request processing all open connections will be closed anyway. 
    /// </summary>
    KeepOpen,
  }

  public class OperationContext : IDisposable {
    public EntityApp App { get; internal set; }
    public string UserCulture { get; set; } = "EN-US";
    public WebCallContext WebContext { get; set; }
    public ILog Log;
    public DbConnectionReuseMode DbConnectionMode { get; set; }
    public QueryFilter QueryFilter { get; } = new QueryFilter();

    internal DataSource LastDataSource; //last data source reference

    public UserInfo User {
      get => _user;  
      set {
        _user = value;
        _logContext = null; 
      } 
    } UserInfo _user; 

    public LogContext LogContext {
      get {
        return _logContext = _logContext ?? new LogContext(this);
      }
    } LogContext _logContext; 

    public UserSessionBase UserSession {
      get { return _userSession; }
      set {
        _userSession = value;
        _logContext = null; //force it to refresh
      }
    } UserSessionBase _userSession; 

    // Disposables - to register objects that must be disposed once an operation is finished. 
    // One use is for keeping track of open connections when sessions are opened with KeepOpen value for DbConnectionMode.
    // The connection is registered in _disposables list, and will be force-closed at the end of the global operaiton.
    // At the end of web request WebCallContextHandler will call DisposeAll() to make sure all disposable objects (connections) 
    // are disposed (closed). 
    ConcurrentBag<WeakReference> _disposables; //created on first use

    public IList<ClientFault> ClientFaults => _clientFaults.ToList();
    ConcurrentBag<ClientFault> _clientFaults = new ConcurrentBag<ClientFault>(); 

    // Name of data source when more than one is registered; null if single data source (db)
    public string DataSourceName = Vita.Data.Runtime.DataSource.DefaultName;
    //cached reference to datasource
    //internal Vita.Data.DataSource DataSource; 

    public IDictionary<string, object> Values {
      get { 
        if(_values == null)
          _values = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase); 
        return _values; 
      }
    } IDictionary<string, object> _values;


    // The version of the global cache that was used last time when processing user request/operation. 
    // It is used to catch the occurrence when entity cache is stale. In web farms, when user request 
    // is routed to a server that has 'older' cache than cache that was used in previous user's operation - this is 
    // a clear indicator of a 'stale' cache on this server. The cache should be refreshed before executing the request.  
    public long EntityCacheVersion;
     
    public OperationContext(EntityApp app, UserInfo user = null, WebCallContext webContext = null, ILog log = null, 
                       DbConnectionReuseMode connectionMode = DbConnectionReuseMode.NoReuse ) {
      App = app; 
      User = user ?? UserInfo.Anonymous;
      WebContext = webContext;
      Log = log ?? app.LogService;
      DbConnectionMode = connectionMode;
    }

    public OperationContext CreateChildContext(UserInfo user) {
      var ctx = new OperationContext(this.App, user, this.WebContext, this.Log, this.DbConnectionMode);
      ctx.DataSourceName = this.DataSourceName;
      RegisterDisposable(ctx); 
      return ctx; 
    }


    public override string ToString() {
      return "Context:" + User;
    }

    public T GetValue<T>(string key) {
      object v;
      if (Values.TryGetValue(key, out v))
        return (T)v;
      return default(T);
    }

    public bool TryGetValue(string key, out object value) {
      if (Values.TryGetValue(key, out value))
        return true;
      return false; 
    }

    public void SetValue(string key, object value) {
      if (value == null) {
        if(_values.ContainsKey(key))
          _values.Remove(key);
      }
      Values[key] = value; 
    }

    public void RemoveValue(string key) {
      Values.Remove(key);
    }

    public void RegisterDisposable(IDisposable disposable) {
      _disposables = _disposables ?? new ConcurrentBag<WeakReference>(); 
      if (_disposables.Count > 50) {
        var allAlive = _disposables.ToArray().Where(wr => wr.IsAlive);
        _disposables = new ConcurrentBag<WeakReference>(allAlive); 
      }
      _disposables.Add(new WeakReference(disposable)); 
    }

    public void DisposeAll() {
      if (_disposables == null || _disposables.Count == 0)
        return; 
      var allAlive = _disposables.ToArray().Where(wr => wr.IsAlive);
      foreach(var wr in allAlive)
        (wr.Target as IDisposable)?.Dispose();
      _disposables = null; 
    }

    public void Dispose() {
      DisposeAll();
    }

    #region CancellationToken
    // We combine here 2 cancellation tokens. External token comes from Web/ASP.NET handler 
    // (see WebCallContextHandler.SendAsync). We want also to be able to cancel internally, 
    // so we create internal token source and token (on first read). We use internal token 
    // in code (WebApiClient uses this token). We propagate external cancelation to internal
    // token.  
    CancellationToken _externalCancellationToken;
    public void SetExternalCancellationToken(CancellationToken token) {
      _externalCancellationToken = token;
      _externalCancellationToken.Register(() => SignalCancellation());
    }

    public CancellationToken CancellationToken {
      get {
        _cancellationTokenSource = _cancellationTokenSource ?? new CancellationTokenSource();
        return _cancellationTokenSource.Token;
      }
    }  CancellationTokenSource _cancellationTokenSource;

    public void SignalCancellation() {
      if(_cancellationTokenSource == null)
        return;
      _cancellationTokenSource.Cancel();
    }

    public void ThrowIfCancelled() {
      if(_cancellationTokenSource != null && _cancellationTokenSource.IsCancellationRequested)
        throw new OperationAbortException("Operation cancelled.", "Cancelled.");
    }
    #endregion 


    #region Includes
    List<LambdaExpression> _includes;
    private object _includesLock = new object();


    /// <summary>Adds Include expression for all LINQ queries executed through sessions attached to this context. </summary>
    /// <param name="include">Include expression.</param>
    public OperationContext AddInclude<TEntity>(Expression<Func<TEntity, object>> include) {
      Util.Check(include != null, "'include' parameter may not be null.");
      //validate
      lock (_includesLock) {
        if (_includes == null)
          _includes = new List<LambdaExpression>();
        _includes.Add(include); 
      }
      return this; 
    }

    /// <summary>Removes Include expressions from internal list of Includes. </summary>
    /// <param name="include">Include expressions to remove.</param>
    /// <returns>True if an Include expression was matched and removed; otherwise, false.</returns>
    /// <remarks>Tries to match expressions first as object, then by ToString() representation.</remarks>
    public bool RemoveInclude<TEntity>(Expression<Func<TEntity, object>> include) {
      Util.Check(include != null, "'include' parameter may not be null.");
      lock (_includesLock) {
          var found = _includes.IndexOf(include); //try match as objects
          if (found >= 0) {
            _includes.RemoveAt(found);
            return true; 
          }
          var incToString = include.ToString();
          var match = _includes.FirstOrDefault(inc => inc.ToString() == incToString);
          if (match != null) {
            _includes.Remove(match);
            return true; 
          }
        return false; 
      }//lock
    }

    public bool HasIncludes() {
      var incs = _includes;
      return incs != null && incs.Count > 0; 
    }

    public ICollection<LambdaExpression> GetIncludes() {
      var result = new List<LambdaExpression>();
      if (_includes == null)
        return result; 
      lock (_includesLock) {
          result.AddRange(_includes);
      }
      return result; 
    }

    // internal use, tries to efficiently merge two lists. 
    internal IList<LambdaExpression> GetMergedIncludes(IList<LambdaExpression> includes) {
      if (_includes == null)
        return includes;
      if(includes == null)
        return _includes; 
      lock (_includesLock) {
        if (_includes.Count == 0)
          return includes;
        var copy = new List<LambdaExpression>(includes); //create copy
        copy.AddRange(_includes);
        return copy; 
      }//lock
    } //method
    #endregion


    #region ClientFaults, Validation

    public void ThrowValidation() {
      if(ClientFaults.Count == 0)
        return;
      var cfex = new ClientFaultException(_clientFaults.ToArray());
      throw cfex;
    }//method

    public void AddClientFault(ClientFault fault) {
      _clientFaults.Add(fault); 
    }

    public ClientFault[] GetClientFaults() {
      return _clientFaults.ToArray(); 
    }

    public bool HasClientFaults() {
      return _clientFaults.Count > 0; 
    }

    #endregion

    public string GetLogContents() {
      var bufLog = this.Log as BufferedLog; 
      if(bufLog == null)
        return null;
      var entries = bufLog.GetAll();
      return string.Join(Environment.NewLine, entries);
    }

  }//class

} //ns
