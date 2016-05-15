using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Services;
using Vita.Entities.Runtime;
using Vita.Entities.Model;
using Vita.Entities.Web;

namespace Vita.Modules.Logging {

  public class UserSessionModule : UserSessionModule<IUserSession> {
    public static readonly Version CurrentVersion = new Version("1.0.0.0");
    
    public UserSessionModule(EntityArea area, UserSessionSettings settings = null, string name = "UserSessionModule") 
      : base(area, settings, name) { }
  }//class

  public class UserSessionModule<TUserSession> : EntityModule, IUserSessionService
    where TUserSession: class, IUserSession
  {
    public readonly UserSessionSettings Settings;
    ITimerService _timers;

    IBackgroundSaveService _saveService;
    ObjectCache<CachedSessionItem> _userSessionCache;
    const int LastUsedIncrementSec = 10; 
    public UserSessionModule(EntityArea area, UserSessionSettings settings = null, string name = "UserSessionModule")  : base(area, name, version: UserSessionModule.CurrentVersion) {
      Settings = settings ?? new UserSessionSettings();
      App.RegisterConfig(Settings); 
      RegisterEntities(typeof(TUserSession));
      App.RegisterService<IUserSessionService>(this);
      // We need to hook to IWebCallNotificationService, but we cannot get in Init() like other services.
      // It is implemented by WebCallContextHandler which is added after EntityApp initialization. 
      // So we do it by subscribing to ServiceAdded event
      App.AppEvents.ServiceAdded += AppEvents_ServiceAdded;
    }

    public override void Init() {
      base.Init();
      _userSessionCache = new ObjectCache<CachedSessionItem>("UserSessonCache", Settings.MemoryCacheExpirationSec);
      _saveService = App.GetService<IBackgroundSaveService>();
      _timers = App.GetService<ITimerService>();
      _timers.Elapsed1Second += Timers_Elapsed1Second;
    }

    private IWebCallNotificationService _webCallNotifications; 
    void AppEvents_ServiceAdded(object sender, ServiceEventArgs e) {
      if (e.ServiceType == typeof(IWebCallNotificationService)) {
        _webCallNotifications = (IWebCallNotificationService) e.ServiceInstance;
        _webCallNotifications.WebCallStarting += WebCallNotifications_WebCallStarting;
        _webCallNotifications.WebCallCompleting += WebCallNotifications_WebCallCompleting;
      }
    }


    #region nested classes CachedSessionItem

    internal class CachedSessionItem {
      public UserSessionContext UserSession;
      public UserSessionExpiration Expiration;
      public DateTime LastUsedOn;
      public CachedSessionItem(UserSessionContext userSession, UserSessionExpiration expiration, DateTime lastUsedOn) {
        UserSession = userSession;
        Expiration = expiration;
        LastUsedOn = lastUsedOn;
      }
    }

    #endregion

    //Default generator of session tokens
    public static string DefaultSessionTokenGenerator() {
      var token = Guid.NewGuid().ToString() + "_" + RandomHelper.GenerateRandomString(20);   
      return token;
    }

    #region IUserSessionService members

    public UserSessionContext StartSession(OperationContext context, UserInfo user, UserSessionExpiration expiration = null) {
      var now = App.TimeService.UtcNow;
      if(expiration == null)
        expiration = new UserSessionExpiration() { 
          ExpirationType = UserSessionExpirationType.Sliding, 
          FixedExpiration = now.AddMonths(1), 
          SlidingWindow = Settings.SessionTimeout };
      //create session entity
      var entUserSession = NewUserSessionEntity(context, user, expiration);
      var cacheItem = CacheUserSession(entUserSession);
      return cacheItem.UserSession;
    }

    public void AttachSession(OperationContext context, string sessionToken, long sessionVersion = 0, string csrfToken = null) {
      if(string.IsNullOrWhiteSpace(sessionToken))
        return; 
      //Check cache
      var cacheItem = _userSessionCache.Lookup(sessionToken);
      var cachedOk = cacheItem != null && CheckVersion(context, cacheItem, sessionVersion);
      if (!cachedOk) {
        //Read from database
        var iEnt = LoadUserSessionEntity(context, sessionToken);
        if(iEnt == null || iEnt.Status != UserSessionStatus.Active)
          return;
        cacheItem = CacheUserSession(iEnt);
      }
      var now = App.TimeService.UtcNow;
      var cachedSession = cacheItem.UserSession; 
      CheckExpiration(context, cacheItem, now);
      switch(cacheItem.UserSession.Status) {
        case UserSessionStatus.Active:
          context.UserSession = cachedSession;
          context.User = cachedSession.User;
          // Save LastUsedOnd if more than 10 seconds ago
          if (cacheItem.LastUsedOn.AddSeconds(LastUsedIncrementSec) < now) 
            ScheduleUpdateLastUsedOn(cachedSession.SessionId, now);
          cacheItem.LastUsedOn = now;
          break; 
        default:
          DeleteSessionCacheItem(cacheItem);
          SaveUserSessionEntity(context, cacheItem.UserSession);
          break; 
      } //switch
    }

    public void UpdateSession(OperationContext context) {
      if (context.UserSession != null)
        SaveUserSessionEntity(context, context.UserSession);
    }

    public void EndSession(OperationContext context) {
      var userSession = context.UserSession;
      if(userSession == null)
        return; 
      _userSessionCache.Remove(userSession.Token);

      userSession.Status = UserSessionStatus.LoggedOut;
      userSession.Version++; 
      //save entity session
      SaveUserSessionEntity(context, context.UserSession);
    }
    #endregion


    #region WebCall start/end handling
    void WebCallNotifications_WebCallStarting(object sender, WebCallEventArgs e) {
      var ctx = e.WebContext;
       //CSRF token
      string csrfToken = null;
      this.AttachSession(ctx.OperationContext, ctx.UserSessionToken, ctx.MinUserSessionVersion,  csrfToken);
    }

    void WebCallNotifications_WebCallCompleting(object sender, WebCallEventArgs e) {
      var webContext = e.WebContext;
      var userSession = webContext.OperationContext.UserSession;
      if (userSession == null)
        return;
      if (userSession.IsModified())
        this.SaveSessionValues(webContext.OperationContext);
      webContext.MinUserSessionVersion = userSession.Version;
    }
    #endregion

    private void SaveSessionValues(OperationContext context) {
      var userSession = context.UserSession;
      userSession.Version++;
      var entSession = context.OpenSystemSession();
      var serValues = SerializeSessionValues(userSession);
      var updateQuery = entSession.EntitySet<IUserSession>().Where(s => s.Id == userSession.SessionId)
        .Select(s => new { Id = s.Id, Values = serValues, Version = userSession.Version });
      updateQuery.ExecuteUpdate<IUserSession>();
      userSession.ResetModified(); 
    }

    private bool CheckVersion(OperationContext context, CachedSessionItem item, long requestedVersion) {
      return (requestedVersion <= item.UserSession.Version);
    }

    private void CheckExpiration(OperationContext context, CachedSessionItem item, DateTime now) {
      var expiration = item.Expiration;
      var lastUsedMin = now.Subtract(expiration.SlidingWindow);
      var userSession = item.UserSession; 
      if(item.UserSession.Status != UserSessionStatus.Active)
        return; 
      var expTerms = item.Expiration; 
      switch(expTerms.ExpirationType) {
        case UserSessionExpirationType.Sliding:
          if (item.LastUsedOn < lastUsedMin) //expired? first refresh LastActiveOn value, maybe it is stale, and check again
            RefreshLastActive(context, item);
          if (item.LastUsedOn < lastUsedMin)
            userSession.Status = UserSessionStatus.Expired;
          break; 
        case UserSessionExpirationType.FixedTerm:
        case UserSessionExpirationType.KeepLoggedIn:
          if(expiration.FixedExpiration < now)
            userSession.Status = UserSessionStatus.Expired; 
          break; 
      }
    }

    private void RefreshLastActive(OperationContext context, CachedSessionItem item) {
      var session = context.OpenSystemSession();
      var lastActive = session.EntitySet<IUserSession>().Where(s => s.Id == item.UserSession.SessionId).Select(s => s.LastUsedOn).FirstOrDefault();
      if (lastActive != default(DateTime))
        item.LastUsedOn = lastActive;
    }

    private IUserSession NewUserSessionEntity(OperationContext context, UserInfo user, UserSessionExpiration expiration) {
      var now = App.TimeService.UtcNow;
      var session = context.OpenSystemSession(); 
      var ent = session.NewEntity<IUserSession>();
      ent.StartedOn = now;
      ent.LastUsedOn = now; 
      ent.UserId = user.UserId;
      ent.AltUserId = user.AltUserId;
      ent.UserName = user.UserName;
      ent.UserKind = user.Kind;
      ent.Status = UserSessionStatus.Active;
      var webCtx = context.WebContext; 
      if (webCtx != null) {
        ent.CreatedByWebCallId = webCtx.Id;
        ent.IPAddress = webCtx.IPAddress;
        var agentData = webCtx.GetIncomingHeader("User-Agent");
        if (agentData != null && agentData.Count > 0) {
          var agent = string.Join(",", agentData);
          ent.UserOS = GetUserOS(agent);
          if (agent != null && agent.Length > 99)
            agent = agent.Substring(0, 100);
          ent.UserAgent = agent;
        }
      }
      ent.ExpirationType = expiration.ExpirationType;
      ent.FixedExpiration = expiration.FixedExpiration;
      ent.ExpirationWindowSeconds = (int)expiration.SlidingWindow.TotalSeconds;
      var token = ent.WebSessionToken = Settings.SessionTokenGenerator();
      ent.CsrfToken = RandomHelper.GenerateRandomString(10);
      session.SaveChanges();
      return ent;
    }

    private string GetUserOS(string userAgent) {
      if (string.IsNullOrWhiteSpace(userAgent))
        return null;
      if (userAgent.Contains("Win")) return "Windows";
      if (userAgent.Contains("Mac")) return "MacOS";
      if (userAgent.Contains("X11")) return "UNIX";
      if (userAgent.Contains("Linux")) return "Linux";
      return null; 

    }

    private IUserSession SaveUserSessionEntity(OperationContext context, UserSessionContext userSession) {
      userSession.Version++;
      var session = context.OpenSystemSession();
      var ent = session.GetEntity<IUserSession>(userSession.SessionId);

      if(ent.Status == UserSessionStatus.Active && userSession.Status != UserSessionStatus.Active)
        ent.EndedOn = App.TimeService.UtcNow;
      ent.Status = userSession.Status;
      ent.Version = Math.Max(ent.Version, userSession.Version);
      ent.Values = SerializeSessionValues(userSession);
      ent.TimeZoneOffsetMinutes = userSession.TimeZoneOffsetMinutes;
      ent.UserAgent = userSession.UserAgent; 
      session.SaveChanges();
      userSession.ResetModified(); 
      return ent;
    }

    private string SerializeSessionValues(UserSessionContext userSession) {
      if (userSession.Values.Count == 0)
        return null;
      else
        return XmlSerializationHelper.SerializeDictionary(userSession.Values);

    }

    private IUserSession LoadUserSessionEntity(OperationContext context, string token) {
      var session = context.OpenSystemSession();
      var hash = Util.StableHash(token);
      var query = from uss in session.EntitySet<IUserSession>()
                  where uss.WebSessionTokenHash == hash && uss.WebSessionToken == token
                  select uss;
      var ent = query.FirstOrDefault();
      return ent; 
    }

    private CachedSessionItem CacheUserSession(IUserSession ent) {
      if(ent == null)
        return null; 
      var token = ent.WebSessionToken; 
      var userInfo = UserInfo.Create(ent.UserKind, ent.UserId, ent.AltUserId, ent.UserName);
      // load Values dictionary
      Dictionary<string, object> valuesDict = null; 
      var xml = ent.Values;
      if (!string.IsNullOrWhiteSpace(xml)) {
        valuesDict = new Dictionary<string, object>();
        XmlSerializationHelper.DeserializeDictionary(xml, valuesDict); 
      }
      var userSession = new UserSessionContext(
         ent.Id, userInfo, ent.StartedOn, ent.WebSessionToken, ent.CsrfToken, ent.Status, ent.LogLevel, 
         ent.Version, ent.TimeZoneOffsetMinutes,ent.UserAgent, valuesDict);
      var expiration = new UserSessionExpiration() {
        ExpirationType = ent.ExpirationType,
        FixedExpiration = ent.FixedExpiration, SlidingWindow = TimeSpan.FromSeconds(ent.ExpirationWindowSeconds)
      };
      var cacheItem = new CachedSessionItem(userSession, expiration, ent.LastUsedOn); 
      _userSessionCache.Add(token, cacheItem);
      return cacheItem;
    }

    private void DeleteSessionCacheItem(CachedSessionItem item) {
      _userSessionCache.Remove(item.UserSession.Token);
    }

    #region Handling LastUsedOn updates


    internal class LastUsedOnSaveObject: IObjectSaveHandler {
      IList<Guid> _sessionIds;
      DateTime _lastUsed; 
      public LastUsedOnSaveObject(IList<Guid> sessionIds, DateTime lastUsed) {
        _sessionIds = sessionIds;
        _lastUsed = lastUsed; 
      }
      public void SaveObjects(IEntitySession session, IList<object> items) {
        //Schedule update query that will update all LastActiveOn columns for all sessions with ID in the hashset
        //We could use DateTime.Now as value for LastUsedOn for all sessions, it is within few seconds, but... 
        // this causes problems for unit tests for session expiration, when we try to shift current time and see if session is expired;
        // (with current sessions would be updated to shifted time). So we keep actual time (latest) while we accumulate sessions for update
        var query = session.EntitySet<IUserSession>().Where(s => _sessionIds.Contains(s.Id)).Select(s => new { Id = s.Id, LastUsedOn = _lastUsed });
        session.ScheduleNonQuery<IUserSession>(query, Vita.Entities.Linq.LinqCommandType.Update);      
      }
    }//class

    //Temp list to accumulate session Ids to update LastUsedOn
    object _lock = new object();
    HashSet<Guid> _sessionsToUpdateLastUsedOn = new HashSet<Guid>();
    DateTime _lastUsedOnValueForGroupUpdate;
    private void ScheduleUpdateLastUsedOn(Guid sessionId, DateTime lastUsed) {
      lock (_lock) {
        _sessionsToUpdateLastUsedOn.Add(sessionId);
        _lastUsedOnValueForGroupUpdate = lastUsed; 
      }
    }
    //Schedules to mass update all LastUsedOn
    void Timers_Elapsed1Second(object sender, EventArgs e) {
      lock (_lock) {
        if (_sessionsToUpdateLastUsedOn.Count == 0)
          return;
        var saveObject = new LastUsedOnSaveObject(_sessionsToUpdateLastUsedOn.ToList(), _lastUsedOnValueForGroupUpdate);
        _saveService.AddObject(saveObject);
        _sessionsToUpdateLastUsedOn.Clear(); 
      }
    }
    #endregion

  }
}
