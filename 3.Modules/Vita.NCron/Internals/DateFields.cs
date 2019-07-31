using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vita.NCron.Internals {

  public enum DateFieldType {
    Year,
    Month,
    Day,
    Hour,
    Minute,
  }

  public static class DateFields {
    public static IntField Year;
    public static IntField Month;
    public static DayField Day;
    public static IntField Hour;
    public static IntField Minute;
    public static DateField[] All;

    static DateFields() {
      Minute = new IntField(DateFieldType.Minute, 0, 59);
      Hour = new IntField(DateFieldType.Hour, 0, 23);
      Day = new DayField();
      Month = new IntField(DateFieldType.Month, 1, 12);
      Year = new IntField(DateFieldType.Year, 2000, 2100);
      All = new DateField[] { Year, Month, Day, Hour, Minute };
    }

    public static DateField Next(this DateField field) {
      if(field.Index == All.Length - 1)
        return null;
      return All[field.Index + 1];
    }

  }//class

  public abstract class DateField {
    public DateFieldType Type;
    public DateField(DateFieldType type) {
      Type = type;
    }

    public int Index {
      get { return (int)Type; }
    }
    public override string ToString() {
      return Type.ToString();
    }

    public abstract int GetMax(DateValue date);

    public virtual int GetMin() {
      switch(Type) {
        case DateFieldType.Minute:
        case DateFieldType.Hour:
          return 0;
        default:
          return 1;
      }
    }

    public void Reset(DateValue date) {
      date[Type] = GetMin();
    }

    public bool TryIncrement(DateValue date) {
      var max = GetMax(date);
      var newValue = date[Type] + 1;
      if(newValue > max)
        return false;
      date[Type] = newValue;
      return true;
    }
  } //DateField

  public class IntField : DateField {
    public int Min;
    public int Max;
    public IntField(DateFieldType type, int min, int max) : base(type) {
      Min = min;
      Max = max;
    }
    public override int GetMax(DateValue date) {
      return Max;
    }
  }

  public class DayField : DateField {
    public DayField() : base(DateFieldType.Day) { }
    public override int GetMax(DateValue date) {
      return date.GetDaysInMonth();
    }
  }

}//ns
