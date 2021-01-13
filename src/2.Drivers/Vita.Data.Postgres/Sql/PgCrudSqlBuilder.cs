using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using Vita.Data.Driver;
using Vita.Data.Linq;
using Vita.Data.Model;
using Vita.Data.Sql;
using Vita.Entities.Model;
using Vita.Entities.Runtime;

namespace Vita.Data.Postgres {
  public class PgCrudSqlBuilder : DbCrudSqlBuilder {
    PgSqlDialect _pgDialect;

    public PgCrudSqlBuilder(DbModel dbModel): base(dbModel) {
      _pgDialect = (PgSqlDialect)dbModel.Driver.SqlDialect;
    }

    // ------------- Notes on identity return ------------------------
    // it sounds logical to use 'Returning id INTO @P', but this does not work
    // SqlTemplate SqlTemplateReturnIdentity = new SqlTemplate(" RETURNING {0} INTO {1};");
    // Note: using @p = LastVal(); -does not work, gives error: Commands with multiple queries cannot have out parameters
    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags;
      if(flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table);
      return sql;
    }

    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      sql.TrimEndingSemicolon();
      // Append returning clause
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var dbType = idCol.Member.DataType.GetIntDbType();
      // we create placeholder based on Id column, only with OUTPUT direction - this results in parameter to return value
      var idPrmPh = new SqlColumnValuePlaceHolder(idCol, ParameterDirection.Output);
      sql.PlaceHolders.Add(idPrmPh);
      var getIdSql = _pgDialect.SqlCrudTemplateReturningIdentity.Format(idCol.SqlColumnNameQuoted);
      sql.Append(getIdSql);
      sql.Append(SqlTerms.NewLine);
    }


  } //class
}
