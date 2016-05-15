
using System;

namespace Vita.Data.Linq.Translation.Expressions
{
    [Flags]
    public enum TableJoinType
    {
        /// <summary>
        /// No join specified
        /// </summary>
        Default = 0,

        /// <summary>
        /// Inner join, default case for joins
        /// </summary>
        Inner = 0,

        /// <summary>
        /// Left outer join
        /// </summary>
        LeftOuter = 0x01,

        /// <summary>
        /// Right outer join
        /// </summary>
        RightOuter = 0x02,

        /// <summary>
        /// Full outer join
        /// </summary>
        FullOuter = LeftOuter | RightOuter,
    }
}