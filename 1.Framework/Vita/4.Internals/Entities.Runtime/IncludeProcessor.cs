using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Model;
using Vita.Entities.Utilities;
using Vita.Data;
using Vita.Data.Linq;

namespace Vita.Entities.Runtime {

  internal class IncludeProcessor {
    public static int MaxNestedRunsPerEntityType = 2; //max nested runs per entity type
    private static IList<EntityRecord> _emptyList = new EntityRecord[] { };

    // Aug 2018 - disabled for preview version, needs more work
    //TODO: test and fix the following: 
    // Includes - when including child list, the list initialized only if it's not empty;
    //    if empty, it remains uninitialized, and on touch fwk fires select query with 0 results
    // Initially found on some external solution, seemed to be broken, but now maybe working. Needs to be retested! 
    internal static void RunIncludeQueries(EntitySession session, LinqCommand command, object mainQueryResult) {
      return; 
      /*
      // initial checks if there's anything to run
      var resultShape = command.Info.ResultShape;
      if (mainQueryResult == null || resultShape == QueryResultShape.Object)
        return;
      var allIncludes = session.Context.GetMergedIncludes(command.Info.Includes);
      if (allIncludes == null || allIncludes.Count == 0)
        return; 
      // Get records from query result
      var records = new List<EntityRecord>();
      switch (resultShape) {
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
      helper.RunIncludeQueries(entityType, records); 
      */
    }

    #region Instance fields, constructor
    EntitySession _session;
    IList<LambdaExpression> _includes;
    StringSet _processedRecordKeys = new StringSet();
    IDictionary<Type, int> _runsPerEntityType = new Dictionary<Type, int>();

    private IncludeProcessor(EntitySession session, IList<LambdaExpression> includes) {
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
      var runCount = IncrementRunCount(entityType);
      if (runCount > MaxNestedRunsPerEntityType)
        return; 
      var matchingIncludes = _includes.Where(f => f.Parameters[0].Type == entityType).ToList();
      if (matchingIncludes.Count == 0)
        return;
      //filter records 
      if (_processedRecordKeys.Count > 0)
        records = records.Where(r => !_processedRecordKeys.Contains(r.PrimaryKey.AsString())).ToList();
      if (records.Count == 0)
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

    private IList<EntityRecord> RunIncludeForMember(Type entityType, IList<EntityRecord> records, MemberExpression ma) {
      IList<EntityRecord> resultRecords; 
      // Check for chained members
      if (ma.Expression.Type != entityType) {
        // ex: include (r => r.Book.Publisher }
        Util.Check(ma.Expression.NodeType == ExpressionType.MemberAccess, "Invalid Include nested expression, must be member access: {0}", ma.Expression);
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
      var fkValues = GetMemberValuesAsTypedArray(records, fkMember); 
      var selectCmdInfo = GetSelectByKeyValueArrayCommand(targetEntity.PrimaryKey);
      var selectCmd = new LinqCommand(selectCmdInfo, targetEntity, new object[] { fkValues });
      var entList = (IList) _session.ExecuteLinqCommand(selectCmd);
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

    public LinqCommandInfo GetSelectByKeyValueArrayCommand(EntityKeyInfo key) {
      return SelectCommandBuilder.BuildSelectByMemberValueArray(key.ExpandedKeyMembers[0].Member);
    }


    // Example: records: List<IBookOrder>, listMember: bookOrder.Lines; so we load lines for each book order
    private IList<EntityRecord> RunIncludeForListManyToOne(IList<EntityRecord> records, EntityMemberInfo listMember) {
      var pkInfo = listMember.Entity.PrimaryKey;
      var expMembers = pkInfo.ExpandedKeyMembers; 
      Util.Check(expMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember);
      var pkMember = expMembers[0].Member; // IBookOrder.Id
      var pkValuesArr = GetMemberValuesAsTypedArray(records, pkMember);
      var listInfo = listMember.ChildListInfo;
      var parentRefMember = listInfo.ParentRefMember; //IBookOrderLine.Order
      var fromKey = parentRefMember.ReferenceInfo.FromKey;
      Util.Check(fromKey.ExpandedKeyMembers.Count == 1, "Composite keys are not supported in Include expressions; member: {0}", parentRefMember);
      var cmdInfo = GetSelectByKeyValueArrayCommand(fromKey); 
      Util.Check(cmdInfo != null, "Select command for entity reference {0} not defined.", fromKey);
      var cmd = new LinqCommand(cmdInfo, listInfo.TargetEntity, new object[] { pkValuesArr });
      var childEntities = (IList) _session.ExecuteLinqCommand(cmd); //list of all IBookOrderLine for BookOrder objects in 'records' parameter
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
        var grpChildEntities = g.Select(r => r.EntityInstance).ToList(); 
        if (childList == null)         
          childList = parent.InitChildEntityList(listMember);
        childList.Init(grpChildEntities);
      }
      // If for some parent records child lists were empty, we need set the list property to empty list, 
      // If it remains null, it will be considered not loaded, and app will attempt to load it again on first touch
      foreach (var parent in records) {
        var value = parent.ValuesTransient[listMember.ValueIndex];
        if (value == null)
          parent.InitChildEntityList(listMember);
      }
      return childRecs;
    }

    // Example: records: List<IBook>, listMember: book.Author; so we load authors list for each book
    private IList<EntityRecord> RunIncludeForListManyToMany(IList<EntityRecord> records, EntityMemberInfo listMember) {
      var pkInfo = listMember.Entity.PrimaryKey;
      var expMembers = pkInfo.ExpandedKeyMembers;
      Util.Check(expMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember);
      var listInfo = listMember.ChildListInfo;
      Util.Check(listInfo.OrderBy == null && listInfo.Filter == null, "Include facility not supported for lists with filter or OrderBy attribute. List: {0}", listMember.MemberName);
      var parentRefMember = listInfo.ParentRefMember; //IBookAuthor.Book
      var parentRefKey = parentRefMember.ReferenceInfo.FromKey;
      Util.Check(parentRefKey.ExpandedKeyMembers.Count == 1, 
           "Composite keys are not supported in Include expressions; member: {0}", parentRefMember);

      //Link records
      var pkMember = expMembers[0].Member; // IBook.Id
      var pkValues = GetMemberValuesAsTypedArray(records, pkMember);
      //list of all IBookAuthor for Book objects in 'records' parameter
      IList<EntityRecord> linkRecs = _emptyList;
      if (pkValues.Length > 0) {
        var fromKey = listInfo.ParentRefMember.ReferenceInfo.FromKey;
        var cmdInfo = GetSelectByKeyValueArrayCommand(fromKey);
        var cmd = new LinqCommand(cmdInfo, listInfo.LinkEntity, new object[] { pkValues });
        var linkEntList = (IList) _session.ExecuteLinqCommand(cmd);
        linkRecs = GetRecordList(linkEntList);
      }
      var parentRefFk = parentRefKey.ExpandedKeyMembers[0].Member;
      //each group is list of IBookAuthor for a single book; group key is BookAuthor.BookId
      var groupedLinkRecs = linkRecs.GroupBy(rec => rec.GetValueDirect(parentRefFk)).ToList(); 

      //Target records 
      var linkToTargetMember = listMember.ChildListInfo.OtherEntityRefMember;
      var linkToTargetKey = linkToTargetMember.ReferenceInfo.FromKey;
      var expTargetMembers = linkToTargetKey.ExpandedKeyMembers;
      Util.Check(expTargetMembers.Count == 1, "Include expression not supported for entities with composite keys, property: {0}.", listMember.ChildListInfo.OtherEntityRefMember);
      var linkToTargetFk = expTargetMembers[0].Member;
      var fkValues = GetMemberValuesAsTypedArray(linkRecs, linkToTargetFk);
      //list of all IAuthor for Book objects in 'records' parameter
      IList<EntityRecord> targetRecs;
      if (fkValues.Length == 0)
        targetRecs = new List<EntityRecord>();
      else {
        var targetKey = linkToTargetMember.ReferenceInfo.ToKey; //IAuthor.Id
        Util.Check(targetKey.ExpandedKeyMembers.Count == 1, "Include expression not supported for entities with composite keys, entity: {0}.", targetKey.Entity.Name);
        var targetCmdInfo = GetSelectByKeyValueArrayCommand(linkToTargetKey);
        Util.Check(targetCmdInfo != null, "Select command for entity reference {0} not defined.", linkToTargetKey);
        var cmd = new LinqCommand(targetCmdInfo, listInfo.TargetEntity, new object[] { fkValues });
        // ??? that will fail, need to complete refactoring to Linq queries
        var targetEnts = (IList) _session.ExecuteLinqCommand(cmd, withIncludes: false);
        targetRecs = ToRecords(targetEnts);
      }
      //fill out lists
      foreach (var linkGroup in groupedLinkRecs) {
        var pkValue = new EntityKey(pkInfo, linkGroup.Key); // BookAuthor.Book_id
        var parent = _session.GetRecord(pkValue); // Book
        var childList = parent.ValuesTransient[listMember.ValueIndex] as IPropertyBoundList; //BookOrder.Lines, list object
        if (childList != null && childList.IsLoaded)
          continue;
        if (childList == null)
          childList = parent.InitChildEntityList(listMember);
        var linkEntities = linkGroup.Select(r => r.EntityInstance).ToList();
        var targetEntities = linkGroup.Select(r => (IEntityRecordContainer) r.GetValue(linkToTargetMember)).ToList(); // touch and collect bookAuthor.Author references; 
        childList.Init(targetEntities, linkEntities);
      }
      // If for some parent records child lists were empty, we need set the list property to empty list, 
      // If it remains null, it will be considered not loaded, and app will attempt to load it again on first touch
      var empty = new IEntityRecordContainer[] { };
      foreach (var parent in records) {
        var value = parent.ValuesTransient[listMember.ValueIndex];
        if (value == null) {
          var list = parent.InitChildEntityList(listMember);
          list.Init(empty, empty);
        }
      }
      return linkRecs;
    }

    private static IList<EntityRecord> ToRecords(IList entities) {
      var records = new List<EntityRecord>();
      foreach(IEntityRecordContainer e in entities)
        records.Add(EntityHelper.GetRecord(e));
      return records; 
    }

    private static Array GetMemberValuesAsTypedArray(IList<EntityRecord> records, EntityMemberInfo member) {
      var objArray = new HashSet<object>(records.Select(r => r.GetValueDirect(member)))
          .Where(v => v != null && v != DBNull.Value).ToArray();
      var typedArray = Array.CreateInstance(member.DataType, objArray.Length);
      for (int i = 0; i < typedArray.Length; i++) 
        typedArray.SetValue(objArray[i], i);
      return typedArray;
    }

  }//class
}
 