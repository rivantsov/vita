
using System;
using System.Diagnostics;

namespace Vita.Data.Linq.Translation.SqlGen {
    /// <summary>
    /// Represents a literal SQL part
    /// </summary>
    [DebuggerDisplay("SqlLiteralPart {Literal}")]
    public  class SqlLiteralPart : SqlPart {
        /// <summary>
        /// The resulting SQL string
        /// </summary>
        /// <value></value>
        public override string Sql { get { return Literal; } }

        /// <summary>
        /// Literal SQL used as is
        /// </summary>
        public string Literal { get; private set; }

        public Type Type; //RI: adding this

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlLiteralPart"/> class.
        /// </summary>
        /// <param name="literal">The literal.</param>
        /// <param name="type">Type.</param>
        public SqlLiteralPart(string literal, Type type = null)
        {
            Literal = literal;
            Type = type; 
        }

/*
        /// <summary>
        /// Creates a SqlLiteralPart from a given string (implicit)
        /// </summary>
        /// <param name="literal"></param>
        /// <returns></returns>
        public static implicit operator SqlLiteralPart(string literal)
        {
            return new SqlLiteralPart(literal, null);
        }
 */ 
    }
}
