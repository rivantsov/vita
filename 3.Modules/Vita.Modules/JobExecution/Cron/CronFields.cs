using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.JobExecution.Cron {

  public enum CronFieldType {
    Year,
    Month,
    Day,
    DayOfWeek,
    Hours,
    Minutes,
  }

  public enum CronFieldKind {
    AnyValue, 
    Value,
    Range,
    List,
  }

  public abstract class CronField {
    public CronFieldType Type;
    public CronFieldKind Kind; 
    public DateField DateField;
    public string Spec;
    public CronField(CronFieldType type, CronFieldKind kind, DateField dateField, string spec) {
      Type = type;
      Kind = kind;
      DateField = dateField;
      Spec = spec;
    }
    public override string ToString() {
      return Type.ToString() + ":" + Spec;
    }

    public virtual int GetFieldValue(DateValue date) {
      return date[DateField.Type];
    }

    public abstract bool Matches(DateValue date);
  }//class

  public class IntCronField: CronField {
    public int[] Values;
    bool _matchLastDay;
    public IntCronField(CronFieldType type, CronFieldKind kind, DateField dateField, string spec, params int[] values) 
         : base(type, kind, dateField, spec) {
      Values = values;
      // For day field 31 matches last day of the month
      _matchLastDay = type == CronFieldType.Day && kind != CronFieldKind.AnyValue && values.Contains(31); 
    }
    public override bool Matches(DateValue date) {
      var value = GetFieldValue(date);
      if(_matchLastDay && value == DateField.GetMax(date))
        return true; 
      switch(Kind) {
        case CronFieldKind.AnyValue: return true;
        case CronFieldKind.Value:
          return value == this.Values[0];
        case CronFieldKind.Range: return value >= this.Values[0] && value <= this.Values[1];
        case CronFieldKind.List: return Values.Contains(value);
        default:
          return false; 
      }
    } //method

  }//class 

  public class DayOfWeekCronField: IntCronField {
    public int DayNum; // number after # in spec: 'Fri#3'; -1 if not specified
    public DayOfWeekCronField(CronFieldKind kind, string spec, int dayNum, int[] values) 
        : base(CronFieldType.DayOfWeek, kind, DateFields.Day, spec, VerifyValues(values)) {
      DayNum = dayNum;
    }

    private static int[] VerifyValues(int[] values) {
      return values.Select(v => v % 7).OrderBy(v => v).ToArray(); 
    }

    public override int GetFieldValue(DateValue date) {
      return (int) date.GetDate().DayOfWeek;
    }

    public override bool Matches(DateValue date) {
      var dayMatches = base.Matches(date);
      if(!dayMatches || DayNum == -1)
        return dayMatches;
      //We have spec like Fri#3, and dayOfWeek already matches - it is Fri
      // check that DayNum matches, that it is Fri #3
      var day = date[DateFieldType.Day];
      var thisDayNum = (day - 1) / 7 + 1; //Number of Fri's before plus this one
      if(thisDayNum == DayNum)
        return true;
      // one special case: Fri#5 matches LAST Fri (#4) if there are only 4
      if(DayNum != 5)
        return false;
      // so this must be Fri#4 to match this special case
      if(thisDayNum < 4) //if 
        return false;
      //We have special case, let's check how many days left in month - if there's one more Fri
      var daysInMonth = date.GetDaysInMonth();
      if(daysInMonth - day < 7) //if there's less than whole week after this Fri, it is last Fri!
        return true;
      //otherwise return false
      return false;  

      
    }
  }//class

}
