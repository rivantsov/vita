using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

using Vita.Data.Linq.Translation.Expressions;

namespace Vita.Data.SqlGen {

  public interface ISqlPrecedenceHandler {
    int GetPrecedence(ExpressionType type);
    int GetPrecedence(SqlFunctionType type);
    bool NeedsParenthesis(SqlFragment parent, SqlFragment child, bool isFirst);
  }
}
