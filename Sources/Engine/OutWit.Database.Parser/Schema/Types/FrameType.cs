namespace OutWit.Database.Parser.Schema.Types;

/// <summary>
/// The type of frame unit (ROWS vs RANGE).
/// </summary>
public enum FrameType
{
    /// <summary>
    /// Frame based on physical row positions.
    /// </summary>
    Rows,
    
    /// <summary>
    /// Frame based on logical value ranges (requires ORDER BY).
    /// </summary>
    Range
}
