using System; 

namespace Vita.Entities.Services {

  public interface IServiceContainer : IServiceProvider {
    void RegisterService<T>(T service) where T: class;
  }


  /// <summary>An optional interface to initialize/shutdown a non-module service added to Services collection of EntityApp. 
  /// Module-based services - when an EntityModule implements a service - are initialized through overridable Init method. 
  /// </summary>
  public interface IEntityServiceBase {
    /// <summary>Notifies that entity app is inititalizing. </summary>
    /// <param name="app">Entity app.</param>
    /// <remarks>This method is invoked very early in application initialization. All services must be registered at this point. 
    /// The service can use this method to link to other services. 
    /// </remarks>
    void Init(EntityApp app);

    void Shutdown();
  }

}
