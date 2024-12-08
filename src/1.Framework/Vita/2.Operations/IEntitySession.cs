using System;
using System.Linq;
using System.Threading.Tasks;


namespace Vita.Entities {

  /// <summary>Represents a session - a virtual connection channel to the database used for reading/writing entities.</summary>
  /// <remarks>Keeps track of loaded and modified entitites.</remarks>
  public interface IEntitySession {
    
    /// <summary>Returns the context associated with the current user. </summary>
    OperationContext Context { get; }

    /// <summary>Creates a new entity.</summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>An entity instance.</returns>
    /// <remarks>The entity is created in memory only. It will be submitted to the database when application calls the <c>SaveChanges</c>  method.</remarks>
    TEntity NewEntity<TEntity>() where TEntity: class;

    /// <summary>Retrieves entity by type and primary key value. </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="primaryKeyValue">The value of the primary key.</param>
    /// <param name="flags">Load flags.</param>
    /// <returns>An entity instance.</returns>
    /// <remarks>For composite primary keys pass an instance of primary key
    /// created using the <c>EntityExtensions.CreatePrimaryKey</c> extension method. 
    /// </remarks>
    TEntity GetEntity<TEntity>(object primaryKeyValue, LoadFlags flags = LoadFlags.Default) where TEntity: class;

    /// <summary>Marks entity for deletion.</summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="entity">The entity to delete.</param>
    /// <remarks>The deletion of database record will happen when the application calls the <c>SaveChanges</c> method.</remarks>
    void DeleteEntity<TEntity>(TEntity entity) where TEntity: class;

    /// <summary>Checks if entity instance can be deleted without violating referential constraints in the database.</summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="entity">The entity to check.</param>
    /// <param name="blockingEntities">The list of entity types for which there are instances (records) in the database that reference 
    /// the entity being checked, thus preventing the entity from being deleted. Set to null if the function returns true.</param>
    /// <returns>True if there are not external references to the entity, so it can be deleted. Otherwise, false.</returns>
    /// <remarks>The function ignores the referencing entities if the reference (property) is marked with [CascadeDelete] attribute, meaning 
    /// that these referencing records would be automatically deleted whenever the parent is deleted.
    /// </remarks>
    bool CanDeleteEntity<TEntity>(TEntity entity, out Type[] blockingEntities) where TEntity : class;

    /// <summary>
    /// Validates the changes in all entities tracked by the session that are subject for the next update (<c>SaveChanges</c> call). 
    /// </summary>
    /// <remarks>Throws a <c>ValidationException</c> if one or more validation errors are encountered.</remarks>
    void ValidateChanges();

    /// <summary>Reverts all changes in entities tracked by the current session.</summary>
    void CancelChanges(); 

    /// <summary> Save the changes for all tracked entities in the database. </summary>
    /// <remarks> Validates changes using then <c>ValidateChanges</c> method before starting the database operation. Automatically orders 
    /// update commands to satisfy the referential integrity rules.
    /// </remarks>
    void SaveChanges(); 

    /// <summary>Returns a queryable set for use in LINQ queries. </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>A queryable set.</returns>
    /// <remarks>Use this command to create primary queryable sets for use in custom LINQ queries.</remarks>    
    IQueryable<TEntity> EntitySet<TEntity>() where TEntity : class;

    // New async stuff
    Task<TEntity> GetEntityAsync<TEntity>(object primaryKeyValue, LoadFlags flags = LoadFlags.Default) where TEntity : class;
  }

  /// <summary> Specifies entity load options for <c>IEntitySession.GetEntity</c> method. </summary>
  [Flags]
  public enum LoadFlags {
    /// <summary> Return already loaded entity; return null if record is not loaded.</summary>
    None = 0x0,
    /// <summary>Load entity from the data store if it is not loaded; does not reload already loaded entity.</summary>
    Load = 0x01,
    /// <summary>Create a stub if entity is not loaded. A stub is a skeleton entity with only primary key value(s) assigned. 
    /// Other entity properties will be loaded lazily, when application tries to read them.
    /// </summary>
    Stub = 0x02,
    /// <summary> Default value: Load. </summary>
    Default = Load,
  }

}
