using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.Modules.JobExecution.Cron {

  public enum WeekDayShiftMode {
    None,
    Nearest,
    Back,
    Forward,
  }

  public class CronSchedule {
    public readonly WeekDayShiftMode WeekDayShift;
    public CronField[] Fields;
    private CronField[][] _fieldsByDateFieldType; 

    public CronSchedule(string spec) {
      var segms = spec.Trim().Split(' ');
      CronSpecParser.Check(segms.Length == 5 || segms.Length == 6, "Invalid CRON spec: expected 5 or 6 segments, found: {0}.", segms.Length);
      var minuteSpec = segms[0];
      if(minuteSpec == "*")
        minuteSpec = "0";
      var minute = CronSpecParser.ParseIntField(CronFieldType.Minutes, minuteSpec);
      var hour = CronSpecParser.ParseIntField(CronFieldType.Hours, segms[1]);
      var day = CronSpecParser.ParseDayField(segms[2], out WeekDayShift);
      var month = CronSpecParser.ParseIntField(CronFieldType.Month, segms[3]);
      var dayOfWeek = CronSpecParser.ParseDayOfWeek(segms[4]);
      var yearSpec = segms.Length == 6 ? segms[5] : "*";
      var year = CronSpecParser.ParseIntField(CronFieldType.Year, yearSpec);
      Fields = new[] { year, month, dayOfWeek, day, hour, minute };
      _fieldsByDateFieldType = new[] {
        new [] {year}, new[] {month}, new [] {day, dayOfWeek}, new [] {hour}, new [] {minute}
      };
    } //constructor

    public override string ToString() {
      return string.Join<CronField>(" ", Fields);
    }

    public DateTime? TryGetNext(DateTime value) {
      var start = value.AddMinutes(1);
      var date = new DateValue(start);
      if(!TryFindMatch(date))
        return null;
      var result = date.GetDate();
      if(WeekDayShift != WeekDayShiftMode.None)
        result = ShiftToWorkDay(result);
      return result; 
    }

    private DateTime ShiftToWorkDay(DateTime date) {
      var dw = date.DayOfWeek;
      if(dw != DayOfWeek.Saturday && dw != DayOfWeek.Sunday)
        return date; 
      switch(WeekDayShift) {
        case WeekDayShiftMode.None: return date;
        case WeekDayShiftMode.Nearest:
          switch(dw) {
            // Note: we cannot cross month boudnaries
            case DayOfWeek.Saturday:
              return date.Day > 1 ? date.AddDays(-1) /*Fri*/ : date.AddDays(2) /*monday*/;
            case DayOfWeek.Sunday:
              var monday = date.AddDays(1);
              return (monday.Month == date.Month) ? monday : date.AddDays(-2); //shift to Fri)
          }
          break;
        case WeekDayShiftMode.Back:
          switch(dw) {
            case DayOfWeek.Saturday: return date.AddDays(-1);
            case DayOfWeek.Sunday: return date.AddDays(-2);
          }
          break;
        case WeekDayShiftMode.Forward:
          switch(dw) {
            case DayOfWeek.Saturday: return date.AddDays(2);
            case DayOfWeek.Sunday:   return date.AddDays(1);
          }
          break;
      }//switch WeekDayShift
      return date; //never happens
    }//method

    private bool TryFindMatch(DateValue date) {
      return TryMatchAllStartingWith(DateFields.Year, date);
    }

    private bool TryMatchAllStartingWith(DateField startingWith, DateValue date) {
      if(startingWith == null)
        return true;
      while(true) {
        if(TryFindMatch(startingWith, date) && TryMatchAllStartingWith(startingWith.Next(), date))
          return true;
        if(!TryIncrement(startingWith, date))
          return false; //we are out of increments for this level
      }
    }

    private bool TryFindMatch(DateField dateField, DateValue date) {
      var cronFields = _fieldsByDateFieldType[dateField.Index];
      while(true) {
        if(cronFields.All(cf => cf.Matches(date)))
          return true;
        if(!TryIncrement(dateField, date))
          return false; 
      }
    }

    private bool TryIncrement(DateField dateField, DateValue date) {
      if(!dateField.TryIncrement(date))
        return false; 
      // if we incremented, reset all following fields
      for(int i = dateField.Index + 1; i < DateFields.All.Length; i++) 
        DateFields.All[i].Reset(date); 
      return true; 
    }//method
  }//class

}
