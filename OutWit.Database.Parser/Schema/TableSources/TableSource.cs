using OutWit.Common.Abstract;
using OutWit.Common.Attributes;
using OutWit.Common.Values;

namespace OutWit.Database.Parser.Schema.TableSources
{
    public abstract class TableSource : ModelBase
    {
        #region Model Base

        public override bool Is(ModelBase modelBase, double tolerance = 1E-07)
        {
            if(modelBase is not TableSource other)
                return false;

            return Alias.Is(other.Alias);
        }

        #endregion

        #region Properties

        [ToString]
        public string? Alias { get; init; }

        #endregion
    }
}
