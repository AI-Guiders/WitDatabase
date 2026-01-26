using OutWit.Database.Sql;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Schema;

/// <summary>
/// INFORMATION_SCHEMA.SEQUENCES implementation.
/// </summary>
public sealed partial class SchemaCatalog
{
    #region Constants

    private static readonly string[] SEQUENCES_COLUMNS = [
        "SEQUENCE_CATALOG", "SEQUENCE_SCHEMA", "SEQUENCE_NAME",
        "DATA_TYPE", "NUMERIC_PRECISION", "NUMERIC_SCALE",
        "START_VALUE", "MINIMUM_VALUE", "MAXIMUM_VALUE", "INCREMENT",
        "CYCLE_OPTION", "CURRENT_VALUE"
    ];

    private static readonly WitSqlType[] SEQUENCES_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Integer, WitSqlType.Integer,
        WitSqlType.Integer, WitSqlType.Integer, WitSqlType.Integer, WitSqlType.Integer,
        WitSqlType.Text, WitSqlType.Integer
    ];

    #endregion

    #region INFORMATION_SCHEMA.SEQUENCES

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.SEQUENCES view data.
    /// Returns information about all sequences.
    /// </summary>
    public IEnumerable<WitSqlRow> GetInformationSchemaSequences()
    {
        m_lock.EnterReadLock();
        try
        {
            var results = new List<WitSqlRow>();
            
            foreach (var sequence in m_sequences.Values)
            {
                results.Add(new WitSqlRow([
                    WitSqlValue.FromText("WitDB"),                                       // SEQUENCE_CATALOG
                    WitSqlValue.FromText("public"),                                      // SEQUENCE_SCHEMA
                    WitSqlValue.FromText(sequence.Name),                                 // SEQUENCE_NAME
                    WitSqlValue.FromText("BIGINT"),                                      // DATA_TYPE
                    WitSqlValue.FromInt(19),                                             // NUMERIC_PRECISION (BIGINT = 19 digits)
                    WitSqlValue.FromInt(0),                                              // NUMERIC_SCALE
                    WitSqlValue.FromInt(sequence.StartWith),                             // START_VALUE
                    sequence.MinValue.HasValue
                        ? WitSqlValue.FromInt(sequence.MinValue.Value)
                        : WitSqlValue.Null,                                              // MINIMUM_VALUE
                    sequence.MaxValue.HasValue
                        ? WitSqlValue.FromInt(sequence.MaxValue.Value)
                        : WitSqlValue.Null,                                              // MAXIMUM_VALUE
                    WitSqlValue.FromInt(sequence.IncrementBy),                           // INCREMENT
                    WitSqlValue.FromText(sequence.Cycle ? "YES" : "NO"),                 // CYCLE_OPTION
                    WitSqlValue.FromInt(sequence.CurrentValue),                          // CURRENT_VALUE
                ], SEQUENCES_COLUMNS));
            }
            
            return results;
        }
        finally
        {
            m_lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.SEQUENCES.
    /// </summary>
    public static IReadOnlyList<string> GetInformationSchemaSequencesColumns() => SEQUENCES_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.SEQUENCES.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetInformationSchemaSequencesColumnTypes() => SEQUENCES_TYPES;

    #endregion
}
