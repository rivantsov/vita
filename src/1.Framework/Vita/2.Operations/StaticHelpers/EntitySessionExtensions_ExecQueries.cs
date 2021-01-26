using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vita.Data.Linq;
using Vita.Entities.Runtime;

namespace Vita.Entities {
  
  partial class EntitySessionExtensions {

    /// <summary>Executes a SQL insert statement based on LINQ query. </summary>
    /// <typeparam name="TEntity">Entity type for entity(ies) being inserted.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="baseQuery">Base LINQ query.</param>
    /// <returns>Number of entities inserted.</returns>
    /// <remarks>
    /// <para>The base query must return an anonymous type with properties matching the names of the entity being inserted. 
    /// The assigned expressions (right side) in anonymous object initializer are the values for columns in inserted rows.
    /// For foreign key columns (entity references) you should provide a property with a name matching the key column (ex: Book_Id), 
    /// and assign it the value of the column, ex: ' Book_id = bk.Id, '. Using entity reference directly is not supported (yet).
    /// </para><para>
    /// About generating new GUID-s for primary key values in new rows: use Guid.NewGuid() function as a value for 
    /// primary key column (Id); the system will translate it into server-specific function. SQLite does not have 
    /// a function for this, so INSERT statements for tables with GUID-type primary keys are not supported 
    /// for this database type. 
    /// </para>
    /// </remarks> 
    public static int ExecuteInsert<TEntity>(this IEntitySession session, IQueryable baseQuery) {
      Util.CheckParam(session, nameof(session));
      var entSession = (EntitySession)session;
      return entSession.ExecuteLinqNonQuery<TEntity>(baseQuery, LinqOperation.Insert);
    }

    /// <summary>Executes a SQL update statement based on LINQ query. </summary>
    /// <typeparam name="TEntity">Entity type for entity(ies) being updated.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="baseQuery">Base LINQ query.</param>
    /// <returns>Number of entities updated.</returns>
    /// <remarks>
    /// <para>The base query must return an anonymous type with properties matching the names of the entity being updated; 
    /// the assigned expressions (right side) in anonymous object initializer are the 'new' values for the left-side properties.
    /// For foreign key columns (entity references) you should provide a property with a name matching the key column (ex: Book_Id), 
    /// and assign it the value of the column, ex: ' Book_id = bk.Id, '. Using entity reference directly is not supported (yet).
    /// </para><para>
    /// For simple updates (not using values from other tables/entities) the output type does not need to include primary key property.
    /// The WHERE clause of LINQ expression will be translated into WHERE clause of the update SQL command. </para><para>
    /// For complex updates using values from other entities the anonymous type must include the property matching the primary key 
    /// of the entity. The primary key value will not be updated, but will be used in the WHERE clause of the UPDATE statement 
    /// for filtering rows for update.
    /// </para>
    /// </remarks> 
    public static int ExecuteUpdate<TEntity>(this IEntitySession session, IQueryable baseQuery) {
      Util.CheckParam(session, nameof(session));
      var entSession = (EntitySession)session;
      return entSession.ExecuteLinqNonQuery<TEntity>(baseQuery, LinqOperation.Update);
    }

    /// <summary>Executes a SQL delete statement based on LINQ query. </summary>
    /// <typeparam name="TEntity">Entity type for entity(ies) to delete.</typeparam>
    /// <param name="session">Entity session.</param>
    /// <param name="baseQuery">Base LINQ query.</param>
    /// <returns>Number of entities deleted.</returns>
    /// <remarks>
    /// <para>The base query must return an anonymous type with a single property matching the primary key.
    /// </para>
    /// <para>For simple deletes (not using values from other tables/entities) the output key value is ignored and DELETE statement
    /// simply includes the WHERE clause from the base query.
    /// For complex deletes thtat use values from other entities/tables the primary key values identify the records to be deleted.
    /// </para>
    /// </remarks> 
    public static int ExecuteDelete<TEntity>(this IEntitySession session, IQueryable baseQuery) {
      Util.CheckParam(session, nameof(session));
      var entSession = (EntitySession)session;
      return entSession.ExecuteLinqNonQuery<TEntity>(baseQuery, LinqOperation.Delete);
    }

    public static void ScheduleInsert<TEntity>(this IEntitySession session, IQueryable query,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      var entSession = (EntitySession)session;
      entSession.ScheduleLinqNonQuery<TEntity>(query, LinqOperation.Insert, schedule);
    }

    public static void ScheduleUpdate<TEntity>(this IEntitySession session, IQueryable query,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      var entSession = (EntitySession)session;
      entSession.ScheduleLinqNonQuery<TEntity>(query, LinqOperation.Update, schedule);
    }

    public static void ScheduleDelete<TEntity>(this IEntitySession session, IQueryable query,
                             CommandSchedule schedule = CommandSchedule.TransactionEnd) {
      var entSession = (EntitySession)session;
      entSession.ScheduleLinqNonQuery<TEntity>(query, LinqOperation.Delete, schedule);
    }

  }
}
