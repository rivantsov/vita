
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Vita.Entities;
using Vita.Entities.Utilities;

namespace Vita.Data.Linq.Translation.Expressions {

  /// <summary>Special Expression types for Sql expressions. </summary>
  public enum SqlExpressionType {
    Select,  
    MetaTable,
    Table,
    SubSelect,
    Column,
    ExternalValue, // Query parameter or value derived from it
    OrderBy,
    Group,
    Alias, 
    NonQueryCommand, // for update statements
    TableFilter, //List filter
    SqlFunction, //SQL function or operator
    Aggregate,
  }
  /// <summary>SQL specific custom expression types. </summary>
  public enum SqlFunctionType {
    IsNull = 100,
    IsNotNull,
    EqualNullables,
    Concat,
    Exists,
    Like,
    Iif,

    StringLength,
    ToUpper,
    ToLower,
    In, //In SubSelect
    InArray, // 'x IN (1, 3, 3)' or 'x IN (@prm)' where prm is array-type parameter
    Substring,
    Trim,
    LTrim,
    RTrim,

    StringEqual,

    Year,
    Month,
    Day,
    Hour,
    Minute,
    Second,
    Millisecond,
    Now,
    Date,
    DateDiffInMilliseconds,
    Week,
    Time,

    Abs,
    Exp,
    Floor,
    Ln,
    Log,
    Round,
    Sign,
    Sqrt,

    AndBitwise,
    OrBitwise,
    XorBitwise,
    ConvertBoolToBit,
    NewGuid,

    SequenceNextValue,
  }

  public enum AggregateType {
    Count,
    Min,
    Max,
    Sum,
    Average,
  }

  [Flags]
  public enum SelectExpressionFlags {
    None = 0,
    Distinct = 1,
    MsSqlUseTop = 1 << 4,
    NeedsFakeOrderBy = 1 << 5 
  }


  public static class LinqExpressionExtensions {
    public static bool IsSet(this SelectExpressionFlags flags, SelectExpressionFlags flag) {
      return (flags & flag) != 0;
    }

  }

}