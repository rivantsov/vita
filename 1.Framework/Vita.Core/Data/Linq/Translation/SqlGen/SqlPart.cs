
namespace Vita.Data.Linq.Translation.SqlGen {

    /// <summary>
    /// An SqlPart is a constitutive string of SQL query
    /// </summary>
    public abstract class SqlPart
    {
        /// <summary>
        /// The resulting SQL string
        /// </summary>
        public abstract string Sql { get; }
    }
}
