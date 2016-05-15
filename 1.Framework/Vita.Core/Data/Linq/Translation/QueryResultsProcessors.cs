using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities.Runtime;

namespace Vita.Data.Linq.Translation {

  using System.Reflection;
  using ProcessorFunc = Func<IEnumerable, object>;

  public abstract class QueryResultsProcessor {
    public abstract object ProcessRows(IEnumerable rows);

    public static QueryResultsProcessor CreateFirstSingleLast(string methodName, Type rowType) {
      var processorType = typeof(FirstSingleLastProcessor<>).MakeGenericType(rowType);
      var processor = (QueryResultsProcessor)Activator.CreateInstance(processorType, methodName);
      return processor;
    }

    public static QueryResultsProcessor CreateGroupBy(Type keyType, Type rowType) {
      var processorType = typeof(GroupByProcessor<,>).MakeGenericType(keyType, rowType);
      var result = (QueryResultsProcessor)Activator.CreateInstance(processorType);
      return result;
    }
  }

  #region First/Single/Last processor
  public class FirstSingleLastProcessor<TRow> : QueryResultsProcessor {
    Func<IEnumerable<TRow>, TRow> _implementation;

    public FirstSingleLastProcessor(string methodName) {
      switch(methodName) {
        case "First": 
          _implementation = rows => rows.First(); 
          break; 
        case "FirstOrDefault": 
          _implementation = rows => rows.FirstOrDefault(); 
          break;
        case "Single": 
          _implementation = rows => rows.Single(); 
          break;
        case "SingleOrDefault": 
          _implementation = rows => rows.SingleOrDefault(); 
          break;
        case "Last": 
          _implementation = rows => rows.First(); 
          break;
        case "LastOrDefault": 
          _implementation = rows => rows.FirstOrDefault(); 
          break; 
        default:
          Util.Throw("Unknown method name for QueryResultsProcessor: {0}.", methodName);
          break; 
      } //switch
    }

    public override object ProcessRows(IEnumerable rows) {
      IEnumerable<TRow> tRows;
      if (typeof(IEnumerable<TRow>).IsAssignableFrom(rows.GetType())) 
        tRows = (IEnumerable<TRow>)rows;
      else {
        tRows = (IEnumerable<TRow>) ConvertHelper.ConvertEnumerable(rows, typeof(IEnumerable<TRow>));
      }
      return _implementation(tRows);
    }

  }
  #endregion


  #region GroupByProcessor
  public class TransientKeyValuePair<TKey, TRow> {
    public TKey Key { get; set; }
    public TRow Row { get; set; }
    public TransientKeyValuePair(TKey key, TRow row) {
      Key = key;
      Row = row;
    }
  }
  public class GroupByProcessor<TKey, TRow> : QueryResultsProcessor {
    public override object ProcessRows(IEnumerable rows) {
      var lRows = rows as IList<TransientKeyValuePair<TKey, TRow>>;
      var result = lRows.GroupBy(p => p.Key, p => p.Row).ToList();
      return result; 
    }
  }
  #endregion

  #region ColumnReaderHelper
  public static class ColumnReaderHelper {
    private static MethodInfo _readColumnMethod;

    public static Expression GetColumnValueReader(ParameterExpression dataRecordParameter, int valueIndex, Type outputType, Func<object, object> conv) {
      _readColumnMethod = _readColumnMethod ?? typeof(ColumnReaderHelper).GetMethod("ReadColumnValue", BindingFlags.Static | BindingFlags.NonPublic);
      object dbNullValue = outputType.IsNullableValueType() ? null : ReflectionHelper.GetDefaultValue(outputType);
      var call = Expression.Call(null, _readColumnMethod, dataRecordParameter, Expression.Constant(valueIndex),
        Expression.Constant(outputType), Expression.Constant(dbNullValue, typeof(object)),
        Expression.Constant(conv, typeof(Func<object, object>)));
      var result = Expression.Convert(call, outputType);
      return result;
    }

    private static object ReadColumnValue(IDataRecord record, int valueIndex,
                                    Type outputType, object dbNullValue, Func<object, object> converter) {
      var value = record[valueIndex];
      if(value == DBNull.Value)
        return dbNullValue;
      if(value != null && value.GetType() == outputType)
        return value;
      if(converter != null)
        value = converter(value);
      // type mismatch might happen here, rarely; 
      // ex: MySql and 'SELECT 1 From ...' - expected value is int32, but MySql returns long
      if(value != null && value.GetType() != outputType && !outputType.IsGenericType) //prevent from converting int->int?
        value = ConvertHelper.ChangeType(value, outputType);
      return value;
    }
  }//class
  #endregion
}
