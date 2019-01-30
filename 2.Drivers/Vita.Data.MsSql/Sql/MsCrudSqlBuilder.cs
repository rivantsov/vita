using System;
using System.Collections.Generic;
using System.Text;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Runtime;
using Vita.Data.Driver;
using Vita.Data.Model;
using Vita.Data.Sql;
using System.Linq;
using System.Data;

namespace Vita.Data.MsSql {

  public class MsCrudSqlBuilder : DbCrudSqlBuilder {
    MsSqlDialect _msDialect;

    public MsCrudSqlBuilder(DbModel dbModel) : base(dbModel) {
      _msDialect = (MsSqlDialect)dbModel.Driver.SqlDialect;
    }


    public override SqlStatement BuildCrudInsertOne(DbTableInfo table, EntityRecord record) {
      var sql = base.BuildCrudInsertOne(table, record);
      var flags = table.Entity.Flags;
      if(flags.IsSet(EntityFlags.HasIdentity))
        AppendIdentityReturn(sql, table);
      if(flags.IsSet(EntityFlags.HasRowVersion))
        AppendRowVersionCheckReturn(sql, table, record);
      return sql;
    }

    public override SqlStatement BuildCrudUpdateOne(DbTableInfo table, EntityRecord rec) {
      var sql = base.BuildCrudUpdateOne(table, rec);
      if(table.Entity.Flags.IsSet(EntityFlags.HasRowVersion))
        AppendRowVersionCheckReturn(sql, table, rec);
      return sql;
    }

    private void AppendIdentityReturn(SqlStatement sql, DbTableInfo table) {
      var idCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.Identity));
      var dbType = idCol.Member.DataType.GetIntDbType();
      var idPrmPh = new SqlColumnValuePlaceHolder(idCol, ParameterDirection.Output);
      sql.PlaceHolders.Add(idPrmPh);
      var getIdSql = _msDialect.SqlGetIdentityTemplate.Format(idPrmPh);
      sql.Append(getIdSql);
      sql.Append(SqlTerms.NewLine);
    }

    private void AppendRowVersionCheckReturn(SqlStatement sql, DbTableInfo table, EntityRecord record) {
      var rvCol = table.Columns.First(c => c.Flags.IsSet(DbColumnFlags.RowVersion));
      // do row count check for update only, not for insert
      if(record.Status == EntityStatus.Modified) {
        var tag = new TextSqlFragment($"'ConcurrentUpdate/{table.Entity.Name}/{record.PrimaryKey.ValuesToString()}'");
        var checkRowsSql = _msDialect.SqlCheckRowCountIsOne.Format(tag);
        sql.Append(checkRowsSql);
      }
      // return RowVersion in parameter
      var rvPrmPholder = new SqlColumnValuePlaceHolder(rvCol, ParameterDirection.InputOutput);
      sql.PlaceHolders.Add(rvPrmPholder);
      rvPrmPholder.PreviewParameter = (prm, ph) => {
        prm.DbType = DbType.Binary;
        prm.Size = 8;
      };
      var getRvSql = _msDialect.SqlGetRowVersionTemplate.Format(rvPrmPholder);
      sql.Append(getRvSql);
      sql.Append(SqlTerms.NewLine);
    }

  }
}
