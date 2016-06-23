using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.ComponentModel;
using System.Reflection;

using Vita.Common; 
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Linq;
using Vita.Data.Upgrades;

namespace Vita.Entities {
  /// <summary>Entity module represents a set of related entities and associated code comprising a unit of functionality.</summary>
  /// <remarks>Modules are primary units of component-based architecture for database applications. 
  /// Modules can be independently developed, tested and then used in multiple applications.</remarks>
  public partial class EntityModule {
    /// <summary>Entity app reference.</summary>
    public readonly EntityApp App;
    /// <summary>Gets module name.</summary>
    public readonly string Name;
    /// <summary>Gets optional module description.</summary>
    public readonly string Description;
    /// <summary>Gets the list of entity types registered.</summary>
    public readonly HashSet<Type> Entities = new HashSet<Type>();
    /// <summary>Gets the list of registered DB views.</summary>
    public readonly List<ViewDefinition> Views = new List<ViewDefinition>();
    /// <summary>Sequences in database.</summary>
    public readonly List<SequenceDefinition> Sequences = new List<SequenceDefinition>(); 
    /// <summary>Primary entity area (schema) that hosts all module's entities.</summary>
    public readonly EntityArea Area;
    /// <summary>Gets current module version.</summary>
    public readonly Version Version;

    private HashSet<Type> _dependencies = new HashSet<Type>();

    /// <summary>Constructs a new instance of the <c>EntityModule</c> class. </summary>
    /// <param name="area">Primary entity area to register module entities.</param>
    /// <param name="name">Module name.</param>
    /// <param name="description">Optional. Module description.</param>
    /// <param name="version">Module version.</param>
    public EntityModule(EntityArea area, string name, string description = null, Version version = null) {
      Area = area; 
      App = Area.App;
      Util.Check(App.Status == EntityAppStatus.Initializing,
        "Module may not be added to an entity app after it is initialized.");
      Name = name;
      Description = description;
      Version = version ?? new Version("1.0.0.0");
      App.AddModule(this);
    }

    /// <summary>Adds a dependency on an external module. </summary>
    /// <typeparam name="TModule">Dependency module type.</typeparam>
    /// <remarks><para>Use this method in module constructor to explicitly list other modules that this module depends on
    /// and which must be therefore included in the entity application. This is the case when entities in this 
    /// module reference entities in other modules. The system will check the dependencies at application startup and 
    /// if anything is missing it will throw an error with helpful message to the app developer that additional 
    /// modules should be included into the entity app.</para>
    /// <para>Example: Login module entities reference IEncryptedData entity
    /// for storing information in encrypted form, so LoginModule explicitly requires EncryptedDataModule.</para>
    /// </remarks>
    protected void Requires<TModule>() where TModule : EntityModule {
      _dependencies.Add(typeof(TModule));
    }

    /// <summary>Returns a list of module dependencies. </summary>
    /// <returns>An enumerable stream of module types that this module depends on.</returns>
    public IEnumerable<Type> GetDependencies() {
      return _dependencies;
    }

    /// <summary>Registers entity type(s) with the entity model in the area/schema. </summary>
    /// <param name="entities">An array of entity types.</param>
    public void RegisterEntities(params Type[] entities) {
      foreach(var entType in entities) {
        Entities.Add(entType);
      }//entType
    }

    /// <summary> Registers companion types. </summary>
    /// <param name="companionTypes">Companion types for registered entities.</param>
    /// <remarks>Companion types are used as an alternative place to put attributes for entities. 
    /// Any attribute you put on an entity (or its property) you can alternatively put on a companion type (or its property). 
    /// This facility might be useful for separation of concerns. For example, you can place all database
    /// index attribute on companion types that are located in a separate file that is maintaned by a 
    /// developer with appropriate database management skills. 
    /// </remarks>
    public void RegisterCompanionTypes(params Type[] companionTypes) {
      App.RegisterCompanionTypes(companionTypes);
    }

    /// <summary>Registers DB View based on a query. </summary>
    /// <typeparam name="TViewEntity">The entity that represents the output of the view.</typeparam>
    /// <param name="query">The query expression encoding the select logic of the view. Usually the return type is an anonymous
    /// object with properties matching the view entity type.</param>
    /// <param name="options">View options, optional.</param>
    /// <param name="viewName">View name, optional. If missing, the name of view entity type is assumed.</param>
    public void RegisterView<TViewEntity>(IQueryable query, DbViewOptions options = DbViewOptions.None, string viewName = null)
                                            where TViewEntity: class {
      var entQuery = query as EntityQuery;
      Util.Check(entQuery != null, "Base query for DB View must be an entity query.");
      Views.Add(new ViewDefinition(this, typeof(TViewEntity), entQuery, options, viewName));
    }
    /// <summary>Registers DB sequences. </summary>
    /// <param name="name">Sequence name.</param>
    /// <param name="type">Data type, optional. If null, 64-bit integer is assumed.</param>
    /// <param name="startValue">Optional, assumed zero if missing.</param>
    /// <param name="increment">Optional, assumed 1 if missing.</param>
    /// <param name="explicitSchema">Optional, explicit schema. If missing, sequence schema is assigned from module's area.</param>
    public void RegisterSequence(string name, Type type = null, int startValue = 0, int increment = 1, string explicitSchema = null) {
      type = type ?? typeof(long); 
      var sequence = new SequenceDefinition(this, name, type, startValue, increment, explicitSchema);
      Sequences.Add(sequence);
    }


    /// <summary>Called by the system notifying that the entity app is being initialized. 
    /// All services are registered by this moment, so module code can retrieve the services it uses
    /// and perform required initialization.</summary>
    public virtual void Init() {
    }

    /// <summary>Notifies the module that entity app initialization is completed. </summary>
    public virtual void AppInitComplete() {

    }

    /// <summary>
    /// Notifies that a new service is registered; useful for services that are added after app initialization
    /// (ex: WebCallNotificationService)
    /// </summary>
    /// <param name="serviceType">Service type.</param>
    /// <param name="service">Service instance</param>
    public virtual void NotifyServiceRegistered(Type serviceType, object service) {

    }

    /// <summary>Notifies the module that entity app is being shut down. </summary>
    public virtual void Shutdown() {

    }

    /// <summary>Registers the size for string and binary columns for the size code that should be applied for entities in the module. 
    /// </summary>
    /// <param name="sizeCode">The size code.</param>
    /// <param name="size">The size value.</param>
    /// <remarks>Most often size codes are registered at entity app level, and applied to all entities in all modules.
    /// However, if you need to set specific size values for codes used in some module, you can use this method to set these 
    /// values when you setup the application. The method uses module's namespace as a prefix in full size code that is registered 
    /// in the app-wide Sizes table. The Size attribute code looks up the size value first using the full size code (with namespace),
    /// then by size code alone. The implicit assumption is that entity interfaces are declared in the same namespace 
    /// as the containing module.</remarks>
    public virtual void RegisterSize(string sizeCode, int size) {
      var fullCode = this.GetType().Namespace + "#" + sizeCode;
      App.SizeTable[fullCode] = size;
    }

    public virtual void RegisterMigrations(DbMigrationSet migrations) {

    }

    public virtual void WebInitialize(Web.WebCallContext webContext) { }

    public override string ToString() {
      return this.Name;
    }


  }//class

} // ns
