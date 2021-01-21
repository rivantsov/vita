using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Vita.Data;
using Vita.Data.Linq;
using Vita.Entities.Model;
using Vita.Entities.Utilities;

namespace Vita.Entities.Runtime {

  internal class IncludeProcessor {
    public static int MaxNestedRunsPerEntityType = 2; //max nested runs per entity type
    private static IList<EntityRecord> _emptyList = new EntityRecord[] { };

    //TODO: test and fix the following: 
    // Includes - when including child list, the list initialized only if it's not empty;
    //    if empty, it remains uninitialized, and on touch fwk fires select query with 0 results
    // Initially found in some external solution, seemed to be broken, but now maybe working. Needs to be retested! 
    internal static void RunIncludeQueries(LinqCommand command, object mainQueryResult) {
      // initial checks if there's anything to run
      if(mainQueryResult == null)
        return;
      var session = command.Session;
      var allIncludes = session.Context.GetMergedIncludes(command.Includes);
      if(allIncludes == null || allIncludes.Count == 0)
        return;
      var resultShape = GetResultShape(session.Context.App.Model, mainQueryResult.GetType());
      if(resultShape == QueryResultShape.Object)
        return;
      // Get records from query result
      var records = new List<EntityRecord>();
      switch(resultShape) {
        case QueryResultShape.Entity:
          records.Add( EntityHelper.GetRecord(mainQueryResult));
          break;
        case QueryResultShape.EntityList:
          var list = mainQueryResult as IList;
          if (list.Count == 0)
            return; 
          foreach (var ent in list)
            records.Add( EntityHelper.GetRecord(ent));
          break;
      }//switch;
      // actually run the includes
      var entityType = records[0].EntityInfo.EntityType;
      var helper = new IncludeProcessor(session, allIncludes);
      session.LogMessage("------- Running include queries   ----------");
      helper.RunIncludeQueries(entityType, records);
      session.LogMessage("------- Completed include queries ----------");
    }

    #region Instance fields, constructor
    EntitySession _session;
    IList<LambdaExpression> _includes;
    StringSet _processedRecordKeys = new StringSet();
    IDictionary<Type, int> _runsPerEntityType = new Dictionary<Type, int>();

    internal IncludeProcessor(EntitySession session, IList<LambdaExpression> includes) {
      _session = session;
      _includes = includes; 
    }
    #endregion

    // Private methods
    private int IncrementRunCount(Type entityType) {
      int oldCount;
      if (!_runsPerEntityType.TryGetValue(entityType, out oldCount))
        oldCount = 0;
      var newCount = oldCount + 1;
      _runsPerEntityType[entityType] = newCount;
      return newCount;
    }

    private void RunIncludeQueries(Type entityType, IList<EntityRecord> records) {
      if(records.Count == 0)
        return;
      var matchingIncludes = _includes.Where(f => f.Parameters[0].Type == entityType).ToList();
      if (matchingIncludes.Count == 0)
        return;
      //filter records 
      if(_processedRecordKeys.Count > 0) {
        records = records.Where(r => !_processedRecordKeys.Contains(r.PrimaryKey.AsString())).ToList();
        if(records.Count == 0)
          return;
      }
      var runCount = IncrementRunCount(entityType);
      if(runCount > MaxNestedRunsPerEntityType)
        return;
      IList<EntityRecord> results; 
      foreach (var include in matchingIncludes) {
        var body = include.Body;
        switch (body.NodeType) {
          case ExpressionType.MemberAccess:
            // ex: session.EntitySet<IBookReview>().Where(...).Include(r => r.Book).ToList();
            //    include: (r=>r.Book)
            var ma = body as MemberExpression; // r.Book
            results = RunIncludeForMember(entityType, records, ma);
            // add pk-s to processed records 
            _processedRecordKeys.UnionWith(results.Select(r => r.PrimaryKey.AsString()));
            break; 
          case ExpressionType.New:
            var newExpr = (NewExpression)body;
            foreach (var arg in newExpr.Arguments) {
              var marg = arg as MemberExpression;
              RunIncludeForMember(entityType, records, marg);
            }
            break;
          default:
            Util.Check(false, "Invalid Include expression: {0}.", body);
            break;
        }//switch body.NodeType
      }//foreach include
    }//method

    internal IList<EntityRecord> RunIncludeForMember(Type entityType, IList<EntityRecord> records, MemberExpression ma) {
      IList<EntityRecord> resultRecords; 
      // Check for chained members
      if (ma.Expression.Type != entityType) {
        // ex: include (r => r.Book.Publisher }
        Util.Check(ma.Expression.NodeType == ExpressionType.MemberAccess, 
                  "Invalid Include nested expression, must be member access: {0}", ma.Expression);
        var nestedMa = ma.Expression as MemberExpression; //r.Book
        var nestedRecs = RunIncludeForMember(entityType, records, nestedMa); //books
        resultRecords = RunIncludeForMember(ma.Expression.Type, nestedRecs, ma);  // (session, IBook, books, b => b.Publisher)
        RunIncludeQueries(ma.Type, resultRecords);
        return resultRecords; 
      }
      var ent = _session.Context.App.Model.GetEntityInfo(entityType); //IBookReview
      var refMember = ent.Members.First(m => m.ClrMemberInfo == ma.Member); //r.Book
      switch (refMember.Kind) {
        case EntityMemberKind.EntityRef:
          resultRecords = RunIncludeForEntityRef(records, refMember);
          RunIncludeQueries(refMember.DataType, resultRecords);
          return resultRecords; 
        case EntityMemberKind.EntityList:
          var listInfo = refMember.ChildListInfo;
          switch (listInfo.RelationType) {
            case EntityRelationType.ManyToOne:
              resultRecords = RunIncludeForListManyToOne(records, refMember);
              RunIncludeQueries(listInfo.TargetEntity.EntityType, resultRecords);
              return resultRecords; 
            case EntityRelationType.ManyToMany:
              resultRecords = RunIncludeForListManyToMany(records, refMember);
              RunIncludeQueries(listInfo.TargetEntity.EntityType, resultRecords);
              return resultRecords; 
          }//switch rel type
          break;
        default:
          Util.Check(false, "Invalid expression in Include query, member {0}: must be entity reference or entity list.", refMember.MemberName);
          return _emptyList;
      }
      return _emptyList;
    }

    private IList<EntityRecord> RunIncludeForEntityRef(IList<EntityRecord> records, EntityMemberInfo refMember) {
      if (records.Count == 0)
        return _emptyList;
      var targetEntity = refMember.ReferenceInfo.ToKey.Entity;
      var fkMember = refMember.ReferenceInfo.FromKey.ExpandedKeyMembers[0].Member; // r.Book_Id
      var fkValues = GetDistinctMemberValues(records, fkMember);
      var selectCmd = LinqCommandFactory.CreateSelectByKeyValueArray(_session, targetEntity.PrimaryKey, null, fkValues);
      var entList = (IList) _session.ExecuteLinqCommand(selectCmd, withIncludes: false);
      if (entList.Count == 0)
        return _emptyList;
      var recList = GetRecordList(entList);
      // Set ref members in parent records
      var targetPk = refMember.ReferenceInfo.ToKey;
      foreach (var parentRec in records) {
        var fkValue = parentRec.GetValueDirect(fkMember);
        if (fkValue == DBNull.Value) {
          parentRec.SetValueDirect(refMember, DBNull.Value);
        } else {
          var pkKey = new EntityKey(targetPk, fkValue);
          //we lookup in session, instead of searching in results of Include query - all just loaded records are registered in session and lookup is done by key (it is fact dict lookup)
          var targetRec = _session.GetRecord(pkKey);
          parentRec.SetValueDirect(refMember, targetRec);
        }
      }
      return recList; 
    }

    public static IList<EntityRecord> GetRecordList(IList entityList) {
      var recList = new List<EntityRecord>(); 
      foreach(var ent in entityList) {
        var rec = ((IEntityRecordContainer)ent).Record;
        recList.Add(rec); 
      }
      return recList; 
    }

    // Example: records: List<IBookOrder>, listMember: bookOrder.Lines; so we load lines for each book order
    private IList<EntityRecord> RunIncludeForListManyToOne(IList<EntityRecord> records, EntityMemberInfo listMember) {
      var pkInfo = listMember.Entity.PrimaryKey;
      var expMembers = pkInfo.ExpandedKeyMembers; 
      Util.Check(expMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember);
      var pkMember = expMembers[0].Member; // IBookOrder.Id
      var pkValuesArr = GetDistinctMemberValues(records, pkMember);
      var listInfo = listMember.ChildListInfo;
      var parentRefMember = listInfo.ParentRefMember; //IBookOrderLine.Order
      var fromKey = parentRefMember.ReferenceInfo.FromKey;
      Util.Check(fromKey.ExpandedKeyMembers.Count == 1, "Composite keys are not supported in Include expressions; member: {0}", parentRefMember);
      var selectCmd = LinqCommandFactory.CreateSelectByKeyArrayForListPropertyManyToOne(_session, listInfo, pkValuesArr);
      var childEntities = (IList) _session.ExecuteLinqCommand(selectCmd, withIncludes: false); //list of all IBookOrderLine for BookOrder objects in 'records' parameter
      var childRecs = GetRecordList(childEntities); 
      //setup list properties in parent records
      var fk = fromKey.ExpandedKeyMembers[0].Member; //IBookOrderLine.Order_Id
      var groupedRecs = childRecs.GroupBy(rec => rec.GetValueDirect(fk)); //each group is list of order lines for a single book order; group key is BookOrder.Id
      foreach (var g in groupedRecs) {
        var pkValue = new EntityKey(pkInfo, g.Key); // Order_Id -> BookOrder.Id
        var parent = _session.GetRecord(pkValue); // BookOrder
        var childList = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if (childList != null && childList.IsLoaded)
          continue; 
        if (childList == null)         
          childList = parent.InitChildEntityList(listMember);
        var grpChildEntities = g.Select(r => r.EntityInstance).ToList();
        childList.SetItems(grpChildEntities);
      }
      // If for some parent records child lists were empty, we need set the list property to empty list, 
      // If it remains null, it will be considered not loaded, and app will attempt to load it again on first touch
      foreach (var parent in records) {
        var list = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList;
        if (list == null)
          list = parent.InitChildEntityList(listMember);
        if (!list.IsLoaded)
          list.SetAsEmpty();
      }
      return childRecs;
    }

    // Example: records: List<IBook>, listMember: book.Author; so we load authors list for each book
    private IList<EntityRecord> RunIncludeForListManyToMany(IList<EntityRecord> records, EntityMemberInfo listMember) {
      var pkInfo = listMember.Entity.PrimaryKey;
      var keyMembers = pkInfo.ExpandedKeyMembers;
      Util.Check(keyMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember);
      var listInfo = listMember.ChildListInfo;

      // PK values of records
      var pkValues = GetDistinctMemberValues(records, keyMembers[0].Member);
      //run include query; it will return LinkTuple list
      var cmd = LinqCommandFactory.CreateSelectByKeyArrayForListPropertyManyToMany(_session, listInfo, pkValues);
      var tuples = (IList<LinkTuple>) _session.ExecuteLinqCommand(cmd, withIncludes: false);

      // Group by parent record, and push groups/lists into individual records
      var fkMember = listInfo.ParentRefMember.ReferenceInfo.FromKey.ExpandedKeyMembers[0].Member;
      var tupleGroups = tuples.GroupBy(t => EntityHelper.GetRecord(t.LinkEntity).GetValueDirect(fkMember)).ToList(); 
      foreach(var g in tupleGroups) {
        var pkValue = new EntityKey(pkInfo, g.Key); // Order_Id -> BookOrder.Id
        var parent = _session.GetRecord(pkValue); // BookOrder
        var childList = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if(childList != null && childList.IsLoaded)
          continue;
        if(childList == null)
          childList = parent.InitChildEntityList(listMember);
        var groupTuples = g.ToList();
        childList.SetItems(groupTuples);
      }
      // Init/clear all lists that were NOT loaded
      var emptyTuples = new List<LinkTuple>();
      foreach(var rec in records) {
        var childList = rec.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if(childList != null && childList.IsLoaded)
          continue;
        if(childList == null)
          childList = rec.InitChildEntityList(listMember);
        childList.SetItems(emptyTuples);
      }
      // collect all target records as function result
      var targetRecords = tuples.Select(t => EntityHelper.GetRecord(t.TargetEntity)).ToList();
      return targetRecords; 
    }

    public static IList GetDistinctMemberValues(IList<EntityRecord> records, EntityMemberInfo member) {
      // using Distinct with untyped values - might be questionable, but it appears it works, 
      // including with value types
      var values = records.Select(r => r.GetValueDirect(member))
                      .Where(v => v != null && v != DBNull.Value)
                      .Distinct()
                      .ToArray();
      return values;
    }

    private static QueryResultShape GetResultShape(EntityModel model, Type outType) {
      if(typeof(EntityBase).IsAssignableFrom(outType))
        return QueryResultShape.Entity;
      if(outType.IsGenericType) {
        var genArg0 = outType.GetGenericArguments()[0];
        if(typeof(IEnumerable).IsAssignableFrom(outType) && model.IsEntity(genArg0))
          return QueryResultShape.EntityList;
      }
      return QueryResultShape.Object; // don't know and don't care      
    }


  }//class
}
 