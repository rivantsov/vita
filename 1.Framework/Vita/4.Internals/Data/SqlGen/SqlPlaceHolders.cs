using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.SqlGen {

  public class SqlPlaceHolder : SqlFragment, IFlatSqlFragment {
    public int Index = -1;

    public SqlPlaceHolder() { }

    public void AddFormatted(IList<string> strings, IList<string> placeHolderArgs) {
      strings.Add(placeHolderArgs[this.Index]);
    }

    public override void Flatten(IList<IFlatSqlFragment> flatList, ISqlPrecedenceHandler precedenceHandler) {
      flatList.Add(this);
    }
    public override string ToString() {
      return "{" + Index + "}"; 
    }
  }

  // ValueRef placeholder is used in InsertMany SQL for all column values of inserted rows
  [DebuggerDisplay("{Value}")]
  public class SqlValueRefPlaceHolder : SqlPlaceHolder {
    public DbStorageType TypeDef;
    public object Value;
    public SqlValueRefPlaceHolder(DbStorageType typeDef, object value) {
      TypeDef = typeDef;
      Value = value; 
    }
  }

  [DebuggerDisplay("{SourceColumn.ColumnName}")]
  public class SqlColumnRefPlaceHolder   : SqlPlaceHolder {
    public DbColumnInfo SourceColumn; 
    public ParameterDirection Direction;
    public SqlColumnRefPlaceHolder(DbColumnInfo sourceColumn) {
      SourceColumn = sourceColumn; 
    }
  }

  [DebuggerDisplay("@P({typeInfo.SqlTypeSpec})")]
  public class SqlParamPlaceHolder : SqlPlaceHolder {
    public DbStorageType TypeDef; 
    public ParameterDirection Direction;
    // null most of the time; assigned for Output parameters returning identity or Row version
    public DbColumnInfo TargetColumn;

    public SqlParamPlaceHolder(DbStorageType typeDef, ParameterDirection direction = ParameterDirection.Input, 
                                 DbColumnInfo targetColumn = null) {
      TypeDef = typeDef; 
      Direction = direction;
      TargetColumn = targetColumn; 
    }

  }

  public class SqlLinqParamPlaceHolder : SqlPlaceHolder {
    public Type DataType;
    public DbStorageType TypeDef; 
    public Func<object[], object> ReadValue;
    public SqlLinqParamPlaceHolder(DbStorageType typeDef, Func<object[], object> readValue) {
      TypeDef = typeDef;
      ReadValue = readValue;
    }
  }


  public class SqlArrayValuePlaceHolder : SqlPlaceHolder {
    public Type ArrayElementType;
    public Func<object[], object> ReadValue;

    public SqlArrayValuePlaceHolder(Type arrayElementType, Func<object[], object> readValue) {
      ArrayElementType = arrayElementType;
      ReadValue = readValue;
    }
  }




}
