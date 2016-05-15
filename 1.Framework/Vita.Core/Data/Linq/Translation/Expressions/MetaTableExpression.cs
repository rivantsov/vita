
using System;
using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Common;

namespace Vita.Data.Linq.Translation.Expressions {
    
    /// <summary>
    /// A MetaTableExpression contains property/constructor parameter mapping for New expressions resulting in new virtual tables
    /// </summary>
    public class MetaTableExpression : SqlExpression  {
      public NewExpression SourceExpression; 
        //Order is important, that's why we have 2 arrays instead of one dictionary
      public IList<SqlExpression> Values; 

        public SqlExpression GetMappedExpression(MemberInfo memberInfo) {
          Util.Check(SourceExpression.Members != null, "Cannot map member {0} from initialization expression in custom type {1}.",
            memberInfo.Name, SourceExpression.Type);
          var index = SourceExpression.Members.IndexOf(memberInfo);
          Util.Check(index >= 0, "Member {0} not found in metatable.", memberInfo.Name);
          return Values[index];
        }

        public MetaTableExpression(NewExpression newExpression, IList<SqlExpression> values) : base(SqlExpressionType.MetaTable, newExpression.Type)
        {
          SourceExpression = newExpression;
          Values = values;
          foreach(var v in Values)
            Operands.Add(v); 
        }

        //RI: adding this to handle conversion to final 'ObjectReader' delegate
        public NewExpression ConvertToNew(IList<Expression> newOperands) {
          var newValues = newOperands.ToArray();
          // we have to use different overloads in each case
          if (SourceExpression.Members == null)
            return Expression.New(SourceExpression.Constructor, newValues); 
          else
            return Expression.New(SourceExpression.Constructor, newValues, SourceExpression.Members); 

        }
    }
}