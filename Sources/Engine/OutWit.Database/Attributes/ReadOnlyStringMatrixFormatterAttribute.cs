using MemoryPack;
using OutWit.Database.Formatters;

namespace OutWit.Database.Attributes
{
    public class ReadOnlyStringMatrixFormatterAttribute : MemoryPackCustomFormatterAttribute<IReadOnlyList<IReadOnlyList<string>>?>
    {
        public override IMemoryPackFormatter<IReadOnlyList<IReadOnlyList<string>>?> GetFormatter()
        {
            return new ReadOnlyStringMatrixMemoryPackFormatter();
        }
    }
}
