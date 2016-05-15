
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using Vita.Common;
using Vita.Data.Linq.Translation;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Linq;
using Vita.Entities.Runtime;

//RI: this class is a replacement for SelectQuery
namespace Vita.Data.Linq {

  public class LinqCommandParameter {
    public string Name;
    public int Index;
    public Type Type;
    public Func<object[], object> ReadValue;

    public LinqCommandParameter(string name, int index, Type type, Func<object[], object> readValueFunc) {
      Name = name;
      Index = index;
      Type = type;
      ReadValue = readValueFunc;
    }
  }

  /// <summary>Represents a linq query, with SQL statement and parameter definitions. </summary>
  public partial class TranslatedLinqCommand  {
    public string BatchSqlTemplate; 
    public readonly string Sql;
    /// <summary> Parameters to be sent as SQL parameters.</summary>
    public IList<LinqCommandParameter> Parameters;

    /// <summary>
    /// Expression that creates a row object
    /// Use GetRowObjectCreator() to access the object with type safety
    /// </summary>
    public readonly Func<IDataRecord, EntitySession, object> ObjectMaterializer;

    public Func<IList> ResultListCreator; //Creates empty generic List<T> list to fill up the results
    public QueryResultsProcessor ResultsPostProcessor;

    public LinqCommandFlags Flags; 

    public TranslatedLinqCommand(string sqlTemplate, string sql, 
                    IList<LinqCommandParameter> parameters, 
                    LinqCommandFlags flags, 
                    Func<IDataRecord, EntitySession, object> objectMaterializer = null, 
                    QueryResultsProcessor resultsPostProcessor = null,
                    Func<IList> resultListCreator = null) {
      BatchSqlTemplate = sqlTemplate;
      Sql = sql; 
      Parameters = parameters;
      Flags = flags; 
      ObjectMaterializer = objectMaterializer;
      ResultsPostProcessor = resultsPostProcessor; 
      ResultListCreator = resultListCreator;
    }

  }//class
}
