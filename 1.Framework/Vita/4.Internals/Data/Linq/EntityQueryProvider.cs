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
      // if session is null, it means that query is not executable - it should be used only to DEFINE a query and translate it to SQL
      // but not execute it. Example: DbView definition
      Util.Check(Session != null, "The query is not executable. Query: {0}", expression);
      var objResult = Session.ExecuteQuery<TResult>(expression);
      return objResult;
    }
  
    object IQueryProvider.Execute(Expression expression) {
      Util.Check(Session != null, "The query is not executable. Query: {0}", expression);
      return Session.ExecuteQuery(expression); 
    }

  }//class
}//ns
