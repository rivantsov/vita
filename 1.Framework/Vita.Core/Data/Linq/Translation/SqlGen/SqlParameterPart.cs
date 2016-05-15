
using System.Diagnostics;

namespace Vita.Data.Linq.Translation.SqlGen {

    /// <summary>
    /// SqlPart exposing a parameter
    /// </summary>
    [DebuggerDisplay("SqlParameterPart {Parameter} (as {Alias})")]
    public class SqlParameterPart : SqlPart  {
        /// <summary>
        /// The SQL part is the literal parameter
        /// </summary>
        public override string Sql { get { return Parameter; } }

        /// <summary>
        /// Literal parameter to be used
        /// </summary>
        public string Parameter { get; private set; }

        /// <summary>
        /// Raw parameter name
        /// </summary>
        public string Alias { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlParameterPart"/> class.
        /// </summary>
        /// <param name="parameter">The parameter.</param>
        /// <param name="alias">The alias.</param>
        public SqlParameterPart(string parameter, string alias)
        {
            Parameter = parameter;
            Alias = alias;
        }
    }
}
