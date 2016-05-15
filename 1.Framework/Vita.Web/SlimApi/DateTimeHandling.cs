using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;
using Vita.Common; 

namespace Vita.Web.SlimApi {

  // It turns out by default Web Api uses 'model binding' for URL parameters, which 
  // results in datetime in URL (sent as UTC) to be converted to local datetime (local for server). 
  // This is inconsistent with DateTime values in body (json) - by default NewtonSoft deserializer treats them as UTC values
  // It is more convenient to treat datetime strings as UTC. 
  // VITA provides a fix - it automatcally detects local datetimes in parameter values and converts them to UTC
  // This includes optional/nullable DateTime values inside complex objects with [FromUr.] attribute

  internal interface IDateTimeUtcConverter {
    object Convert(object value);
  }

  // Handles date time values in URL parameters, converts them to UTC if they are local datetime
  internal class SimpleDateTimeValueConverter : IDateTimeUtcConverter {
    public object Convert(object value) {
      if (value == null)
        return null;
      var type = value.GetType();
      if (type != typeof(DateTime) && type != typeof(DateTime?))
        return value; 
      var dt = (DateTime)value;
      if (dt.Kind == DateTimeKind.Local)
        return dt.ToUniversalTime();
      else
        return dt; 
    }
  }//class

  // For parameter objects with [FromUrl] attribute, scans properties for datatime values, and 
  // converts them to UTC
  internal class FromUrlObjectDateTimeValueConverter : IDateTimeUtcConverter {
    Type _type; 
    IList<PropertyInfo> _dateTimeProperties; 
    public FromUrlObjectDateTimeValueConverter(Type type) {
      _type = type; 
      _dateTimeProperties = type.GetProperties().Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == (typeof(DateTime?))).ToList();

    }
    public bool HasDateTimeProperties {
      get {return _dateTimeProperties.Count > 0;}
    }

    public object Convert(object value) {
      if (value == null)
        return null; 
      var vtype = value.GetType();
      Util.Check(_type.IsAssignableFrom(vtype), "Invalid argument for DateTimeValueConverter; expected: {0}, actual: {1}.", _type, vtype);
      //reassign values
      foreach (var p in _dateTimeProperties) {
        var dtObj = p.GetValue(value);
        if (dtObj == null)
          continue;
        var dtvalue = (DateTime)dtObj; 
        if (dtvalue.Kind == DateTimeKind.Local)
          p.SetValue(value, dtvalue.ToUniversalTime());
      }
      return value; 
    }
  }

}
