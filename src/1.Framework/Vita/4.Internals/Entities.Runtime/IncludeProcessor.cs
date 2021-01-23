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
      var resultShape = MemberLoadHelper.GetResultShape(session.Context.App.Model, mainQueryResult.GetType());
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
          resultRecords = MemberLoadHelper.LoadEntityRefMember(_session, records, refMember);
          RunIncludeQueries(refMember.DataType, resultRecords);
          return resultRecords; 
        case EntityMemberKind.EntityList:
          var listInfo = refMember.ChildListInfo;
          switch (listInfo.RelationType) {
            case EntityRelationType.ManyToOne:
              resultRecords = MemberLoadHelper.LoadListManyToOneMember(_session, records, refMember);
              RunIncludeQueries(listInfo.TargetEntity.EntityType, resultRecords);
              return resultRecords; 
            case EntityRelationType.ManyToMany:
              resultRecords = MemberLoadHelper.LoadListManyToManyMember(_session, records, refMember);
              RunIncludeQueries(listInfo.TargetEntity.EntityType, resultRecords);
              return resultRecords; 
          }//switch rel type
          break;
        default:
          Util.Check(false, "Invalid expression in Include query, member {0}: must be entity reference or entity list.", refMember.MemberName);
          return EntityRecord.EmptyList;
      }
      return EntityRecord.EmptyList;
    }


  }//class
}
 