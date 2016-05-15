
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Vita.Data.Linq.Translation.SqlGen {

    /// <summary>
    /// An SqlStatement is a literal SQL request, composed of different parts (SqlPart)
    /// each part being either a parameter or a literal string
    /// </summary>
    [DebuggerDisplay("SqlStatement {ToString()}")]
    public class SqlStatement : IEnumerable<SqlPart>
    {
        private readonly List<SqlPart> parts = new List<SqlPart>();

        /// <summary>
        /// Empty SqlStatement, used to build new statements
        /// </summary>
        public static readonly SqlStatement Empty = new SqlStatement();

        /// <summary>
        /// Returns the number of parts present
        /// </summary>
        public int Count { get { return parts.Count; } }

        /// <summary>
        /// Enumerates all parts
        /// </summary>
        /// <returns></returns>
        public IEnumerator<SqlPart> GetEnumerator()
        {
            return parts.GetEnumerator();
        }

        /// <summary>
        /// Enumerates all parts
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns part at given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SqlPart this[int index]
        {
            get { return parts[index]; }
        }

        /// <summary>
        /// Combines all parts, in correct order
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Join(string.Empty, (from part in parts select part.Sql).ToArray());
        }

        /// <summary>
        /// Joins SqlStatements into a new SqlStatement
        /// </summary>
        /// <param name="sqlStatement"></param>
        /// <param name="sqlStatements"></param>
        /// <returns></returns>
        public static SqlStatement Join(SqlStatement sqlStatement, IList<SqlStatement> sqlStatements)
        {
            // optimization: if we have only one statement to join, we return the statement itself
            if (sqlStatements.Count == 1)
                return sqlStatements[0];
            var builder = new SqlStatementBuilder();
            builder.AppendJoin(sqlStatement, sqlStatements);
            return builder.ToSqlStatement();
        }

        /// <summary>
        /// Joins SqlStatements into a new SqlStatement
        /// </summary>
        /// <param name="sqlStatement"></param>
        /// <param name="sqlStatements"></param>
        /// <returns></returns>
        public static SqlStatement Join(SqlStatement sqlStatement, params SqlStatement[] sqlStatements)
        {
            return Join(sqlStatement, (IList<SqlStatement>)sqlStatements);
        }

        /// <summary>
        /// Formats an SqlStatement
        /// </summary>
        /// <param name="format"></param>
        /// <param name="sqlStatements"></param>
        /// <returns></returns>
        public static SqlStatement Format(string format, IList<SqlStatement> sqlStatements)
        {
            var builder = new SqlStatementBuilder();
            builder.AppendFormat(format, sqlStatements);
            return builder.ToSqlStatement();
        }

        /// <summary>
        /// Formats the specified text.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="sqlStatements">The SQL statements.</param>
        /// <returns></returns>
        public static SqlStatement Format(string format, params SqlStatement[] sqlStatements)
        {
            return Format(format, (IList<SqlStatement>)sqlStatements);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlStatement"/> class.
        /// </summary>
        public SqlStatement()
        {
        }

        /// <summary>
        /// Builds an SqlStatement by concatenating several statements
        /// </summary>
        /// <param name="sqlStatements"></param>
        public SqlStatement(IEnumerable<SqlStatement> sqlStatements)
        {
            foreach (var sqlStatement in sqlStatements)
            {
                parts.AddRange(sqlStatement.parts);
            }
        }

        /// <summary>
        /// Builds SqlStatement
        /// </summary>
        /// <param name="sqlStatements"></param>
        public SqlStatement(params SqlStatement[] sqlStatements)
            : this((IEnumerable<SqlStatement>)sqlStatements)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlStatement"/> class.
        /// </summary>
        /// <param name="sqlParts">The SQL parts.</param>
        public SqlStatement(params SqlPart[] sqlParts)
            : this((IList<SqlPart>)sqlParts)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlStatement"/> class.
        /// </summary>
        /// <param name="sqlParts">The SQL parts.</param>
        public SqlStatement(IEnumerable<SqlPart> sqlParts)
        {
            foreach (var sqlPart in sqlParts)
                SqlStatementBuilder.AddPart(parts, sqlPart);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlStatement"/> class.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        public SqlStatement(string sql)
        {
            parts.Add(new SqlLiteralPart(sql));
        }


        /// <summary>
        /// Converts a string to an SqlStatement
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public static implicit operator SqlStatement(string sql)
        {
            return new SqlStatement(sql);
        }
    }
}
