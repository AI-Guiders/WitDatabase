using OutWit.Database.Context;
using OutWit.Database.Expressions;
using OutWit.Database.Interfaces;
using OutWit.Database.Parser.Expressions;
using OutWit.Database.Parser.Schema.Types;
using OutWit.Database.Sql;
using OutWit.Database.Values;

namespace OutWit.Database.Iterators;

/// <summary>
/// Iterator for hash-based JOIN operations.
/// Uses build-probe algorithm: builds hash table on smaller relation, probes with larger.
/// Supports INNER and LEFT joins with equi-join conditions.
/// </summary>
/// <remarks>
/// Hash join has O(N + M) complexity compared to O(N × M) for nested loop.
/// Best used when:
/// - Join condition is equality (equi-join)
/// - Both tables have significant size (> 100 rows)
/// - No suitable index exists on join columns
/// </remarks>
public sealed class IteratorHashJoin : IteratorBase
{
    #region Fields

    private readonly IResultIterator m_buildSide;
    private readonly IResultIterator m_probeSide;
    private readonly JoinType m_joinType;
    private readonly IReadOnlyList<JoinKeyPair> m_joinKeys;
    private readonly WitSqlExpression? m_residualCondition;
    private readonly ExpressionEvaluator m_evaluator;
    private readonly bool m_buildIsLeft;

    private Dictionary<HashKey, List<WitSqlRow>>? m_hashTable;
    private IEnumerator<WitSqlRow>? m_currentMatches;
    private WitSqlRow m_currentProbeRow;
    private bool m_probeRowMatched;
    private bool m_probeExhausted;
    private WitSqlRow m_current;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new hash join iterator.
    /// </summary>
    /// <param name="left">The left iterator.</param>
    /// <param name="right">The right iterator.</param>
    /// <param name="joinType">The type of join (INNER or LEFT supported).</param>
    /// <param name="joinKeys">The equi-join key pairs.</param>
    /// <param name="residualCondition">Optional non-equality conditions to apply after hash match.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="buildLeft">If true, build hash table on left side; otherwise on right. Ignored for LEFT JOIN.</param>
    public IteratorHashJoin(
        IResultIterator left,
        IResultIterator right,
        JoinType joinType,
        IReadOnlyList<JoinKeyPair> joinKeys,
        WitSqlExpression? residualCondition,
        ContextExecution context,
        bool buildLeft = false)
    {
        if (joinType is not (JoinType.Inner or JoinType.Left))
            throw new ArgumentException($"Hash join only supports INNER and LEFT joins, got {joinType}", nameof(joinType));

        if (joinKeys.Count == 0)
            throw new ArgumentException("Hash join requires at least one join key", nameof(joinKeys));

        // For LEFT JOIN, we must probe the left side and build hash table on right
        // to preserve LEFT JOIN semantics (all left rows must appear in output)
        if (joinType == JoinType.Left)
        {
            m_buildIsLeft = false; // Always build right for LEFT JOIN
            m_buildSide = right;
            m_probeSide = left;
        }
        else
        {
            // For INNER JOIN, we can choose build side based on size
            m_buildIsLeft = buildLeft;
            m_buildSide = buildLeft ? left : right;
            m_probeSide = buildLeft ? right : left;
        }

        m_joinType = joinType;
        m_joinKeys = joinKeys;
        m_residualCondition = residualCondition;
        m_evaluator = new ExpressionEvaluator(context);

        Schema = BuildSchema(left.Schema, right.Schema);
    }

    #endregion

    #region Functions

    private static IReadOnlyList<WitSqlColumnInfo> BuildSchema(
        IReadOnlyList<WitSqlColumnInfo> leftSchema,
        IReadOnlyList<WitSqlColumnInfo> rightSchema)
    {
        var schema = new List<WitSqlColumnInfo>(leftSchema.Count + rightSchema.Count);
        schema.AddRange(leftSchema);
        schema.AddRange(rightSchema);
        return schema;
    }

    private HashKey ComputeHashKey(WitSqlRow row, bool isBuildSide)
    {
        if (m_joinKeys.Count == 1)
        {
            var keyExpr = isBuildSide == m_buildIsLeft
                ? m_joinKeys[0].LeftKey
                : m_joinKeys[0].RightKey;
            
            // Swap if build side is right
            if (!m_buildIsLeft)
            {
                keyExpr = isBuildSide
                    ? m_joinKeys[0].RightKey
                    : m_joinKeys[0].LeftKey;
            }

            var value = m_evaluator.Evaluate(keyExpr, row);
            return new HashKey(value);
        }

        var values = new WitSqlValue[m_joinKeys.Count];
        for (int i = 0; i < m_joinKeys.Count; i++)
        {
            var keyExpr = isBuildSide == m_buildIsLeft
                ? m_joinKeys[i].LeftKey
                : m_joinKeys[i].RightKey;

            if (!m_buildIsLeft)
            {
                keyExpr = isBuildSide
                    ? m_joinKeys[i].RightKey
                    : m_joinKeys[i].LeftKey;
            }

            values[i] = m_evaluator.Evaluate(keyExpr, row);
        }
        return new HashKey(values);
    }

    private WitSqlRow CombineRows(WitSqlRow probeRow, WitSqlRow buildRow)
    {
        // If build side is left, result is (build, probe) = (left, right)
        // If build side is right, result is (probe, build) = (left, right)
        var leftRow = m_buildIsLeft ? buildRow : probeRow;
        var rightRow = m_buildIsLeft ? probeRow : buildRow;

        return CombineLeftRight(leftRow, rightRow);
    }

    private WitSqlRow CombineLeftRight(WitSqlRow left, WitSqlRow right)
    {
        var leftSchema = m_buildIsLeft ? m_buildSide.Schema : m_probeSide.Schema;
        var rightSchema = m_buildIsLeft ? m_probeSide.Schema : m_buildSide.Schema;

        var columnCount = 0;
        foreach (var col in leftSchema)
            columnCount += col.TableName != null ? 2 : 1;
        foreach (var col in rightSchema)
            columnCount += col.TableName != null ? 2 : 1;

        var columns = new string[columnCount];
        var values = new WitSqlValue[columnCount];
        var index = 0;

        AddColumnsFromSchema(leftSchema, left, columns, values, ref index);
        AddColumnsFromSchema(rightSchema, right, columns, values, ref index);

        return new WitSqlRow(values, columns);
    }

    private static void AddColumnsFromSchema(
        IReadOnlyList<WitSqlColumnInfo> schema,
        WitSqlRow row,
        string[] columns,
        WitSqlValue[] values,
        ref int index)
    {
        foreach (var col in schema)
        {
            WitSqlValue value;
            var qualifiedName = col.TableName != null ? $"{col.TableName}.{col.Name}" : null;

            if (qualifiedName != null && row.TryGetValue(qualifiedName, out value))
            {
                // Found by qualified name
            }
            else if (row.TryGetValue(col.Name, out value))
            {
                // Found by simple name
            }
            else
            {
                value = WitSqlValue.Null;
            }

            columns[index] = col.Name;
            values[index] = value;
            index++;

            if (col.TableName != null)
            {
                columns[index] = qualifiedName!;
                values[index] = value;
                index++;
            }
        }
    }

    private WitSqlRow CreateNullBuildRow()
    {
        var columnCount = 0;
        foreach (var col in m_buildSide.Schema)
            columnCount += col.TableName != null ? 2 : 1;

        var columns = new string[columnCount];
        var values = new WitSqlValue[columnCount];
        var index = 0;

        foreach (var col in m_buildSide.Schema)
        {
            columns[index] = col.Name;
            values[index] = WitSqlValue.Null;
            index++;

            if (col.TableName != null)
            {
                columns[index] = $"{col.TableName}.{col.Name}";
                values[index] = WitSqlValue.Null;
                index++;
            }
        }

        return new WitSqlRow(values, columns);
    }

    private bool MatchesResidualCondition(WitSqlRow combined)
    {
        if (m_residualCondition == null)
            return true;

        var result = m_evaluator.Evaluate(m_residualCondition, combined);
        return !result.IsNull && result.AsBool();
    }

    private bool TryGetNextMatch()
    {
        while (m_currentMatches != null && m_currentMatches.MoveNext())
        {
            var buildRow = m_currentMatches.Current;
            var combined = CombineRows(m_currentProbeRow, buildRow);

            if (MatchesResidualCondition(combined))
            {
                m_probeRowMatched = true;
                m_current = combined;
                return true;
            }
        }
        
        // No more matches - clear the enumerator
        m_currentMatches?.Dispose();
        m_currentMatches = null;
        return false;
    }

    #endregion

    #region IResultIterator

    /// <inheritdoc/>
    public override void Open()
    {
        base.Open();
        m_buildSide.Open();
        m_probeSide.Open();

        // Build phase: create hash table from build side
        m_hashTable = new Dictionary<HashKey, List<WitSqlRow>>();

        while (m_buildSide.MoveNext())
        {
            var row = m_buildSide.Current;
            var key = ComputeHashKey(row, isBuildSide: true);

            // Skip rows with NULL in any join key
            if (key.HasNull)
                continue;

            if (!m_hashTable.TryGetValue(key, out var bucket))
            {
                bucket = new List<WitSqlRow>();
                m_hashTable[key] = bucket;
            }
            bucket.Add(row);
        }

        m_currentMatches = null;
        m_probeRowMatched = false;
        m_probeExhausted = false;
    }

    /// <inheritdoc/>
    public override bool MoveNext()
    {
        // Try to get next match from current probe row
        if (TryGetNextMatch())
            return true;

        // For LEFT JOIN, emit unmatched probe row
        if (m_joinType == JoinType.Left && !m_probeRowMatched && !m_probeExhausted && m_currentMatches != null)
        {
            m_current = CombineRows(m_currentProbeRow, CreateNullBuildRow());
            m_currentMatches = null;
            return true;
        }

        // Get next probe row
        while (m_probeSide.MoveNext())
        {
            m_currentProbeRow = m_probeSide.Current;
            m_probeRowMatched = false;

            var key = ComputeHashKey(m_currentProbeRow, isBuildSide: false);

            // NULL keys never match in equi-join
            if (key.HasNull)
            {
                if (m_joinType == JoinType.Left)
                {
                    m_current = CombineRows(m_currentProbeRow, CreateNullBuildRow());
                    return true;
                }
                continue;
            }

            if (m_hashTable!.TryGetValue(key, out var matches))
            {
                m_currentMatches = matches.GetEnumerator();
                if (TryGetNextMatch())
                    return true;
            }

            // No matches found for this probe row
            if (m_joinType == JoinType.Left)
            {
                m_current = CombineRows(m_currentProbeRow, CreateNullBuildRow());
                return true;
            }
        }

        m_probeExhausted = true;
        return false;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        m_buildSide.Reset();
        m_probeSide.Reset();
        m_hashTable = null;
        m_currentMatches = null;
        m_probeRowMatched = false;
        m_probeExhausted = false;
        m_current = default;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public override void Dispose()
    {
        m_currentMatches?.Dispose();
        m_buildSide.Dispose();
        m_probeSide.Dispose();
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override IReadOnlyList<WitSqlColumnInfo> Schema { get; }

    /// <inheritdoc/>
    public override WitSqlRow Current => m_current;

    #endregion

    #region Nested Types

    /// <summary>
    /// Represents a pair of join key expressions (left and right).
    /// </summary>
    public sealed class JoinKeyPair
    {
        /// <summary>
        /// The left side key expression.
        /// </summary>
        public required WitSqlExpression LeftKey { get; init; }

        /// <summary>
        /// The right side key expression.
        /// </summary>
        public required WitSqlExpression RightKey { get; init; }
    }

    /// <summary>
    /// Hash key for join operations. Supports single and multi-column keys.
    /// </summary>
    private readonly struct HashKey : IEquatable<HashKey>
    {
        private readonly WitSqlValue m_v0;
        private readonly WitSqlValue[]? m_extra;
        private readonly int m_count;
        private readonly int m_hashCode;
        private readonly bool m_hasNull;

        public HashKey(WitSqlValue value)
        {
            m_v0 = value;
            m_extra = null;
            m_count = 1;
            m_hasNull = value.IsNull;
            m_hashCode = value.IsNull ? 0 : value.GetHashCode();
        }

        public HashKey(WitSqlValue[] values)
        {
            m_count = values.Length;
            m_v0 = values.Length > 0 ? values[0] : default;
            m_extra = values.Length > 1 ? values[1..] : null;

            m_hasNull = false;
            var hash = new HashCode();
            foreach (var v in values)
            {
                if (v.IsNull)
                    m_hasNull = true;
                hash.Add(v);
            }
            m_hashCode = m_hasNull ? 0 : hash.ToHashCode();
        }

        public bool HasNull => m_hasNull;

        public bool Equals(HashKey other)
        {
            if (m_count != other.m_count)
                return false;

            if (m_hasNull || other.m_hasNull)
                return false; // NULL never equals NULL in SQL

            if (m_v0 != other.m_v0)
                return false;

            if (m_extra == null)
                return other.m_extra == null;

            if (other.m_extra == null)
                return false;

            for (int i = 0; i < m_extra.Length; i++)
            {
                if (m_extra[i] != other.m_extra[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is HashKey other && Equals(other);
        public override int GetHashCode() => m_hashCode;
    }

    #endregion
}
