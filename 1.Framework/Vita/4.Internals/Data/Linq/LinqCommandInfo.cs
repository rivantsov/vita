using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Entities.Locking;

namespace Vita.Data.Linq {

  /// <summary>Contains basic information about the Linq command (query). Produced by the LinqCommandAnalyzer.</summary>
  /// <remarks> Contains query parameter values and cache key - this data allows looking up translated query definition from query cache
  /// and execute using current parameters. 
  /// </remarks>
  public class LinqCommandInfo {
    public QueryOptions Options;
    public LockType LockType;
    public bool IsView;
    public IList<Type> EntityTypes;
    // CacheKey and parameters - assigned by pre-analyzer; 
    // these values are minimum that is necessary for repeated query execution - with translated query created earlier 
    // and saved in query cache; all we need for repeated execution is CacheKey to get the translated query 
    // from cache (SQL), and current values for SQL parameters. 
    public string CacheKey;
    public IList<ParameterExpression> ExternalParameters;
    //Assigned by Pre-processor
    public HashSet<EntityInfo> Entities = new HashSet<EntityInfo>();
    public LambdaExpression Lambda;
    public QueryResultShape ResultShape;
    public IList<LambdaExpression> Includes;
    public EntityMemberMask MemberMask; //for queries returning entities

    public LinqCommandInfo(QueryOptions options, LockType lockType, bool isView, IList<Type> entityTypes, string cacheKey, IList<ParameterExpression> externalParameters, 
                     IList<LambdaExpression> includes) {
      Options = options;
      LockType = lockType;
      IsView = isView; 
      EntityTypes = entityTypes; 
      CacheKey = cacheKey;
      ExternalParameters = externalParameters;
      Includes = includes; 
    }

  }//class

}
