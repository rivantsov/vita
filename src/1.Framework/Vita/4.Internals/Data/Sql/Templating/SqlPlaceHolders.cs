﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Driver.TypeSystem;
using Vita.Data.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Sql {

  // base placeholder, also used as positional placeholder in Sql templates
  public  class SqlPlaceHolder : SqlFragment, IFlatSqlFragment {
    public int Index;
    public ParameterDirection ParamDirection = ParameterDirection.Input;
    public Func<object, string> FormatLiteral;
    public Func<IDbDataParameter, string> FormatParameter;
    public Action<IDbDataParameter, SqlPlaceHolder> PreviewParameter;


    public SqlPlaceHolder(int index = -1) {
      Index = index; 
    }

    public void AddFormatted(IList<string> strings, IList<string> placeHolderArgs) {
      strings.Add(placeHolderArgs[this.Index]);
    }
    public override void Flatten(IList<IFlatSqlFragment> flatList, ISqlPrecedenceHandler precedenceHandler) {
      flatList.Add(this); 
    }
  }

  [DebuggerDisplay("?{Column.ColumnName}?")]
  public class SqlColumnValuePlaceHolder : SqlPlaceHolder {
    public DbColumnInfo Column;

    public SqlColumnValuePlaceHolder(DbColumnInfo column, ParameterDirection direction = ParameterDirection.Input){
      Column = column;
      ParamDirection = direction;
      base.FormatLiteral = column.TypeInfo.TypeDef.ToLiteral;
    }
  }

  public class SqlLinqParamPlaceHolder : SqlPlaceHolder {
    public Type DataType;
    public Func<object[], object> ValueReader;
    public Func<object, object> ValueToDbValue;

    public SqlLinqParamPlaceHolder(Type dataType, Func<object[], object> valueReader, Func<object, object> valueToDbValue, 
          Func<object, string> formatLiteral)  {
      DataType = dataType;
      ValueReader = valueReader;
      ValueToDbValue = valueToDbValue;
      FormatLiteral = formatLiteral;
    }
  }

  public class SqlListParamPlaceHolder : SqlPlaceHolder {
    public Type ElementType;
    public DbTypeDef ElementTypeDef; 
    public Func<object[], object> ListValueReader;

    public SqlListParamPlaceHolder(Type elementType, DbTypeDef elemTypeDef, Func<object[], object> valueReader, 
                                               Func<object, string> formatLiteral, Func<IDbDataParameter, string> formatParameter = null) {
      ElementType = elementType;
      ElementTypeDef = elemTypeDef;
      ListValueReader = valueReader;
      base.FormatLiteral = formatLiteral;
      base.FormatParameter = formatParameter ?? (p => p.ParameterName);
    }
  }

  public class SqlPlaceHolderList: List<SqlPlaceHolder> {
    public new void Add(SqlPlaceHolder ph) {
      if (ph.Index == -1)
        ph.Index = this.Count;
      base.Add(ph); 
    }

    public void Reindex() {
      for(int i = 0; i < Count; i++)
        this[i].Index = i; 
    }
  }


  
}
