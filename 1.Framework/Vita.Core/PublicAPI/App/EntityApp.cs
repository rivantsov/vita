using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Authorization;
using Vita.Entities.Caching;
using Vita.Entities.Logging;
using Vita.Entities.Model;
using Vita.Entities.Model.Construction;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Services.Implementations;
using Vita.Data;
using Vita.Entities.Web;
using Vita.Entities.Web.Implementation;

namespace Vita.Entities {

  /// <summary> Represents entity application status, from initialization to shutdown. </summary>
  public enum EntityAppStatus {
    Created,
    /// <summary> Application is initializing. </summary>
    Initializing,
    Initialized,
    Connected,
    Shutdown,
  }


  /// <summary>Entity application. </summary>
  public partial class EntityApp : IServiceProvider {
    /// <summary>The app name. Defaults to type name. </summary>
    public string AppName;

    /// <summary>Entity application status, from initialization to shutdown. </summary>
    public EntityAppStatus Status { get; protected set; }

    ///<summary>Entity application version, formatted as '1.0.0.0' . </summary>
    public Version Version;

    /// <summary>Gets a collection of registered areas. EntityArea is a representation of database schema object like 'dbo'. </summary>
    public IEnumerable<EntityArea> Areas {
      get { return _areas; }
    } IList<EntityArea> _areas = new List<EntityArea>();

    /// <summary>Gets a list of entity modules in the application. </summary>
    public IEnumerable<EntityModule> Modules {
      get { return _modules; }
    } IList<EntityModule> _modules = new List<EntityModule>();

    /// <summary>Application-level events.</summary>
    public readonly EntityAppEvents AppEvents;

    /// <summary>Entity-level events.</summary>
    public readonly EntityEvents EntityEvents;

    /// <summary> Gets the settings for entity cache (data cache). </summary>
    public readonly CacheSettings CacheSettings = new CacheSettings();

    /// <summary>Gets the activation log for the application. </summary>
    public MemoryLog ActivationLog; 

    /// <summary>A dictionary of custom attribute handlers.</summary>
    public readonly IDictionary<Type, CustomAttributeHandler> AttributeHandlers;

    public readonly IList<IEntityModelExtender> ModelExtenders = new List<IEntityModelExtender>();

    /// <summary>Gets a dictionary of string/binary size values indexed by size code (string). </summary>
    /// <remarks>
    /// The purpose of this table is to make it easier to manage sizes of string columns and change it globally for certain column group. 
    /// You can specify column size directly using Size attribute and provide literal value as parameter. 
    /// Alternatively, you can specify the size code (string); in this case the system will lookup the value in this dictionary.
    /// Later, if you need to change 'Description' columns throughout the app, you can do it by changing the value in the <c>SizeTable</c> dictionary.
    /// </remarks>
    public readonly Sizes.SizeTable SizeTable;

    /// <summary>Default length for string properties without Size attribute. </summary>
    public int DefaultStringLength = 50;

    /// <summary>Gets the entity model instance . Entity model is an internal object containing detailed meta-information about entities. </summary>
    /// <remarks> This property is initially null - do not use it the <c>EntityApp</c> constructor. 
    /// The model is built at application initialization. </remarks>
    public EntityModel Model { get; internal set; } 

    /// <summary>Get a path for activation log file. </summary>
    public string ActivationLogPath;

    public ApiConfiguration ApiConfiguration = new ApiConfiguration();

    /// <summary>Gets or sets a full path of the log file.</summary>
    public string LogPath {
      get {
        return _logFilePath; 
      }
      set {
        _logFilePath = value;
        if(this.Status== EntityAppStatus.Created)
          return; //nothing to do
        if(_logFileWriter == null)
          _logFileWriter = new LogFileWriter(this, _logFilePath);
        else
          _logFileWriter.LogPath = _logFilePath;
      }
    }
    string _logFilePath; 
    LogFileWriter _logFileWriter;

    /// <summary>Gets the instance of the application time service. </summary>
    public ITimeService TimeService { get; protected set; }
    public virtual IErrorLogService ErrorLog { get { return GetService<IErrorLogService>(); } }

    /// <summary>Gets the instance of the application authorization service. </summary>
    public IAuthorizationService AuthorizationService { get; protected set; }
    /// <summary>Holds the instance of the data access service. </summary>
    public IDataAccessService DataAccess { get; protected set; } 

    internal readonly List<EntityReplacementInfo> Replacements = new List<EntityReplacementInfo>();
    internal readonly HashSet<Type> CompanionTypes = new HashSet<Type>();
    internal readonly Dictionary<Type, EntityArea> MovedEntities = new Dictionary<Type, EntityArea>(); 

    //Services
    private IDictionary<Type, object> _services = new Dictionary<Type, object>();

    public readonly IList<EntityApp> LinkedApps = new List<EntityApp>();

    /// <summary> Constructs a new EntityApp instance. </summary>
    public EntityApp(string appName = null, string version = "1.0.0.0") {
      AppName = appName ?? this.GetType().Name;
      Version = new Version(version); 
      Status = EntityAppStatus.Created;
      var ctx = this.CreateSystemContext(); 
      ActivationLog = new MemoryLog(ctx); 
      AppEvents = new EntityAppEvents(this);
      EntityEvents = new EntityEvents();
      AttributeHandlers = CustomAttributeHandler.GetDefaultHandlers();
      SizeTable = Sizes.GetDefaultSizes();
    }

    /// <summary>Initializes the entity app. </summary>
    /// <remarks>Call this method after you finished composing entity application of modules.
    /// The method is called automatically when you connect the application to the database
    /// with <c>ConnectTo()</c> extension method.</remarks>
    public virtual void Init() {
      if(Status != EntityAppStatus.Created)
        return; 
      Status = EntityAppStatus.Initializing;
      this.AppEvents.OnInitializing(EntityAppInitStep.Initializing);
      //Check dependencies
      foreach(var mod in this.Modules) {
        var depList = mod.GetDependencies();
        foreach(var dep in depList) {
          var ok = Modules.Any(m => dep.IsTypeOrSubType(m));
          if(!ok)
            this.ActivationLog.Error("Module {0} requires dependent module {1} which is not included in the app.", mod.GetType(), dep);
        }
      }
      this.CheckActivationErrors();
      // create default services
      CreateDefaultServices();
      //create log file writer
      if (!string.IsNullOrWhiteSpace(_logFilePath))
        _logFileWriter = new LogFileWriter(this, _logFilePath);
       
      //Build model
      var modelBuilder = new EntityModelBuilder(this);
      modelBuilder.BuildModel();
      if(ActivationLog.HasErrors()) {
        if(!string.IsNullOrEmpty(this.ActivationLogPath))
          ActivationLog.DumpTo(this.ActivationLogPath);
        var errors = ActivationLog.GetAllAsText();
        throw new StartupFailureException("Entity model build failed.", errors);
      }
      //Notify modules that entity app is constructed
      foreach(var module in this.Modules)
        module.Init();
      //init services
      var servList = this.GetAllServices();
      for(int i = 0; i < servList.Count; i++) {
        var service = servList[i];
        var iServiceInit = service as IEntityService;
        if(iServiceInit != null)
          iServiceInit.Init(this);
      }
      //complete initialization
      this.AppEvents.OnInitializing(EntityAppInitStep.Initialized);
      foreach(var module in this.Modules)
        module.AppInitComplete();

      CheckActivationErrors();
      // Init linked apps 
      foreach(var linkedApp in LinkedApps)
        linkedApp.Init();

      Status = EntityAppStatus.Initialized;
    }

    protected virtual void CreateDefaultServices() {
      this.AuthorizationService = new AuthorizationService(this);
      RegisterService<IAuthorizationService>(this.AuthorizationService);
      this.DataAccess = new DataAccessService(this);
      RegisterService<IDataAccessService>(this.DataAccess);
      // create services only if they do not exist yet, or not imported from LinkedApps
      RegisterServiceIfNotFound<IErrorLogService>(() => new TraceErrorLogService());
      this.TimeService = new TimeService();
      this.TimeService = this.RegisterServiceIfNotFound<ITimeService>(() => new TimeService());
      var timers = this.RegisterServiceIfNotFound<ITimerService>(() => new TimerService());
      this.RegisterService<ITimerServiceControl>(timers as ITimerServiceControl);
      this.RegisterServiceIfNotFound<IBackgroundSaveService>(() => new BackgroundSaveService());
      // likely is replaced by another implementation writing to database - during modules initialization
      this.RegisterServiceIfNotFound<IOperationLogService>(() => new DefaultOperationLogService(this, LogLevel.Details));
    }

    /// <summary>Moves entities (types) from their original areas to the target Area. </summary>
    /// <param name="toArea">Target area.</param>
    /// <param name="entityTypes">Entity types.</param>
    public void MoveTo(EntityArea toArea, params Type[] entityTypes) {
      foreach(var ent in entityTypes)
        MovedEntities[ent] = toArea; 
    }

    /// <summary>Imports services from external service provider. </summary>
    /// <param name="provider">External service provider, usually another entity applications.</param>
    /// <param name="serviceTypes">Types of services to import.</param>
    public void ImportServices(IServiceProvider provider, params Type[] serviceTypes) {
      foreach(var type in serviceTypes) {
        var serv = provider.GetService(type);
        if(serv != null)
          this._services[type] = provider.GetService(type);
      }
    }

    /// <summary>Adds an area (logical equivalent of database schema like 'dbo') to the data model.</summary>
    /// <param name="areaName">Area name. It is default for schema name, unless it is mapped to different schema.</param>
    /// <returns>A new area instance.</returns>
    /// <remarks>Before you can create modules and register entities for your entity app, 
    /// you must create at least one area. </remarks>
    public EntityArea AddArea(string areaName) {
      var area = new EntityArea(this, areaName);
      _areas.Add(area);
      return area; 
    }

    [Obsolete("Use AddArea(name) method.")]
    public EntityArea AddArea(string name, string schemaName, string description = null) {
      return this.AddArea(schemaName); 
    }
    /// <summary>Adds an entity module to the application. </summary>
    /// <param name="module">A module to add.</param>
    internal void AddModule(EntityModule module) {
      if (!_modules.Contains(module)) //prevent duplicates
        _modules.Add(module); 
    }

    public TModule GetModule<TModule>() where TModule : EntityModule {
      var result = Modules.FirstOrDefault(m => m is TModule) as TModule;
      if(result != null)
        return result;
      foreach(var linkedApp in LinkedApps) {
        result = linkedApp.GetModule<TModule>();
        if(result != null)
          return result; 
      }
      return null; 
    }

    /// <summary>Creates a data source (database) using provided DB setttings and registers it with data access service.</summary>
    /// <param name="dbSettings">Database settings.</param>
    public void ConnectTo(DbSettings dbSettings) {
      if(this.Status == EntityAppStatus.Initializing)
        this.Init();
      var db = new Database(this, dbSettings); //this will construct DbModel for Database
      var ds = new DataSource(dbSettings.DataSourceName, db, this.CacheSettings);
      this.DataAccess.RegisterDataSource(ds);
      this.Status = EntityAppStatus.Connected; 
    }

    /// <summary> Replaces one registered entity with extended version. </summary>
    /// <param name="replacedType">The entity type to be replaced.</param>
    /// <param name="replacementEntityType">The new replacing entity type.</param>
    /// <remarks><para>
    /// This method provides a way to extend entities defined in independently built modules. 
    /// The other use is to integrate the independently developed modules, so that the tables in database 
    /// coming from different modules can actually reference each other through foreign keys.
    /// If the replacement type is not registered with any module, it is placed in the module of the type being replace.  
    /// </para>
    /// </remarks>
    public void ReplaceEntity(Type replacedType, Type replacementEntityType) {
      Util.Check(replacedType.IsInterface, "Invalid type: {0}; expected Entity interface.", replacedType);
      Util.Check(replacementEntityType.IsInterface, "Invalid type: {0}; expected Entity interface.", replacementEntityType);
      //Unless the type being replaced is an empty stub, then check that new type is compatible with the old type
      var oldTypeProps = replacedType.GetAllProperties();
      if (oldTypeProps.Count > 0) { //it is not an empty stub
        Util.Check(replacedType.IsAssignableFrom(replacementEntityType), 
          "The replacing type ({0}) must be a subtype of the type being replaced ({1}). ", replacementEntityType, replacedType);
      }
      var replInfo = new EntityReplacementInfo() { ReplacedType = replacedType, NewType = replacementEntityType};
      Replacements.Add(replInfo); 
    }

    /// <summary> Registers companion types. </summary>
    /// <param name="companionTypes">Companion types for registered entities.</param>
    /// <remarks>Companion type is used as an alternative place to put attributes for an entity. 
    /// This facility might be useful for separation of concerns. For example, you can place all database
    /// index attribute on companion types that are located in a separate file that is maintaned by a 
    /// developer with database expertise. 
    /// </remarks>
    public void RegisterCompanionTypes(params Type[] companionTypes) {
      CompanionTypes.UnionWith(companionTypes);
    }

    /// <summary>Registers a service with an application. </summary>
    /// <typeparam name="T">Service type used as a key in internal servcies dictionary. Usually it is an interface type.</typeparam>
    /// <param name="service">Service implementation.</param>
    /// <remarks>The most common use for the services is entity module registering itself as a service for the application.
    /// For example, ErrorLogModule registers IErrorLogService that application code and other modules can use to log errors.
    /// </remarks>
    public void RegisterService<T>(T service) {
      _services[typeof(T)] = service;
      this.AppEvents.OnServiceAdded(this, typeof(T), service);
      //notify child apps
      foreach (var linkedApp in this.LinkedApps)
        linkedApp.AppEvents.OnServiceAdded(this, typeof(T), service);
    }

    protected T RegisterServiceIfNotFound<T>(Func<T> creator) where T: class {
      var old = GetService<T>();
      if(old != null)
        return old;
      var service = creator(); 
      RegisterService(service);
      return service; 
    }

    public void RemoveService(Type serviceType) {
      if(_services.ContainsKey(serviceType))
        _services.Remove(serviceType);
    }
    /// <summary> Gets a service by service type. </summary>
    /// <typeparam name="TService">Service type, usually an interface type.</typeparam>
    /// <returns>Service implementation.</returns>
    public TService GetService<TService>()  where TService : class {
      return (TService)this.GetService(typeof(TService));
    }

    #region IServiceProvider members
    /// <summary> Gets a service by service type. </summary>
    /// <param name="serviceType">Service type.</param>
    /// <returns></returns>
    public object GetService(Type serviceType) {
      object result;
      if (_services.TryGetValue(serviceType, out result))
        return result;
      foreach(var linkedApp in LinkedApps) {
        result = linkedApp.GetService(serviceType);
        if(result != null)
          return result; 
      }
      return null;  
    }
    #endregion

    /// <summary>Returns the list of all service types (keys) registered in the application. </summary>
    /// <returns>List of service interface types.</returns>
    public IList<Type> GetAllServiceTypes() {
      return _services.Keys.ToList(); 
    }

    /// <summary>Returns a list of all entity types from all entity modules. </summary>
    /// <returns></returns>
    public IEnumerable<Type> GetAllEntityTypes() {
      return this.Modules.SelectMany(m => m.Entities); 
    }

    /// <summary>Returns entity app name. </summary>
    /// <returns></returns>
    public override string ToString() {
      return AppName;
    }

    /// <summary>Fires an event requesting all logging facilities to flush buffers. </summary>
    public void Flush() {
      AppEvents.OnFlushRequested();
      foreach(var linkedApp in LinkedApps)
        linkedApp.Flush(); 
    }

    /// <summary>Performs the shutdown of the application. Notifies all components and modules about pending application shutdown. 
    /// </summary>
    public virtual void Shutdown() {
      Flush();
      Status = EntityAppStatus.Shutdown;
      foreach(var module in this.Modules)
        module.Shutdown();
      //shutdown services
      var servList = this.GetAllServices();
      for(int i = 0; i < servList.Count; i++) {
        var service = servList[i];
        var iEntService = service as IEntityService;
        if(iEntService != null)
          iEntService.Shutdown();
      }
    }

    /// <summary>
    /// Checks activation log messages and throws exception if there were any errors during application initialization.
    /// </summary>
    public void CheckActivationErrors() {
      if(ActivationLog.HasErrors()) {
        if (!string.IsNullOrEmpty(ActivationLogPath))
          ActivationLog.DumpTo(ActivationLogPath);
        var allErrors = ActivationLog.GetAllAsText();
        throw new StartupFailureException("Application activation failed.", allErrors);
      }
    }

    private IList<object> GetAllServices() {
      return _services.Values.ToList();
    }

    #region Config repo access

    //Repository of all config/settings objects, indexed by type, for easy access from anywhere
    Dictionary<Type, object> _configsRepo = new Dictionary<Type, object>();
    private object _lock = new object();

    /// <summary>Registers config/settings object in global repo.</summary>
    /// <typeparam name="T">Type of config object.</typeparam>
    /// <param name="config">Config object.</param>   
    public void RegisterConfig<T>(T config) where T : class {
      lock(_lock) {
        _configsRepo[typeof(T)] = config;
      }
    }
    /// <summary>Retrieves config object based on type. </summary>
    /// <typeparam name="T">Config object type.</typeparam>
    /// <returns>Config object.</returns>
    public T GetConfig<T>(bool throwIfNotFound = true) where T: class {
      lock(_lock) {
        object config;
        if(_configsRepo.TryGetValue(typeof(T), out config))
          return (T)config;
      }
      if (throwIfNotFound)
        Util.Throw("Config/settings object of type {0} is not registered in ConfigRepo. " + 
                   "Possibly owner module is not included into the app.", typeof(T));
      return null; //never happens
    }

    #endregion 

    #region Methods to override (optionally)
    /// <summary>Returns a list of authorization roles for a given user. </summary>
    /// <param name="user">UserInfo object.</param>
    /// <returns>List of authorization roles.</returns>
    /// <remarks>Override this method if you use Authorization and secure sessions.</remarks>
    public virtual IList<Role> GetUserRoles(UserInfo user) {
      return new List<Role>(); 
    }

    public virtual string GetUserDispalyName(UserInfo user) {
      return user.UserName;
    }

    public virtual void UserLoggedIn(OperationContext context) {
       
    }

    public virtual void UserLoggedOut(OperationContext context) {
      this.AuthorizationService.UserLoggedOut(context.User);
    }
    #endregion

    public bool IsConnected() {
      return Status == EntityAppStatus.Connected || Status == EntityAppStatus.Shutdown; 
    }

    private bool _webInitialized;
    public virtual void WebInitilialize(WebCallContext webContext) {
      if(_webInitialized) return; 
      lock(this) {
        if(_webInitialized) return; 
        try {
          foreach(var m in Modules)
            m.WebInitialize(webContext);
          foreach(var linkedApp in LinkedApps)
            linkedApp.WebInitilialize(webContext); 
        } finally { _webInitialized = true; }
      }//lock

    }
  }//class

}//ns
