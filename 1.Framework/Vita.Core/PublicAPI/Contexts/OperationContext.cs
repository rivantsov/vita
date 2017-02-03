using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

using Vita.Common;
using Vita.Entities.Authorization;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Caching;
using Vita.Entities.Model;
using Vita.Entities.Linq;
using Vita.Entities.Logging;
using Vita.Entities.Web;
using System.Linq.Expressions;
using System.Threading;

namespace Vita.Entities {

  public class OperationContext : IDisposable {

    public readonly EntityApp App;
    public UserInfo User;
    public string UserCulture = "EN-US";
    public MemoryLog LocalLog;
    public LogLevel LogLevel;
    public WebCallContext WebContext;
    public UserSessionContext UserSession; //might be null
    public ClientFaultList ClientFaults = new ClientFaultList();
    public DbConnectionReuseMode DbConnectionMode;
    public QueryFilter QueryFilter = new QueryFilter();
    // Disposables - to register objects that must be disposed once an operation is finished. 
    // One use is for keeping track of open connections when sessions are opened with KeepOpen value for DbConnectionMode.
    // The connection is registered in _disposables list, and will be force-closed at the end of the global operaiton.
    // At the end of web request WebCallContextHandler will call DisposeAll() to make sure all disposable objects (connections) 
    // are disposed (closed). 
    internal ConcurrentDisposableSet Disposables;


    // Name of data source when more than one is registered; null if single data source (db)
    public string DataSourceName {
      get { return _dataSourceName; }
      set {
        _dataSourceName = value;
        DataSource = null; 
      }
    } string _dataSourceName = Vita.Data.DataSource.DefaultName;
    //cached reference to datasource
    internal Vita.Data.DataSource DataSource; 

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

     
    public OperationContext(EntityApp app, UserInfo user = null, WebCallContext webContext = null, 
                       DbConnectionReuseMode connectionMode = DbConnectionReuseMode.NoReuse) {
      App = app;
      User = user ?? UserInfo.Anonymous;
      WebContext = webContext;
      DbConnectionMode = connectionMode;
      LocalLog = new MemoryLog(this);
      Disposables = new ConcurrentDisposableSet(); 
    }

    //Used for creating System-level context within user operation
    public OperationContext(OperationContext parentContext, UserInfo user) {
      App = parentContext.App;
      User = user; 
      WebContext = parentContext.WebContext;
      LocalLog = parentContext.LocalLog;
      UserSession = parentContext.UserSession;
      DataSourceName = parentContext.DataSourceName;
      DbConnectionMode = parentContext.DbConnectionMode;
      Disposables = parentContext.Disposables;

    }

    public override string ToString() {
      return "Context:" + User;
    }

    public T GetValue<T>(string key) {
      object v;
      if (Values.TryGetValue(key, out v))
        return (T)v;
      if (UserSession != null)
        return UserSession.GetValue<T>(key); 
      return default(T);
    }

    public bool TryGetValue(string key, out object value) {
      if (Values.TryGetValue(key, out value))
        return true;
      if (UserSession != null)
        return UserSession.TryGetValue(key, out value);
      return false; 
    }

    public void SetValue(string key, object value) {
      Values[key] = value; 
    }

    public void RemoveValue(string key) {
      Values.Remove(key);
    }

    public void RegisterDisposable(IDisposable disposable) {
      Disposables.AddRef(disposable); 
    }

    public void DisposeAll() {
      Disposables.DisposeAll();
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
      lock (_includesLock) {
        if (_includes.Count == 0)
          return includes;
        var copy = new List<LambdaExpression>(includes); //create copy
        copy.AddRange(_includes);
        return copy; 
      }//lock
    } //method
    #endregion

    #region nested ConcurrentDisposableSet
    public class ConcurrentDisposableSet : ConcurrentBag<WeakReference> {

      public void AddRef(IDisposable target) {
        base.Add(new WeakReference(target));
      }

      public void DisposeAll() {
        foreach(var wr in this) {
          var disp = wr.Target as IDisposable;
          if(disp != null)
            try { disp.Dispose(); } catch { }
        }
      }
    }//class
    #endregion


  }//class

}
