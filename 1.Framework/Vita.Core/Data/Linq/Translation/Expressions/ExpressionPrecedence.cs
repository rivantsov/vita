
namespace Vita.Data.Linq.Translation.Expressions {

    internal enum ExpressionPrecedence
    {
        /// <summary>
        /// x.y  f(x)  a[x]  x++  x--  new typeof  checked  unchecked
        /// </summary>
        Primary,
        /// <summary>
        /// +  -  !  ~  ++x  --x  (T)x
        /// </summary>
        Unary,
        /// <summary>
        /// *  /  %
        /// </summary>
        Multiplicative,
        /// <summary>
        /// +  -
        /// </summary>
        Additive,
        /// <summary>
        /// &lt;&lt;  >>
        /// </summary>
        Shift,
        /// <summary>
        /// &lt;  >  &lt;=  >=  is  as
        /// </summary>
        RelationalAndTypeTest,
        /// <summary>
        /// ==  !=
        /// </summary>
        Equality,
        /// <summary>
        /// &amp;
        /// </summary>
        LogicalAnd,
        /// <summary>
        /// ^
        /// </summary>
        LogicalXor,
        /// <summary>
        /// |
        /// </summary>
        LogicalOr,
        /// <summary>
        /// &amp;&amp;
        /// </summary>
        ConditionalAnd,
        /// <summary>
        /// ||
        /// </summary>
        ConditionalOr,
        /// <summary>
        /// ??
        /// </summary>
        NullCoalescing,
        /// <summary>
        /// ?:
        /// </summary>
        Conditional,
        /// <summary>
        /// Assignments and augmented assignments
        /// </summary>
        Assignment,

        /// <summary>
        /// A SQL clause, FROM, WHERE, etc.
        /// </summary>
        Clause
    }
}