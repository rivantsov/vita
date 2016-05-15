using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Entities.Linq {

  [Flags]
  public enum LinqCommandFlags {
    None = 0, 
    NoQueryCache = 1,

    NoLock = 1 << 4, 
    ReadLock = 1 << 5,
    WriteLock = 1 << 6,
  }

  /// <summary>Contains basic information about the query. Produced by the QueryExpressionAnalyzer.</summary>
  /// <remarks> Contains query parameter values and cache key - this data allows looking up translated query definition from query cache
  /// and execute using current parameters. 
  /// </remarks>
  public class LinqCommandInfo {
    public LinqCommandType CommandType;
    public LinqCommandKind CommandKind;
    public QueryOptions Options;
    public List<Type> EntityTypes;
    // CacheKey and parameters - assigned by pre-analyzer; 
    // these values are minimum that is necessary for repeated query execution - with translated query created earlier 
    // and saved in query cache; all we need for repeated execution is CacheKey to get the translated query 
    // from cache (SQL), and current values for SQL parameters. 
    public string CacheKey;
    public List<ParameterExpression> ExternalParameters;
    //Assigned by Pre-processor
    public HashSet<EntityInfo> Entities = new HashSet<EntityInfo>();
    public LambdaExpression Lambda;
    public QueryResultShape ResultShape;
    public LinqCommandFlags Flags;
    public List<LambdaExpression> Includes;

    public LinqCommandInfo(LinqCommand command, QueryOptions options, LinqCommandFlags flags,
                     List<Type> entityTypes, string cacheKey, List<ParameterExpression> externalParameters, 
                     List<LambdaExpression> includes) {
      CommandType = command.CommandType;
      CommandKind = command.Kind;
      Options = options;
      Flags = flags;
      EntityTypes = entityTypes; 
      CacheKey = cacheKey;
      ExternalParameters = externalParameters;
      Includes = includes; 
    }

  }//class


}
