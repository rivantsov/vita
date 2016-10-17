using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Entities;

namespace Vita.Modules.Calendar.Cron {

  public class CronParsingException : OperationAbortException {
    public CronParsingException(string message, Exception inner = null) : base(message, "InvalidCronSpec", inner) { }
  }

  public static class CronSpecParser {

    public static IntCronField ParseIntField(CronFieldType type, string spec) {
      try {
        var dateField = GetDateField(type);
        var nameLists = GetNameLists(type);
        if(spec == "*")
          return new IntCronField(type, CronFieldKind.AnyValue, dateField, spec);
        if(spec.Contains(',')) {
          var values = spec.Split(',').Select(s => ParseValue(s, nameLists)).ToArray();
          return new IntCronField(type, CronFieldKind.List, dateField, spec, values);
        }
        if(spec.Contains('-')) {
          var values = spec.Split('-').Select(s => ParseValue(s, nameLists)).ToArray();
          Check(values.Length == 2, "Invalid cron list spec: '{0}', expected only 2 values separated by dash.", spec);
          Check(values[0] < values[1], "Invalid cron range spec: '{0}', first item must less than the second.", spec);
          return new IntCronField(type, CronFieldKind.Range, dateField, spec, values[0], values[1]);
        }
        if (spec.Contains("/")) {
          var parts = spec.Split('/');
          Check(parts.Length == 2, "Invalid '/' CRON field: " + spec);
          var p0 = parts[0];
          Check(p0 == string.Empty || p0 == "*", "Invalid '/' CRON field, expected * as first symbol: " + spec);
          int div;
          Check(int.TryParse(parts[1], out div), "Invalid CRON field, expected int after / :" + spec);
          Check(div > 1 && div < 30, "Invalid divisor value in / CRON field, expected 2..30 :" + spec);
          var values = GetListForDivisor(type, div);
          return new IntCronField(type, CronFieldKind.List, dateField, spec, values);
        }
        var value = ParseValue(spec, nameLists);
        return new IntCronField(type, CronFieldKind.Value, dateField, spec, value);
      } catch(Exception ex) {
        ex.Data["CronField"] = spec;
        ex.Data["CronFieldType"] = type;
        throw; 
      }
    }//method

    // Handles 'W' symbol in day field - shifts to nearest workday, without crossing month boundary
    // Extensions: W> - shift to first later workday, getting to another month is OK
    //             W< - shift to earlier workday, crossing month boundary is OK
    public static IntCronField ParseDayField(string spec, out WeekDayShiftMode mode) {
      mode = WeekDayShiftMode.None;
      if (spec.Contains('W')) {
        var parts = spec.Split('W');
        switch(parts[1]) {
          // Standard case - only W
          case "": mode = WeekDayShiftMode.Nearest; break;
          //Extensions 
          case ">": mode = WeekDayShiftMode.Forward; break;
          case "<": mode = WeekDayShiftMode.Back; break; 
        }
        return ParseIntField(CronFieldType.Day, parts[0]);
      } else 
        return ParseIntField(CronFieldType.Day, spec);
    }

    public static DayOfWeekCronField ParseDayOfWeek(string spec) {
      // we parse as int, then create specialized field from its props
      IntCronField intField; 
      int dayNum = -1;
      string errorPrefix = "Invalid CRON day-of-week field, ";
      if (spec.Contains('#')) {
        var parts = spec.Split('#');
        Check(parts.Length == 2, errorPrefix + " expected single #: " + spec);
        Check(int.TryParse(parts[1], out dayNum) && dayNum > 0 && dayNum <= 5, 
                  errorPrefix + "expected number 1..5 after #: " + spec);
        intField = ParseIntField(CronFieldType.DayOfWeek, parts[0]);
        Check(intField.Kind == CronFieldKind.Value, errorPrefix + "expected single day before #: " + spec);
      } else {
        intField = ParseIntField(CronFieldType.DayOfWeek, spec);
      }
      //Create actual field
      return new DayOfWeekCronField(intField.Kind, spec, dayNum, intField.Values);
    }

    private static int[] GetListForDivisor(CronFieldType type, int div) {
      switch(type) {
        case CronFieldType.Day: return GetDivisibles(1, 31, div); 
        case CronFieldType.Hours: return GetDivisibles(0, 23, div); 
        case CronFieldType.Minutes: return GetDivisibles(0, 59, div); 
        case CronFieldType.Month: return GetDivisibles(1, 12, div);
        case CronFieldType.Year: return GetDivisibles(2001, 2100, div);
        default:
          Check(false, "Invalid CRON field, / is not allowed for field type " + type);
          return null; //never happens
      }
    }

    private static int[] GetDivisibles(int from, int until, int div) { 
      var list = new List<int>();
      for(int i = from; i <= until; i++)
        if(i % div == 0)
          list.Add(i);
      return list.ToArray();
    }

    private static int ParseValue(string str, string[][] nameLists) {
      int result;
      if(char.IsDigit(str[0]))
        if(int.TryParse(str, out result))
          return result;
        else
          throw new CronParsingException("Invalid CRON field: " + str);
      // not digit, try parsing name
      Check (nameLists != null && nameLists.Length > 0, "Invalid CRON field: " + str + ", expected digit.");
      str = str.ToLower(); 
      foreach(var list in nameLists) {
        var index = Array.IndexOf(list, str);
        if(index >= 0)
          return index; 
      }
      Check(false, "Invalid CRON field: " + str + ", unknown day or month name.");
      return -1; // never happens
    }

    static string[] _dayNames = new[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
    static string[] _dayNamesShort = new[] { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };
    // months are numbered starting with 1, so we inject extra name to adjust result of IndexOf
    static string[] _monthNames = new[] {"/not-a-month/", "january", "february", "march", "april", "may", "june",
        "july", "august", "september", "october", "november", "december" };
    static string[] _monthNamesShort = new[] {"/not-a-month/", "jan", "feb", "mar", "apr", "may", "jun",
        "jul", "aug", "sep", "oct", "nov", "dec" };

    static string[][] GetNameLists(CronFieldType type) {
      switch(type) {
        case CronFieldType.DayOfWeek: return new[] { _dayNames, _dayNamesShort };
        case CronFieldType.Month: return new[] { _monthNames, _monthNamesShort };
        default: return null; 
      }
    }

    static DateField[] _dateFieldsByCronFieldType;
    public static DateField GetDateField(CronFieldType cronType) {
      _dateFieldsByCronFieldType = _dateFieldsByCronFieldType ??
           new DateField[] { DateFields.Year, DateFields.Month, DateFields.Day, DateFields.Day,
              DateFields.Hour, DateFields.Minute };
      return _dateFieldsByCronFieldType[(int)cronType];
    }

    public static void Check(bool condition, string message, params object[] args) {
      if(condition)
        return;
      message = StringHelper.SafeFormat(message, args);
      throw new CronParsingException(message); 
    }

  }//class
}
