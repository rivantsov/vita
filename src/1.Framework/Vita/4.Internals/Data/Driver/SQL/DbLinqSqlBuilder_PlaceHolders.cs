using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Vita.Data.Linq;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Sql;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Driver {

  public partial class DbLinqSqlBuilder {

    protected virtual SqlPlaceHolder CreateSqlPlaceHolder(ExternalValueExpression extValue) {
      var dataType = extValue.SourceExpression.Type;
      var driver = this.DbModel.Driver;
      var typeRegistry = driver.TypeRegistry;
      var valueReader = BuildParameterValueReader(extValue.SourceExpression);
      SqlPlaceHolder ph; 
      if(dataType.IsListOfDbPrimitive(out var elemType)) {
        // list parameter
        var elemTypeDef = typeRegistry.GetDbTypeDef(elemType);
        Util.Check(elemTypeDef != null, "Failed to match DB type for CLR type {0}", elemType);
        ph = new SqlListParamPlaceHolder(elemType, elemTypeDef, valueReader,
                   // ToLiteral
                   formatLiteral: list => driver.SqlDialect.ListToLiteral(list, elemTypeDef)
                   );
      } else {
        //regular linq parameter
        var typeDef = typeRegistry.GetDbTypeDef(dataType);
        Util.Check(typeDef != null, "Failed to find DB type for linq parameter of type {0}", dataType);
        var dbConv = typeRegistry.GetDbValueConverter(typeDef.ColumnOutType, dataType);
        Util.Check(dbConv != null, "Failed to find converter from type {0} to type {1}", dataType, typeDef.ColumnOutType);
        ph = new SqlParamPlaceHolder(dataType, typeDef, valueReader, dbConv.PropertyToColumn);
      }
      this.PlaceHolders.Add(ph);
      return ph; 
    }

    private Func<object[], object> BuildParameterValueReader(Expression valueSourceExpression) {
      var prms = Command.Lambda.Parameters; 
      // One trouble - for Binary object, we need to convert them to byte[]
      if(valueSourceExpression.Type == typeof(Binary)) {
        var methGetBytes = typeof(Binary).GetMethod(nameof(Binary.GetBytes));
        valueSourceExpression = Expression.Call(valueSourceExpression, methGetBytes);
      }
      //Quick path: most of the time the source expression is just a lambda parameter
      if(valueSourceExpression.NodeType == ExpressionType.Parameter) {
        var prmSource = (ParameterExpression)valueSourceExpression;
        var index = prms.IndexOf(prmSource);
        return (object[] values) => values[index];
      }
      // There is some computation in valueSourceExpression; use dynamic invoke to evaluate it.
      // DynamicInvoke is not efficient but this case is rare enough, so it is not worth more trouble
      var valueReadLambda = Expression.Lambda(valueSourceExpression, prms);
      var compiledValueRead = valueReadLambda.Compile();
      return (object[] values) => compiledValueRead.DynamicInvoke(values);
    }


  }
}
