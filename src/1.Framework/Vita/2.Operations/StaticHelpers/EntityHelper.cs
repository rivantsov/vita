﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Services;
using Vita.Entities.Utilities;

namespace Vita.Entities {

  /// <summary>A static utility class with helper methods for manipulating entities. </summary>
  public static class EntityHelper {

    /// <summary>Returns an entity session that entity is linked to.</summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>The entity session.</returns>
    public static IEntitySession GetSession(object entity) {
      Util.Check(entity != null, "GetSession: parameter 'entity' may not be null.");
      var rec = GetRecord(entity);
      return rec.Session;
    }

    //.NET framework's GetHashCode() is not guaranteed to be stable between .NET versions. 
    // If we want to keep hashes in database, we need a stable hash implementation 
    public static int ComputeStableHash(this EntityApp app, string value) {
      if(string.IsNullOrWhiteSpace(value))
        return 0;
      var hasher = app.GetService<IHashingService>(); 
      return hasher.ComputeHash(value);
    }

    /// <summary>Returns an underlying <c>EntityRecord</c> object for an entity. </summary>
    /// <param name="entity">An entity instance.</param>
    /// <returns>The entity record.</returns>
    public static EntityRecord GetRecord(object entity) {
      //Util.CheckParam(entity, nameof(entity));
      if (entity == null || entity == DBNull.Value)
        return null;
      if (entity is EntityRecord)
        return (EntityRecord) entity;
      var recHolder = entity as IEntityRecordContainer;
      if (recHolder == null)
        Util.Throw("Object {0} is not an Entity, cannot retrieve Record field.", entity);
      return recHolder.Record;
    }

    /// <summary>Returns the entity type (interface type) for an entity instance. </summary>
    /// <param name="entity">An entity instance.</param>
    /// <returns>The interface type for an entity.</returns>
    /// <remarks>Note that entity.GetType() returns an underlying class type (IL-generated at runtime), not entity interface type.</remarks>
    public static Type GetEntityType(object entity) {
      var rec = GetRecord(entity);
      return rec.EntityInfo.EntityType;
    }

    public static int GetPropertySize<TEntity>(this EntityApp app, Expression<Func<TEntity, object>> selector) {
      var propName = ExpressionHelper.GetSelectedProperty(selector);
      var ent = app.Model.GetEntityInfo(typeof(TEntity), throwIfNotFound: true);
      var member = ent.GetMember(propName, throwIfNotFound: true);
      return member.Size; 
    }

    /// <summary>Returns a value of the entity property.</summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The value of the property.</returns>
    public static object GetProperty(object entity, string propertyName) {
      var rec = EntityHelper.GetRecord(entity);
      var member = rec.EntityInfo.GetMember(propertyName, throwIfNotFound: true);
      var value = rec.GetValue(member);
      return value;
    }

    /// <summary>Returns a strongly-typed value of the entity property.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The value of the property.</returns>
    public static T GetProperty<T>(object entity, string propertyName) {
      return (T) GetProperty(entity, propertyName);
    }

    /// <summary>Sets a property value of an entity. </summary>
    /// <param name="entity">The entity instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    public static void SetProperty(object entity, string propertyName, object value) {
      var rec = EntityHelper.GetRecord(entity);
      var member = rec.EntityInfo.GetMember(propertyName, throwIfNotFound: true); 
      rec.SetValue(member, value);
    }

    /// <summary>Returns custom tag (free-form object) associated with the entity. </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>Tag object.</returns>
    /// <remarks>Custom tags are free-form objects that can be attached to entities. Application 
    /// is free to use the tags any way it needs. They are not used by the framework, and are 
    /// reserved to use by client application code.</remarks>
    public static object GetCustomTag(object entity) {
      var rec = EntityHelper.GetRecord(entity);
      return rec.CustomTag; 
    }

    /// <summary>Associates a custom tag (free-form object) with an entity. </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tag">A tag to attach.</param>
    /// <remarks>Custom tags are free-form objects that can be attached to entities. Application 
    /// is free to use the tags any way it needs. They are not used by the framework, and are 
    /// reserved to use by client application code.</remarks>
    public static void SetCustomTag(object entity, object tag) {
      var rec = EntityHelper.GetRecord(entity);
      rec.CustomTag = tag; 
    }

    /// <summary>Checks if specified properties of the entity are loaded from the database. Reloads the entity if necessary.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity instance.</param>
    public static void EnsureLoaded<TEntity>(TEntity entity) {
      var rec = GetRecord(entity);
      if (rec.Status == EntityStatus.Stub)
        rec.Reload();
    }

    #region nested classes
    /// <summary>Represents a matching pair of objects in list comparisons</summary>
    /// <typeparam name="TOld">Object type in old list.</typeparam>
    /// <typeparam name="TNew">Object type in new list.</typeparam>
    /// <typeparam name="TId">ID type (typically primary key type).</typeparam>
    public class Match<TOld, TNew, TId> {
      public TId Id;
      public TOld Old;
      public TNew New; 
    }

    /// <summary>Represents the result of lists comparison.</summary>
    /// <typeparam name="TOld">Object type in old list.</typeparam>
    /// <typeparam name="TNew">Object type in new list.</typeparam>
    /// <typeparam name="TId">ID type (typically primary key type).</typeparam>
    public class ListCompareResult<TOld, TNew, TId> {
      public List<TOld> ToDelete = new List<TOld>(); 
      public List<Match<TOld, TNew, TId>> ToUpdate = new List<Match<TOld, TNew, TId>>(); 
      public List<TNew> ToAdd = new List<TNew>(); 
    }
    #endregion

    /// <summary>
    /// Compares two lists (old and new) based on matching IDs and returns a result object with lists of objects to delete, update and insert. 
    /// </summary>
    /// <typeparam name="TOld">Type of objects in the OLD list, typically an entity type.</typeparam>
    /// <typeparam name="TNew">Type of objects in the NEW list, typically a model (DTO) type.</typeparam>
    /// <typeparam name="TId">Type of ID (primary key) used for objects matching.</typeparam>
    /// <param name="oldList">The list of old objects (entities in the database).</param>
    /// <param name="oldIdReader">Function delegate reading the ID value from an object in the old list.</param>
    /// <param name="newList">The list of new objects received from the client.</param>
    /// <param name="newIdReader">Function delegate reading the ID value from an object in the new list.</param>
    /// <returns>Returns a container with lists of objects to delete, add or update.</returns>
    /// <remarks>
    /// This method is useful when your code on the server receives an updated list from the client (UI) tier,
    /// and you need to figure out which objects to delete, insert or update in the database. 
    /// In this context the term &quot;Old&quot; refers to the list of entities in the database and
    /// &quot;Old&quot; - to the list of models (DTOs) received from the UI. 
    /// </remarks>
    public static ListCompareResult<TOld, TNew, TId> CompareLists<TOld, TNew, TId>(
                                 IList<TOld> oldList, 
                                 Func<TOld, TId> oldIdReader, 
                                 IList<TNew> newList, 
                                 Func<TNew, TId> newIdReader) {
      var result = new ListCompareResult<TOld, TNew, TId>();
      result.ToAdd.AddRange(newList); //initially add all as new
      foreach(var old in oldList) {
        var oldId = oldIdReader(old); 
        //use result.ToAdd instead of newList cause it's a bit more efficient - the ToAdd list shrinks as we go
        var newObj = result.ToAdd.FirstOrDefault(n=> oldId.Equals(newIdReader(n))); 
        if (newObj == null)
          result.ToDelete.Add(old); 
        else {
          result.ToUpdate.Add(new Match<TOld, TNew, TId>() {Id = oldId, Old = old, New = newObj});
          result.ToAdd.Remove(newObj); 
        }
      }
      return result; 
    }

    /// <summary>Reorders objects in the list according to the order of IDs (primary keys) in the ID list.</summary>
    /// <typeparam name="T">Type of objects in the list.</typeparam>
    /// <typeparam name="TId">Type of object ID (primary key) used in comparison.</typeparam>
    /// <param name="list">The list of objects to reorder.</param>
    /// <param name="idReader">The function delegate that retrieves an ID value from the object.</param>
    /// <param name="newIdList">The list of IDs representing the desired order of objects.</param>
    /// <returns>True if the list was reordered, otherwise false.</returns>
    /// <remarks><para>This method is useful when you need to re-arrange entities in the list in the database according to 
    /// the explicit ordering user created in UI (typically with drop-drap of list items or with move up/down buttons).
    /// For explicit ordering of lists in the database VITA provides an attribute [PersistOrderIn] which shoud be placed
    /// on the list property - in this case VITA detects when list has changed (entities are re-ordered); when changes are saved, 
    /// VITA runs through the list assigning an incrementing value to the Number property/column (or similar) in the child entity
    /// to save the explicit order in the database.</para>
    /// <para>In a typical scenario the user changes the order of items in some UI and submits the changes. The UI code might send a new order 
    /// in a form of ID list. On the server, you can load the list from the database and call the <c>ReorderList</c> method - it 
    /// will rearrange the entities in the list according the ID sequence. When you call <c>session.SaveChanges</c>, VITA will 
    /// re-assign the numbering property of all entities in the list before actually submitting changes to the database.</para>
    /// </remarks>
    public static bool ReorderList<T, TId>(IList<T> list, Func<T, TId> idReader, IList<TId> newIdList) {
      Util.Check(list.Count == newIdList.Count, "List count ({0}) should be equal or less than ID list count ({1}).", list.Count, newIdList.Count);
      bool reordered = false;
      for(int i = 0; i < list.Count; i++) {
        var elem = list[i];
        var expectedId = newIdList[i];
        if (!idReader(elem).Equals(expectedId)) {
          var targetIndex = FindElement(list, expectedId, idReader, i + 1);
          Util.Check(targetIndex >= 0, "Element for Id {0} not found.", expectedId);
          //The found index should be after 'i'
          Util.Check(targetIndex > i, "ReorderLists: Invalid ID list, duplicate ID found: {0}", expectedId);
          //Move element to current position
          var targetElem = list[targetIndex];
          list.RemoveAt(targetIndex); 
          list.Insert(i, targetElem);
          reordered = true; 
        }
      }
      return reordered; 
    }
    private static int FindElement<T, TId>(IList<T> list, TId findId, Func<T, TId> idReader, int startIndex) {
      for(int i = startIndex; i < list.Count; i++) {
        var id = idReader(list[i]);
        if(id.Equals(findId))
          return i;
      }
      return -1;
    }


    public static void RefreshEntity(object entity) {
      var rec = EntityHelper.GetRecord(entity);
      Util.Check(rec != null, "Object is not an entity ({0}) - failed to retrieve entity record.", entity);
      rec.Reload();
    }

  }//class
}//ns
