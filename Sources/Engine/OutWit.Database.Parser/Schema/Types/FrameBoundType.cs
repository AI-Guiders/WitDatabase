namespace OutWit.Database.Parser.Schema.Types;

/// <summary>
/// The type of frame boundary.
/// </summary>
public enum FrameBoundType
{
    /// <summary>
    /// UNBOUNDED PRECEDING - start from the first row of the partition.
    /// </summary>
    UnboundedPreceding,
    
    /// <summary>
    /// n PRECEDING - n rows/values before the current row.
    /// </summary>
    Preceding,
    
    /// <summary>
    /// CURRENT ROW - the current row.
    /// </summary>
    CurrentRow,
    
    /// <summary>
    /// n FOLLOWING - n rows/values after the current row.
    /// </summary>
    Following,
    
    /// <summary>
    /// UNBOUNDED FOLLOWING - extend to the last row of the partition.
    /// </summary>
    UnboundedFollowing
}
