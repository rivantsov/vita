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
    public string Token; //Unique string token identifying session, placed in session cookie
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

    object _lock = new object();
    bool _modified;

    public IDictionary<string, object> Values {
      get {
        if (_values == null) {
          lock (_lock) {
            if (_values == null)
            _values = new DictionaryWrapper<string, object>(
                new ConcurrentDictionary<string, object>(StringComparer.InvariantCultureIgnoreCase));
          }
        }
        return _values; 
      }
    } DictionaryWrapper<string, object> _values; //We use Dict wrapper here to track changes made to values - to user session when it is modified

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
        var dict = Values; 
        foreach(var de in values)
          dict[de.Key] = de.Value;
      }
      ResetModified();
    }

    public T GetValue<T>(string key) {
      if (_values == null)
        return default(T); 
      object v;
      if(Values.TryGetValue(key, out v))
        return (T)v;
      return default(T);
    }

    public bool TryGetValue(string key, out object value) {
      value = null; 
      if (_values == null)
        return false;
      if (Values.TryGetValue(key, out value))
        return true;
      return false; 
    }

    public void SetValue(string key, object value) {
      Values[key] = value;
    }

    public void RemoveValue(string key) {
      if (_values == null)
        return; 
      Values.Remove(key);
    }

    public bool IsModified() {
      return _modified || (_values != null && _values.Modified);
    }

    public void SetModified() {
      _modified = true; 
    }

    public void ResetModified() {
      this._modified = false;
      if (_values != null)
        _values.Modified = false; 
    }
  }//class
}
