using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Common;
using Vita.Data.Linq.Translation.SqlGen;
using System.Data;

namespace Vita.Data.SQLite {
  public class SQLiteLinqSqlProvider : LinqSqlProvider {
    public SQLiteLinqSqlProvider(DbModel dbModel)  : base(dbModel) {

    }

    public override SelectExpression PreviewSelect(SelectExpression e) {
      return base.PreviewSelect(e);
    }

    public override Type GetSqlFunctionResultType(SqlFunctionType functionType, Type[] operandTypes) {
      switch(functionType) {
        case SqlFunctionType.Count: return typeof(long); 
      }
      return base.GetSqlFunctionResultType(functionType, operandTypes);
    }

    public override Linq.Translation.SqlGen.SqlStatement GetOrderByColumn(Linq.Translation.SqlGen.SqlStatement expression, bool descending) {
      string result = string.Format("{0} COLLATE NOCASE", expression);
      if(descending)
        result += " DESC";
      return result; 
    }

    public override Linq.Translation.SqlGen.SqlStatement GetLiteral(bool literal) {
      return literal ? "1" : "0";
    }

    protected override SqlStatement GetLiteralStringConcat(SqlStatement a, SqlStatement b) {
      return SqlStatement.Format("{0} || {1}", a, b);
    }


    public override Linq.Translation.SqlGen.SqlStatement GetLiteral(Guid literal) {
      var bytes = literal.ToByteArray();
      var hex = HexUtil.ByteArrayToHex(bytes);
      var result = "X'" + hex + "'";
      return result; 
    }

    protected override SqlStatement GetLiteralStringLength(SqlStatement a) {
      return SqlStatement.Format("Length({0})", a);
    }

    protected override SqlStatement GetLiteralDateTimePart(SqlStatement dateExpression, SqlFunctionType operationType) {
      Util.Throw("SQL function {0} not supported.", operationType);
      return null; 
    }
    protected override SqlStatement GetLiteralStringEqual(SqlStatement x, SqlStatement y, bool forceIgnoreCase) {
      if (forceIgnoreCase) //Use ILIKE
        return SqlStatement.Format("({0} LIKE {1} ESCAPE '{2}')", x, y, Driver.DefaultLikeEscapeChar.ToString());
      return base.GetLiteralStringEqual(x, y, forceIgnoreCase);
    }


    //Datetime is a special case - we need to convert to string properly

    public override SqlStatement GetLiteral(DateTime literal) {
      return "'" + SQLiteTypeRegistry.DateTimeToString(literal) + "'";
    }


    /*
    public override void CheckQueryParameter(ExternalValueExpression parameter) {
      base.CheckQueryParameter(parameter);
    }
    public override void SetDbParameterValue(IDbDataParameter parameter, Type type, object value) {

      if(value != null && (type == typeof(DateTime) || type == typeof(DateTime?))) {

        var dtq = value as DateTime?;
        var dt = dtq == null ? (DateTime)value : dtq.Value; 
        parameter.Value = SQLiteTypeRegistry.DateTimeToString(dt);

//        parameter.Value = SQLiteTypeRegistry.DateTimeToString(value);
    } else

        base.SetDbParameterValue(parameter, type, value);
    }
    */
  }
}
