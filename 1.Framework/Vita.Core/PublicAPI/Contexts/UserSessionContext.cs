using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Services;

namespace Vita.Entities {

  public enum UserSessionStatus {
    Active = 1,
    Expired = 2,
    LoggedOut = 3,
  }

  public class UserSessionContext {
    public readonly Guid SessionId;
    public UserInfo User;
    public readonly DateTime StartedOn;
    public DateTime? ExpirationEstimate;
    public string Token; //Unique string token identifying session, placed in session cookie
    public string RefreshToken; 
    public string CsrfToken; //CSRF protection header value


    public UserSessionStatus Status {
      get { return _status;  }
      set { _status = value; _modified = true; } 
    } UserSessionStatus _status;

    // Might be different from ILogService.LogLevel
    public LogLevel LogLevel {
      get { return _logLevel; }
      set { _logLevel = value; _modified = true; }
    } LogLevel _logLevel;

    public int TimeZoneOffsetMinutes {
      get { return _timeZoneOffset; }
      set { _timeZoneOffset = value; _modified = true;  }
    } int _timeZoneOffset;

    public string UserAgent {
      get { return _userAgent; }
      set { _userAgent = value; _modified = true; }
    } string _userAgent; 

    public long Version;

    public UserSessionContext() { }

    public UserSessionContext(Guid sessionId, UserInfo user, DateTime startedOn, string token, string csrfToken, UserSessionStatus status,
                              LogLevel logLevel, long version, int timeZoneOffsetMinutes, string userAgent = null, IDictionary<string, object> values = null) {
      SessionId = sessionId;
      User = user;
      StartedOn = startedOn;
      Token = token;
      CsrfToken = csrfToken; 
      _status = status;
      _logLevel = logLevel;
      Version = version;
      _timeZoneOffset = timeZoneOffsetMinutes;
      _userAgent = userAgent; 
      if(values != null && values.Count > 0) {
        foreach(var de in values)
          _values[de.Key] = de.Value;
      }
      _modified = false; 
    }

    #region Modified
    bool _modified;
    public bool IsModified() {
      return _modified;
    }

    public void SetModified() {
      _modified = true;
    }

    public void ResetModified() {
      this._modified = false;
    }
    #endregion

    #region Values dictionary 
    ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, object> GetValues() {
      return _values;
    }
    public T GetValue<T>(string key) {
      object v;
      if(_values.TryGetValue(key, out v))
        return (T)v;
      return default(T);
    }

    public bool TryGetValue(string key, out object value) {
      value = null; 
      if (_values.TryGetValue(key, out value))
        return true;
      return false; 
    }

    public void SetValue(string key, object value) {
      _values[key] = value;
      _modified = true; 
    }

    public void RemoveValue(string key) {
      object value;
      _values.TryRemove(key, out value);
      _modified = true; 
    }
    #endregion 

  }//class
}
