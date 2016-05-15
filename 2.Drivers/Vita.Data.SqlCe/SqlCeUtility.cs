using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Data;

using Vita.Common;
using Vita.Data.Model;

namespace Vita.Data.SqlCe {

  public static class SqlCeUtility {
    
    // In SQL CE database, DateTimeOffset is represented by nvarchar(34). To allow for proper sorting, we store UTC datetime (not local)
    // followed by offset. However, if we try to use a DateTimeOffset constant in a LINQ query, it will be converted to string (nvachar)
    // using DateTimeOffset.ToString() method. There are two problems with this method.
    // First, the result is a local date-time (!) followed by offset. So to be compatible with database, we need to "shift" the value 
    // to London, so that local time is UTC time, so it will appear in ToString() representation.
    // Second, the DateTimeOffset.ToString uses ShortDatePattern to format the date, which results in 'mm/dd/yyyy' in US locale.
    // What we need is 'yyyy/mm/dd'. The only way to do this is to change short datetime format on current culture on current thread.
    private static CultureInfo _adjustedCulture;
    public static void AdjustDefaultDateTimeFormatForSqlCe() {
      var currThread = Thread.CurrentThread;
      var currCulture = currThread.CurrentCulture;
      if (currCulture == _adjustedCulture) return;
      if (_adjustedCulture == null) {
        _adjustedCulture = currCulture.Clone() as CultureInfo;
        var fmtInfo = _adjustedCulture.DateTimeFormat;
        fmtInfo.ShortDatePattern = "yyyy-MM-dd";
        fmtInfo.LongTimePattern = "HH:mm:ss.fffffff";
      } 
      currThread.CurrentCulture = _adjustedCulture;
    }

    //Use this method in LINQ queries when using constant DateTimeOffset value, when your target database is SQL CE
    public static DateTimeOffset AdjustDateTimeOffsetForLinq(DateTimeOffset value) {
      AdjustDefaultDateTimeFormatForSqlCe(); 
      return value.Subtract(value.Offset);
    }

  }//class
}
