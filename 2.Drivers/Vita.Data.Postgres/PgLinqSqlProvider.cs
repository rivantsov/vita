using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Linq.Translation.SqlGen;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Common;
using Vita.Entities.Linq;
using System.Collections;

namespace Vita.Data.Postgres {
  public class PgLinqSqlProvider : LinqSqlProvider {
    public PgLinqSqlProvider(DbModel dbModel) : base(dbModel) {
    }

    public override SqlStatement ReviewSelectSql(SelectExpression select, SqlStatement sql) {
      const string ReadLockTemplate = "{0} \r\n FOR SHARE;";
      const string WriteLockTemplate = "{0} \r\n FOR UPDATE;";
      var flags = select.CommandInfo.Flags;
      if (flags.IsSet(LinqCommandFlags.ReadLock))
        return string.Format(ReadLockTemplate, sql);
      if (flags.IsSet(LinqCommandFlags.WriteLock))
        return string.Format(WriteLockTemplate, sql);
      return sql;
    }


    public override Type GetSqlFunctionResultType(SqlFunctionType functionType, Type[] operandTypes) {
      switch(functionType) {
        case SqlFunctionType.Count: return typeof(long);
      }
      return base.GetSqlFunctionResultType(functionType, operandTypes);
    }

    // Install uuid-ossp extension by simply running (once) the following in the SQL query window: 
    //   CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
    protected override SqlStatement GetNewGuid() {
      return "uuid_generate_v1()";
    }

    protected override SqlStatement GetLiteralDateTimePart(SqlStatement dateExpression, SqlFunctionType operationType) {
      switch (operationType) {
        case SqlFunctionType.Date:
          return SqlStatement.Format("DATE({0})", dateExpression);
        case SqlFunctionType.Time:
          return SqlStatement.Format("DATE_PART('time', {0})", dateExpression);
        case SqlFunctionType.Week:
          return SqlStatement.Format("EXTRACT(WEEK FROM {0})", dateExpression);
        case SqlFunctionType.Year:
          return SqlStatement.Format("EXTRACT(YEAR FROM {0})", dateExpression);
        case SqlFunctionType.Month:
          return SqlStatement.Format("EXTRACT(MONTH FROM {0})", dateExpression);
        case SqlFunctionType.Day:
          return SqlStatement.Format("EXTRACT(DAY FROM {0})", dateExpression);

        default:
          Util.Throw("SQL function {0} not supported.", operationType);
          return null;
      }
    }
    protected override SqlStatement GetLiteralLike(SqlStatement column, SqlStatement pattern, bool forceIgnoreCase) {
      if (forceIgnoreCase) //Use ILIKE
        return SqlStatement.Format("{0} ILIKE {1} ESCAPE '{2}'", column, pattern, Driver.DefaultLikeEscapeChar.ToString());
      else 
        return base.GetLiteralLike(column, pattern, forceIgnoreCase);
    }
    protected override SqlStatement GetLiteralStringEqual(SqlStatement x, SqlStatement y, bool forceIgnoreCase) {
      if (forceIgnoreCase) //Use ILIKE
        return SqlStatement.Format("({0} ILIKE {1} ESCAPE '{2}')", x, y, Driver.DefaultLikeEscapeChar.ToString());
      return base.GetLiteralStringEqual(x, y, forceIgnoreCase);
    }

    protected override SqlStatement GetLiteralInArray(SqlStatement a, SqlStatement b) {
      return string.Format("{0} = ANY({1})", a, b);
    }

    public override void SetDbParameterValue(System.Data.IDbDataParameter parameter, object value) {
      if (value == null)
        return;
      var type = value.GetType();
      //Check for array of enums - should be converted to array of ints
      Type elemType;
      // quick check for array or gen type, then deeper check for list of db primitives
      if ((type.IsArray || type.IsGenericType) && type.IsListOfDbPrimitive(out elemType) && elemType.IsEnum) {
        parameter.Value = ConvertToInts(value as ICollection);
        return;
      }
      base.SetDbParameterValue(parameter, value);
    }
    private IList<int> ConvertToInts(ICollection list) {
      var result = new List<int>();;
      foreach(var v in list)
        result.Add((int)v);
      return result; 
    }
  }//class

}
