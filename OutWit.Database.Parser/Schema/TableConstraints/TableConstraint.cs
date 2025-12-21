using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Values;

namespace OutWit.Database.Parser.Schema.TableConstraints
{
    public abstract class TableConstraint : ModelBase
    {
        #region Model Base

        public override bool Is(ModelBase modelBase, double tolerance = 1E-07)
        {
            if (modelBase is not TableConstraint other)
                return false;

            return Name.Is(other.Name);
        }

        #endregion

        #region Properties

        [ToString]
        public string? Name { get; init; }

        #endregion
    }
}
