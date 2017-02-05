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
    where TUserSession: class, IUserSession {
    public readonly UserSessionSettings Settings;
    OperationContext _opContext; 
    ITimerService _timers;
    IBackgroundSaveService _saveService;
    //private IWebCallNotificationService _webCallNotificationService;
    ObjectCache<string, CachedSessionItem> _userSessionCache;
    const int LastUsedIncrementSec = 10; 

    public UserSessionModule(EntityArea area, UserSessionSettings settings = null, string name = "UserSessionModule")  : base(area, name, version: UserSessionModule.CurrentVersion) {
      Settings = settings ?? new UserSessionSettings();
      App.RegisterConfig(Settings); 
      RegisterEntities(typeof(TUserSession));
      App.RegisterService<IUserSessionService>(this);
    }

    public override void Init() {
      base.Init();
      _opContext = App.CreateSystemContext();
      _userSessionCache = new ObjectCache<string, CachedSessionItem>(
        expirationSeconds: Settings.MemoryCacheExpirationSec, maxLifeSeconds: Settings.MemoryCacheExpirationSec);
      _saveService = App.GetService<IBackgroundSaveService>();
      _timers = App.GetService<ITimerService>();
      _timers.Elapsed1Second += Timers_Elapsed1Second;
    }

    private IEntitySession OpenEntitySession() {
      return _opContext.OpenSession(); 
    }

    #region nested classes CachedSessionItem

    internal class CachedSessionItem {
      public UserSessionContext UserSession;
      public UserSessionExpirationType ExpirationType;
      public DateTime? ExpiresOn; 
      public DateTime LastUsedOn;
      public CachedSessionItem(UserSessionContext userSession, TUserSession sessionEntity) {
        UserSession = userSession;
        ExpirationType = sessionEntity.ExpirationType;
        ExpiresOn = sessionEntity.FixedExpiration;
        LastUsedOn = sessionEntity.LastUsedOn;
      }
    }
    #endregion

    //Default generator of session tokens
    public static string DefaultSessionTokenGenerator() {
      var token = Guid.NewGuid().ToString() + "_" + RandomHelper.GenerateRandomString(20);   
      return token;
    }

    #region IUserSessionService members

    public UserSessionContext StartSession(OperationContext context, UserInfo user, UserSessionExpirationType expirationType = UserSessionExpirationType.Sliding) {
      var utcNow = App.TimeService.UtcNow;
      //create session entity
      var entUserSession = CreateUserSessionInDb(user, context.WebContext, expirationType);
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
        var iEnt = LoadUserSessionEntity(sessionToken);
        if(iEnt == null || iEnt.Status != UserSessionStatus.Active)
          return;
        cacheItem = CacheUserSession(iEnt);
      }
      var utcNow = App.TimeService.UtcNow;
      var cachedSession = cacheItem.UserSession; 
      CheckExpiration(cacheItem, utcNow);
      switch(cacheItem.UserSession.Status) {
        case UserSessionStatus.Active:
          context.UserSession = cachedSession;
          context.User = cachedSession.User;
          // Save LastUsedOnd if more than 10 seconds ago
          if (cacheItem.LastUsedOn.AddSeconds(LastUsedIncrementSec) < utcNow) 
            ScheduleUpdateLastUsedOn(cachedSession.SessionId, utcNow);
          cacheItem.LastUsedOn = utcNow;
          break; 
        default:
          DeleteSessionCacheItem(cacheItem);
          SaveUserSessionEntity(cacheItem.UserSession);
          break; 
      } //switch
    }

    public void UpdateSession(OperationContext context) {
      if (context.UserSession != null)
        SaveUserSessionEntity(context.UserSession);
    }

    public void EndSession(OperationContext context) {
      var userSession = context.UserSession;
      if(userSession == null)
        return; 
      _userSessionCache.Remove(userSession.Token);

      userSession.Status = UserSessionStatus.LoggedOut;
      //save entity session
      SaveUserSessionEntity(context.UserSession);
    }

    public string RefreshSessionToken(OperationContext context, string refreshToken) {
      var oldToken = context.UserSession.Token;
      //remove from cache
      _userSessionCache.Remove(oldToken);
      // Note that iEnt is attached to different session (hooked to LoggingApp), not connected to 'context' we have here
      var iEnt = LoadUserSessionEntity(oldToken);
      context.ThrowIfNull(iEnt, ClientFaultCodes.ObjectNotFound, "Token", "User session does not exist.");
      context.ThrowIfEmpty(iEnt.RefreshToken, ClientFaultCodes.InvalidAction, "RefreshToken", "User session does not allow refresh, refresh token not found in session.");
      var refreshMatches = string.Equals(iEnt.RefreshToken, refreshToken, StringComparison.OrdinalIgnoreCase);
      context.ThrowIf(!refreshMatches, ClientFaultCodes.InvalidValue, "RefreshToken", "Invalid refresh token.");
      var newToken = Settings.SessionTokenGenerator();
      var newRefreshToken = Settings.SessionTokenGenerator();
      iEnt.WebSessionToken = newToken;
      iEnt.WebSessionTokenCreatedOn = context.App.TimeService.UtcNow;
      iEnt.RefreshToken = newRefreshToken; 
      UpdateFixedExpiration(iEnt); 
      var entSession = EntityHelper.GetSession(iEnt);
      entSession.SaveChanges();
      context.UserSession.Token = newToken;
      if(context.WebContext != null)
        context.WebContext.UserSessionToken = newToken;
      //log event in login log
      var loginLog = App.GetService<ILoginLogService>();
      if (loginLog != null)
        loginLog.LogEvent(context, Login.LoginEventType.TokenRefreshed, notes: StringHelper.SafeFormat("Token refreshed, new expiration: {0}.", iEnt.FixedExpiration));
      return newRefreshToken; 
    }

    #endregion

    private void UpdateFixedExpiration(TUserSession sessionEntity) {
      var utcNow = App.TimeService.UtcNow; 
      switch(sessionEntity.ExpirationType) {
        case UserSessionExpirationType.FixedTerm:
          sessionEntity.FixedExpiration = utcNow.Add(Settings.SessionTimeout);
          break;
        case UserSessionExpirationType.LongFixedTerm:
          sessionEntity.FixedExpiration = utcNow.Add(Settings.LongSessionTimeout);
          break;
      }

    }

    private void SaveSessionValues(UserSessionContext userSession) {
      userSession.Version++;
      var entSession = OpenEntitySession();
      var serValues = SerializeSessionValues(userSession);
      var updateQuery = entSession.EntitySet<TUserSession>().Where(s => s.Id == userSession.SessionId)
        .Select(s => new { Id = s.Id, Values = serValues, Version = userSession.Version });
      updateQuery.ExecuteUpdate<TUserSession>();
      userSession.ResetModified(); 
    }

    private bool CheckVersion(OperationContext context, CachedSessionItem item, long requestedVersion) {
      return (requestedVersion <= item.UserSession.Version);
    }

    private void CheckExpiration(CachedSessionItem item, DateTime utcNow) {
      var userSession = item.UserSession; 
      if(userSession.Status != UserSessionStatus.Active)
        return; 
      switch(item.ExpirationType) {
        case UserSessionExpirationType.Sliding:
          var expires = item.LastUsedOn.Add(Settings.SessionTimeout);
          if(expires < utcNow) { //expired? first refresh LastActiveOn value, maybe it is stale, and check again
            RefreshLastActive(item);
            expires = item.LastUsedOn.Add(Settings.SessionTimeout);
          }
          if(expires < utcNow) {
            userSession.Status = UserSessionStatus.Expired;
            return; 
          }
          userSession.ExpirationEstimate = utcNow.Add(Settings.SessionTimeout); 
          break; 

        case UserSessionExpirationType.FixedTerm:
        case UserSessionExpirationType.LongFixedTerm:
          if(item.ExpiresOn < utcNow)
            userSession.Status = UserSessionStatus.Expired;
          else
            userSession.ExpirationEstimate = item.ExpiresOn; 
          break;
        case UserSessionExpirationType.KeepLoggedIn:
          return; 
      }
    }

    private void RefreshLastActive(CachedSessionItem item) {
      var session = OpenEntitySession();
      var lastActive = session.EntitySet<TUserSession>().Where(s => s.Id == item.UserSession.SessionId)
          .Select(s => s.LastUsedOn).FirstOrDefault();
      if (lastActive != default(DateTime))
        item.LastUsedOn = lastActive;
    }

    private TUserSession CreateUserSessionInDb(UserInfo user, WebCallContext webContext, UserSessionExpirationType expirationType) {
      var now = App.TimeService.UtcNow;
      var session = OpenEntitySession(); 
      var ent = session.NewEntity<TUserSession>();
      ent.StartedOn = now;
      ent.LastUsedOn = now; 
      ent.UserId = user.UserId;
      ent.AltUserId = user.AltUserId;
      ent.UserName = user.UserName;
      ent.UserKind = user.Kind;
      ent.Status = UserSessionStatus.Active;
      if (webContext != null) {
        ent.CreatedByWebCallId = webContext.Id;
        ent.IPAddress = webContext.IPAddress;
        var agentData = webContext.GetIncomingHeader("User-Agent");
        if (agentData != null && agentData.Count > 0) {
          var agent = string.Join(",", agentData);
          ent.UserOS = GetUserOS(agent);
          if (agent != null && agent.Length > 99)
            agent = agent.Substring(0, 100);
          ent.UserAgent = agent;
        }
      }
      ent.ExpirationType = expirationType;
      UpdateFixedExpiration(ent); 
      ent.ExpirationWindowSeconds = (int)Settings.SessionTimeout.TotalSeconds;
      var token = ent.WebSessionToken = Settings.SessionTokenGenerator();
      if(expirationType == UserSessionExpirationType.LongFixedTerm)
        ent.RefreshToken = Settings.SessionTokenGenerator(); 
      ent.WebSessionTokenCreatedOn = now;
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

    private TUserSession SaveUserSessionEntity(UserSessionContext userSession) {
      userSession.Version++;
      var session = OpenEntitySession();
      var ent = session.GetEntity<TUserSession>(userSession.SessionId);

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
      var values = userSession.GetValues(); 
      if (values.Count == 0)
        return null;
      else
        return XmlSerializationHelper.SerializeDictionary(values);
    }

    private TUserSession LoadUserSessionEntity(string token) {
      var session = OpenEntitySession();
      var hash = Util.StableHash(token);
      var query = from uss in session.EntitySet<TUserSession>()
                  where uss.WebSessionTokenHash == hash && uss.WebSessionToken == token
                  select uss;
      var ent = query.FirstOrDefault();
      return ent; 
    }

    private CachedSessionItem CacheUserSession(TUserSession ent) {
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
      userSession.RefreshToken = ent.RefreshToken; 
      var cacheItem = new CachedSessionItem(userSession, ent);
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
        var query = session.EntitySet<TUserSession>().Where(s => _sessionIds.Contains(s.Id))
          .Select(s => new { Id = s.Id, LastUsedOn = _lastUsed });
        session.ScheduleNonQuery<TUserSession>(query, Vita.Entities.Linq.LinqCommandType.Update);      
      }
    }//class

    //Temp list to accumulate session Ids to update LastUsedOn
    ConcurrentDictionary<Guid, int> _lastUsedToUpdate = new ConcurrentDictionary<Guid, int>();
    private void ScheduleUpdateLastUsedOn(Guid sessionId, DateTime lastUsed) {
      _lastUsedToUpdate[sessionId] = 1;
    }
    //Schedules to mass update all LastUsedOn
    void Timers_Elapsed1Second(object sender, EventArgs e) {
      var newDict = new ConcurrentDictionary<Guid, int>();
      var oldDict = System.Threading.Interlocked.Exchange(ref _lastUsedToUpdate, newDict);
      if(oldDict.Count == 0)
        return; 
      var utcNow = App.TimeService.UtcNow;
      var updateGuidList = oldDict.Keys.ToList();
      var saveObject = new LastUsedOnSaveObject(updateGuidList, utcNow);
      _saveService.AddObject(saveObject);
    }
    #endregion

  }
}
