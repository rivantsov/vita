using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vita.Entities.Utilities;
using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq {

  public class EntityQueryProvider : IQueryProvider {
    public readonly EntitySession Session; //might be null

    //Session might be null for non-executable query. These are used at startup to define views using LINQ
    public EntityQueryProvider(EntitySession session = null) {
      Session = session; 
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) {
      return new EntityQuery<TElement>(this, expression);
    }

    static MethodInfo _createEntityQueryMethod;

    IQueryable IQueryProvider.CreateQuery(Expression expression) {
      //Note: this method is almost never called, so we do not worry about efficiency here
      if(_createEntityQueryMethod == null)
        _createEntityQueryMethod = this.GetType().GetMethod("CreateEntityQuery", BindingFlags.Instance | BindingFlags.NonPublic);
      var elementType = expression.Type.GetGenericArguments()[0];
      var genMethod = _createEntityQueryMethod.MakeGenericMethod(elementType);
      var query = (EntityQuery)genMethod.Invoke(null, new object[] {expression });
      return query;
    }

    TResult IQueryProvider.Execute<TResult>(Expression expression) {
      var qp = (IQueryProvider)this;
      var objResult = qp.Execute(expression);
      if (objResult == null)
        return default(TResult);
      if (typeof(TResult).IsAssignableFrom(objResult.GetType()))
        return (TResult)objResult;
      //one special case - when TResult is IEnumerable<T> but query returns IEnumerable<T?>
      var resType = typeof(TResult);
      if (resType.IsGenericType && resType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
        var list = ConvertHelper.ConvertEnumerable(objResult as IEnumerable, resType);
        return (TResult)list;
      }
      Util.Throw("Failed to convert query result of type {0} to type {1}.", objResult.GetType(), resType);
      return default(TResult);
    }
  
    object IQueryProvider.Execute(Expression expression) {
      // if session is null, it means that query is not executable - it should be used only to DEFINE a query and translate it to SQL
      // but not execute it. Example: DbView definition
      Util.Check(Session != null, "The query is not executable. Query: {0}", expression);
      var elemType = expression.Type.IsGenericType ? expression.Type.GenericTypeArguments[0] : typeof(object);
      var command = new LinqCommand(expression, LinqCommandKind.Select, null);
      var result = Session.ExecuteLinqCommand(command);
      return result;
    }

  }//class
}//ns
