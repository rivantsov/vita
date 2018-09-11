using System;
using System.Collections.Generic;
using System.Text;
using Vita.Data.Model;
using Vita.Data.SqlGen;

namespace Vita.Data.Linq {

  // LinqLiteral is a special object for representing string literals or object names, when 
  // the value should NOT be quoted. If we use string constant in a Linq query, it will be translated into 
  // quoted string in the resulting SQL. To avoid quoting and produce string as-is, we use this wrapper - 
  // this is signal to SQL gen code to not add quotes. 
  public abstract class LinqLiteralBase {
    public abstract SqlFragment GetSql(DbModel dbModel);
  }

  public class LinqLiteral : LinqLiteralBase {
    public readonly string Value;

    public LinqLiteral(string value) {
      Value = value; 
    }
    public override SqlFragment GetSql(DbModel dbModel) {
      return new TextSqlFragment(Value); 
    }
  }

  public class LinqNameRefLiteral : LinqLiteralBase {
    public string AreaName;
    public string ObjectName; 

    public LinqNameRefLiteral(string areaName, string objectName) {
      AreaName = areaName;
      ObjectName = objectName; 
    }

    public override SqlFragment GetSql(DbModel dbModel) {
      var schema = dbModel.Config.GetSchema(AreaName);
      var fullName = dbModel.FormatFullName(schema, ObjectName);
      return new TextSqlFragment(fullName); 
    }
  }


}
