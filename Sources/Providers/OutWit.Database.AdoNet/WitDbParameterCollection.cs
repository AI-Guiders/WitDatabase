using System.Collections;
using System.Data.Common;

namespace OutWit.Database.AdoNet;

/// <summary>
/// Represents a collection of parameters for a <see cref="WitDbCommand"/>.
/// </summary>
public sealed class WitDbParameterCollection : DbParameterCollection
{
    #region Fields

    private readonly object m_syncRoot = new();
    private readonly List<WitDbParameter> m_parameters = [];

    #endregion

    #region Add

    /// <inheritdoc/>
    public override int Add(object value)
    {
        var param = CastToWitDbParameter(value);
        m_parameters.Add(param);
        return m_parameters.Count - 1;
    }

    /// <summary>
    /// Adds a parameter to the collection.
    /// </summary>
    /// <param name="parameter">The parameter to add.</param>
    /// <returns>The added parameter.</returns>
    public WitDbParameter Add(WitDbParameter parameter)
    {
        m_parameters.Add(parameter);
        return parameter;
    }

    /// <summary>
    /// Adds a parameter with the specified name and value.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>The added parameter.</returns>
    public WitDbParameter AddWithValue(string parameterName, object? value)
    {
        var param = new WitDbParameter(parameterName, value);
        m_parameters.Add(param);
        return param;
    }

    /// <inheritdoc/>
    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    #endregion

    #region Remove

    /// <inheritdoc/>
    public override void Clear()
    {
        m_parameters.Clear();
    }

    /// <inheritdoc/>
    public override void Remove(object value)
    {
        var param = CastToWitDbParameter(value);
        m_parameters.Remove(param);
    }

    /// <inheritdoc/>
    public override void RemoveAt(int index)
    {
        m_parameters.RemoveAt(index);
    }

    /// <inheritdoc/>
    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
            m_parameters.RemoveAt(index);
    }

    #endregion

    #region Contains/IndexOf

    /// <inheritdoc/>
    public override bool Contains(object value)
    {
        return value is WitDbParameter param && m_parameters.Contains(param);
    }

    /// <inheritdoc/>
    public override bool Contains(string value)
    {
        return IndexOf(value) >= 0;
    }

    /// <inheritdoc/>
    public override int IndexOf(object value)
    {
        return value is WitDbParameter param ? m_parameters.IndexOf(param) : -1;
    }

    /// <inheritdoc/>
    public override int IndexOf(string parameterName)
    {
        for (int i = 0; i < m_parameters.Count; i++)
        {
            if (NamesMatch(m_parameters[i].ParameterName, parameterName))
                return i;
        }
        return -1;
    }

    private static bool NamesMatch(string name1, string name2)
    {
        // Normalize names by removing @ : $ prefixes
        var n1 = NormalizeName(name1);
        var n2 = NormalizeName(name2);
        return string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        
        if (name.StartsWith("@") || name.StartsWith(":") || name.StartsWith("$"))
            return name[1..];
        
        return name;
    }

    #endregion

    #region Insert

    /// <inheritdoc/>
    public override void Insert(int index, object value)
    {
        var param = CastToWitDbParameter(value);
        m_parameters.Insert(index, param);
    }

    #endregion

    #region CopyTo

    /// <inheritdoc/>
    public override void CopyTo(Array array, int index)
    {
        ((ICollection)m_parameters).CopyTo(array, index);
    }

    #endregion

    #region Enumerator

    /// <inheritdoc/>
    public override IEnumerator GetEnumerator()
    {
        return m_parameters.GetEnumerator();
    }

    #endregion

    #region GetParameter/SetParameter

    /// <inheritdoc/>
    protected override DbParameter GetParameter(int index)
    {
        return m_parameters[index];
    }

    /// <inheritdoc/>
    protected override DbParameter GetParameter(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index < 0)
            throw new ArgumentException($"Parameter '{parameterName}' not found.", nameof(parameterName));
        return m_parameters[index];
    }

    /// <inheritdoc/>
    protected override void SetParameter(int index, DbParameter value)
    {
        m_parameters[index] = CastToWitDbParameter(value);
    }

    /// <inheritdoc/>
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index < 0)
            throw new ArgumentException($"Parameter '{parameterName}' not found.", nameof(parameterName));
        m_parameters[index] = CastToWitDbParameter(value);
    }

    private static WitDbParameter CastToWitDbParameter(object value)
    {
        return value as WitDbParameter 
               ?? throw new ArgumentException("Parameter must be a WitDbParameter.", nameof(value));
    }

    #endregion

    #region Indexers

    /// <summary>
    /// Gets or sets the parameter at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the parameter.</param>
    /// <returns>The parameter at the specified index.</returns>
    public new WitDbParameter this[int index]
    {
        get => m_parameters[index];
        set => m_parameters[index] = value;
    }

    /// <summary>
    /// Gets or sets the parameter with the specified name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The parameter with the specified name.</returns>
    public new WitDbParameter this[string parameterName]
    {
        get => (WitDbParameter)GetParameter(parameterName);
        set => SetParameter(parameterName, value);
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override int Count => m_parameters.Count;

    /// <inheritdoc/>
    public override object SyncRoot => m_syncRoot;

    #endregion
}
