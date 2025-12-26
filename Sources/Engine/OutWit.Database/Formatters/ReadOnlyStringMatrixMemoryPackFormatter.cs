using System;
using MemoryPack;

namespace OutWit.Database.Formatters
{
    public class ReadOnlyStringMatrixMemoryPackFormatter : MemoryPackFormatter<IReadOnlyList<IReadOnlyList<string>>?>
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref IReadOnlyList<IReadOnlyList<string>>? value)
        {
            // If the outer list is null, write the null header and return
            if (value == null)
            {
                writer.WriteNullCollectionHeader();
                return;
            }

            // Write the count of the outer list
            writer.WriteCollectionHeader(value.Count);

            // Iterate over the outer list
            for (int i = 0; i < value.Count; i++)
            {
                var innerList = value[i];

                // Handle null inner lists
                if (innerList == null)
                {
                    writer.WriteNullCollectionHeader();
                    continue;
                }

                // Write the count of the inner list
                writer.WriteCollectionHeader(innerList.Count);

                // Iterate and write strings in the inner list
                for (int j = 0; j < innerList.Count; j++)
                    writer.WriteString(innerList[j]);
                
            }
        }

        public override void Deserialize(ref MemoryPackReader reader, scoped ref IReadOnlyList<IReadOnlyList<string>>? value)
        {
            // Try to read the header. If it returns false, the collection is null.
            if (!reader.TryReadCollectionHeader(out var outerCount))
            {
                value = null;
                return;
            }

            // Allocate a jagged array (array of arrays) which satisfies IReadOnlyList<IReadOnlyList<string>>
            var result = new string[outerCount][];

            for (int i = 0; i < outerCount; i++)
            {
                // Try to read inner list header
                if (!reader.TryReadCollectionHeader(out var innerCount))
                {
                    result[i] = null!; // Inner list is null
                    continue;
                }

                // Allocate inner array
                var innerArray = new string[innerCount];

                for (int j = 0; j < innerCount; j++)
                    innerArray[j] = reader.ReadString() ?? string.Empty;

                result[i] = innerArray;
            }

            value = result;
        }
    }
}
