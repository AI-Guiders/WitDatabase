using OutWit.Common.Abstract;
using OutWit.Common.Values;
using OutWit.Database.Parser.Interfaces;

namespace OutWit.Database.Parser.Nodes
{
    /// <summary>
    /// Base class for all SQL AST nodes.
    /// </summary>
    public abstract class WitSqlNode : ModelBase
    {
        #region Model Base

        public override bool Is(ModelBase modelBase, double tolerance = 1E-07)
        {
            if(modelBase is not WitSqlNode other)
                return false;

            return Line.Is(other.Line)
                && Column.Is(other.Column);
        }

        #endregion

        #region Functions

        /// <summary>
        /// Accept a visitor.
        /// </summary>
        public abstract T Accept<T>(IWitSqlVisitor<T> visitor);

        #endregion

        #region Properties

        /// <summary>
        /// Line number in source SQL (1-based).
        /// </summary>
        public int Line { get; init; }

        /// <summary>
        /// Column position in source SQL (0-based).
        /// </summary>
        public int Column { get; init; }

        #endregion
    }
}
