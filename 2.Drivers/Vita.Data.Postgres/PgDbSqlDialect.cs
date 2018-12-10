﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Vita.Data.Driver;
using Vita.Data.Driver.TypeSystem;
using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.SqlGen;

namespace Vita.Data.Postgres {
  public class PgDbSqlDialect : DbSqlDialect {
    public SqlFragment SqlLockForUpdate = new TextSqlFragment(" FOR UPDATE");
    public SqlFragment SqlLockInShareMode = new TextSqlFragment("  FOR SHARE");
    public SqlTemplate SqlOffsetTemplate = new SqlTemplate("OFFSET {0} ");
    public SqlTemplate SqlLimitTemplate = new SqlTemplate("LIMIT {0} ");

    public SqlTemplate SqlCrudTemplateReturningIdentity = new SqlTemplate(@" RETURNING {0};");


    public PgDbSqlDialect(PgDbDriver driver) : base(driver) {
      base.MaxParamCount = 32000; //reported also 65K
      base.DynamicSqlParameterPrefix = "@P"; 
      base.BatchBeginTransaction = new TextSqlFragment("START TRANSACTION;");
      base.BatchCommitTransaction = new TextSqlFragment("COMMIT;");
      // we are using ANY instead of IN, works for parameters and list literals
      base.SqlCrudTemplateDeleteMany = new SqlTemplate(
      @"DELETE FROM {0} 
    WHERE {1} = ANY({2})");
    }

    public override void InitTemplates() {
      base.InitTemplates();

      AddTemplate("UPPER({0})", SqlFunctionType.ToUpper);
      AddTemplate("LOWER({0})", SqlFunctionType.ToLower);
      AddTemplate("LENGTH({0})", SqlFunctionType.StringLength);


      AddTemplate("DATE({0})", SqlFunctionType.Date);
      AddTemplate("DATE_PART('time', {0})", SqlFunctionType.Time);
      AddTemplate("EXTRACT(WEEK FROM {0})", SqlFunctionType.Week);

      AddTemplate("EXTRACT(YEAR FROM {0})", SqlFunctionType.Year);
      AddTemplate("EXTRACT(MONTH FROM {0})", SqlFunctionType.Month);
      AddTemplate("EXTRACT(DAY FROM {0})", SqlFunctionType.Day);

      //sequence name in double quotes inside single-quote argument
      AddTemplate("nextval('{0}')", SqlFunctionType.SequenceNextValue);
      AddTemplate("uuid_generate_v1()", SqlFunctionType.NewGuid);
      AddTemplate("char_length({0})", SqlFunctionType.StringLength);

      //we use ANY function for IN operator, it works both for parameter or literal list
      AddTemplate("{0} = ANY({1})", SqlFunctionType.InArray);
    }

    // schema should not be quoted
    public override string FormatFullName(string schema, string name) {
      return schema + "." + base.LeftSafeQuote + name + base.RightSafeQuote;
    }

    SqlTemplate SqlTemplateLikeIgnoreCase = new SqlTemplate("{0} ILIKE {1} ESCAPE '\\'");
    SqlTemplate SqlTemplateStringEqualIgnoreCase = new SqlTemplate("({0} ILIKE {1} ESCAPE '\\')");

    public override SqlTemplate GetSqlFunctionTemplate(SqlFunctionExpression expr) {
      switch(expr.FunctionType) {
        case SqlFunctionType.Like:
          if(expr.ForceIgnoreCase)
            return SqlTemplateLikeIgnoreCase;
          break; //use default like
        case SqlFunctionType.StringEqual:
          if(expr.ForceIgnoreCase)
            return SqlTemplateStringEqualIgnoreCase;
          break;
      }//switch
      return base.GetSqlFunctionTemplate(expr);
    }


    public override string ListToLiteral(object value, DbTypeDef elemTypeDef) {
      var list = value as IList;
      if(list.Count == 0)
        return GetEmptyListLiteral(elemTypeDef);
      var strList = new List<string>(list.Count);
      foreach(var item in list) {
        strList.Add(elemTypeDef.ToLiteral(item));
      }
      return "ARRAY[" + string.Join(", ", strList) + "]";
    }

    public override string GetEmptyListLiteral(DbTypeDef elemTypeDef) {
      var pgDbType = elemTypeDef.Name;
      var emptyList = string.Format("SELECT CAST(NULL AS {0}) WHERE 1=0", pgDbType); 
      return emptyList;
    }

    public override IDbDataParameter AddDbParameter(IDbCommand command, SqlPlaceHolder ph, object value) {
      var prm = (NpgsqlParameter) base.AddDbParameter(command, ph, value);
      switch(ph) {
        case SqlListParamPlaceHolder lph:
          // For array parameters PG requires setting NpgsqlDbType to combination of Array and pg-dbType for element
          var pgDbType = (NpgsqlDbType)lph.ElementTypeDef.ProviderDbType; 
          prm.NpgsqlDbType = NpgsqlDbType.Array | pgDbType;
          break; 
      }
      return prm; 
    }

  }
}
