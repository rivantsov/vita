using System;
using System.Collections.Generic;
using System.Text;

namespace Vita.Data.Sql {

  public static class SqlTerms  {
    public static TextSqlFragment LeftParenthesis = new TextSqlFragment("(");
    public static TextSqlFragment RightParenthesis = new TextSqlFragment(")");

    public static  TextSqlFragment InnerJoin = new TextSqlFragment(" INNER JOIN ");
    public static  TextSqlFragment LeftJoin = new TextSqlFragment(" LEFT JOIN ");
    public static  TextSqlFragment RightJoin = new TextSqlFragment(" RIGHT JOIN ");
    public static  TextSqlFragment Select = new TextSqlFragment("SELECT ");
    public static  TextSqlFragment From = new TextSqlFragment("FROM ");
    public static  TextSqlFragment Where = new TextSqlFragment("WHERE ");
    public static  TextSqlFragment OrderBy = new TextSqlFragment("ORDER BY ");
    public static  TextSqlFragment GroupBy = new TextSqlFragment("GROUP BY ");
    public static  TextSqlFragment Having = new TextSqlFragment("HAVING ");
    public static  TextSqlFragment Distinct = new TextSqlFragment("DISTINCT ");
    public static  TextSqlFragment Desc = new TextSqlFragment(" DESC");
    public static  TextSqlFragment On = new TextSqlFragment(" ON ");
    public static  TextSqlFragment As = new TextSqlFragment(" AS ");

    public static  TextSqlFragment Comma = new TextSqlFragment(", ");
    public static TextSqlFragment CommaNewLineIndent = new TextSqlFragment("," + Environment.NewLine + "    "); //used in Insert-many
    public static  TextSqlFragment Dot = new TextSqlFragment(".");
    public static TextSqlFragment Semicolon = new TextSqlFragment(";");
    public static TextSqlFragment Space = new TextSqlFragment(" ");
    public static  TextSqlFragment NewLine = new TextSqlFragment(Environment.NewLine);
    public static TextSqlFragment Indent = new TextSqlFragment("    ");
    public static TextSqlFragment  Zero = new TextSqlFragment("0");
    public static TextSqlFragment  One = new TextSqlFragment("1");
    public static TextSqlFragment  Empty = new TextSqlFragment(string.Empty);
    public static TextSqlFragment  Null = new TextSqlFragment("NULL");
    public static TextSqlFragment And = new TextSqlFragment(" AND ");
    public static TextSqlFragment Equal = new TextSqlFragment(" = ");
    public static TextSqlFragment Star = new TextSqlFragment("*");


  }
}
