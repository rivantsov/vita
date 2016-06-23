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

  internal class UrlDateTimeHandler {
    // Not null if there's a parameter with [FromUrl] attribute
    Type _fromUrlParamType; 
    IList<PropertyInfo> _fromUrlDateTimeProperties; 

    public UrlDateTimeHandler(ParameterInfo fromUrlParameter) {
      if (fromUrlParameter != null) {
        _fromUrlParamType = fromUrlParameter.ParameterType;
        _fromUrlDateTimeProperties = GetDateTimeProperties(_fromUrlParamType);
      }

    }

    public object Convert(object value) {
      if(value == null)
        return null;
      var type = value.GetType();
      if(type == typeof(DateTime) || type == typeof(DateTime?))
        return ConvertDateTime(value);
      if(type == _fromUrlParamType)
        ConvertFromUrlObjectProperties(value);
      return value; 
    }

    private object ConvertDateTime(object value) {
      var dt = (DateTime)value;
      if(dt.Kind == DateTimeKind.Local)
        return dt.ToUniversalTime();
      else
        return dt;
    }

    private object ConvertFromUrlObjectProperties(object urlObject) {
      //reassign values
      foreach(var p in _fromUrlDateTimeProperties) {
        var dtObj = p.GetValue(urlObject);
        if (dtObj == null)
          continue;
        var utc = ConvertDateTime(dtObj);
        p.SetValue(urlObject, utc);
      }
      return urlObject; 
    }

    public static IList<PropertyInfo> GetDateTimeProperties(Type type) {
      return type.GetProperties().Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == (typeof(DateTime?))).ToList();
    }

  }//class

}
