
using System;
using System.Diagnostics;
using System.Linq.Expressions;

using Vita.Data.Linq.Translation.Expressions;
using Vita.Data.Model;
using Vita.Entities;
using Vita.Entities.Locking;

namespace Vita.Data.Linq.Translation.Expressions
{
    /// <summary>
    /// A table is a default table, or a joined table
    /// Different joins specify different tables
    /// </summary>
    [DebuggerDisplay("TableExpression {Name} (as {Alias})")]
    public class TableExpression : SqlExpression {
        // Table idenfitication
        public string Name { get; private set; }
        public DbTableInfo TableInfo;
        public LockOptions LockOptions; 
        public int SortIndex; 

        // Join: if this table is related to another, the following informations are filled
        public Expression JoinExpression { get; private set; } // full information is here, ReferencedTable and Join could be (in theory) extracted from this
        public TableJoinType JoinType { get; private set; }
        public string JoinID { get; private set; }
        public TableExpression JoinedTable { get; private set; }

        public TableExpression(Type type, string name, DbTableInfo tableInfo, LockOptions lockOptions = LockOptions.None)
                : base(SqlExpressionType.Table, type) {
          Vita.Common.Util.Check(type != null, "TableExpression (name: {0}) - type may not be null.", name);
          TableInfo = tableInfo; 
          Name = name;
          LockOptions = lockOptions;
        }


        /// <summary>
        /// Set table join
        /// </summary>
        /// <param name="joinType"></param>
        /// <param name="joinedTable"></param>
        /// <param name="joinExpression"></param>
        public void Join(TableJoinType joinType, TableExpression joinedTable, Expression joinExpression)
        {
            //RI: special case - inner joins on top of outer joins should become outer joins as well
          if (joinedTable.JoinedTable != null && (joinedTable.JoinType & TableJoinType.FullOuter) != 0)
            joinType |= joinedTable.JoinType;
          JoinExpression = joinExpression;
          JoinType = joinType;
          JoinedTable = joinedTable;
        }

        /// <summary>
        /// Set table join
        /// </summary>
        /// <param name="joinType"></param>
        /// <param name="joinedTable"></param>
        /// <param name="joinExpression"></param>
        /// <param name="joinID"></param>
        public void Join(TableJoinType joinType, TableExpression joinedTable, Expression joinExpression, string joinID)
        {
            Join(joinType, joinedTable, joinExpression);
            JoinID = joinID;
        }

        /// <summary>
        /// Set the table outer join, depending on the current table location
        /// Result can set the table to be left outer join or right outer join
        /// </summary>
        public void SetOuterJoin()
        {
            // JoinExpression is non-null for associated table
            if (JoinExpression != null)
                JoinType |= TableJoinType.LeftOuter;
            else
                JoinType |= TableJoinType.RightOuter;
        }

        public virtual bool IsEqualTo(TableExpression table)
        {
            return Name == table.Name && JoinID == table.JoinID && this.Alias == table.Alias; //RI: added Alias comparison
        }
    }
}