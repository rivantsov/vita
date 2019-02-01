using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Oracle {
  public class OracleCrudSqlBuilder : DbCrudSqlBuilder {

    public OracleCrudSqlBuilder(DbModel dbModel) : base(dbModel) {
    }

    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags;
      if(flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table);
      return sql;
    }

    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      sql.TrimEndingSemicolon(); // somewhat a hack
      // Append returning clause
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var dbType = idCol.Member.DataType.GetIntDbType();
      // we create placeholder based on Id column, only with OUTPUT direction - this results in parameter to return value
      var idPrmPh = new SqlColumnValuePlaceHolder(idCol, ParameterDirection.Output);
      sql.PlaceHolders.Add(idPrmPh);
      var oracleDialect = (OracleSqlDialect)base.SqlDialect;
      var getIdSql = oracleDialect.SqlReturnIdentityTemplate.Format(idCol.SqlColumnNameQuoted, idPrmPh);
      sql.Append(getIdSql);
      sql.Append(SqlTerms.NewLine);
    }


  }//class

}
