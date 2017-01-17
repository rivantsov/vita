using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.JobExecution.Cron {

  public class DateValue {
    public int[] FieldValues; 
    public DateValue(DateTime date) {
      FieldValues = new[] { date.Year, date.Month, date.Day, date.Hour, date.Minute };
    }

    public int this[DateFieldType type] {
      get { return FieldValues[(int)type]; }
      set { FieldValues[(int)type] = value; }
    }

    public DateTime GetDate() {
      return new DateTime(FieldValues[0], FieldValues[1], FieldValues[2], FieldValues[3], FieldValues[4], 0);
    }

    public override string ToString() {
      return string.Format("{0}/{1}/{2} {3}:{4}", 
        FieldValues[0], FieldValues[1], FieldValues[2], FieldValues[3], FieldValues[4]);
    }

    public int GetDaysInMonth() {
      var d1 = new DateTime(FieldValues[0], FieldValues[1], 1); //first day of month
      var dlast = d1.AddMonths(1).AddDays(-1); //last day of month
      return dlast.Day; 
    }
  }//class

}
