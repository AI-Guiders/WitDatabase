using System.Data;
using System.Data.Common;

namespace OutWit.Database.AdoNet;

/// <summary>
/// Represents a set of data commands and a database connection used to fill a DataSet
/// and update a WitDatabase database.
/// </summary>
public sealed class WitDbDataAdapter : DbDataAdapter
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbDataAdapter"/> class.
    /// </summary>
    public WitDbDataAdapter()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbDataAdapter"/> class
    /// with the specified select command.
    /// </summary>
    /// <param name="selectCommand">The SELECT command to use.</param>
    public WitDbDataAdapter(WitDbCommand selectCommand)
    {
        SelectCommand = selectCommand;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbDataAdapter"/> class
    /// with the specified select command text and connection.
    /// </summary>
    /// <param name="selectCommandText">The SELECT command text.</param>
    /// <param name="connection">The connection to use.</param>
    public WitDbDataAdapter(string selectCommandText, WitDbConnection connection)
    {
        SelectCommand = new WitDbCommand(selectCommandText, connection);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WitDbDataAdapter"/> class
    /// with the specified select command text and connection string.
    /// </summary>
    /// <param name="selectCommandText">The SELECT command text.</param>
    /// <param name="connectionString">The connection string.</param>
    public WitDbDataAdapter(string selectCommandText, string connectionString)
    {
        var connection = new WitDbConnection(connectionString);
        SelectCommand = new WitDbCommand(selectCommandText, connection);
    }

    #endregion

    #region Row Events

    /// <summary>
    /// Occurs during <see cref="DbDataAdapter.Update"/> before a command is executed against the data source.
    /// </summary>
    public event EventHandler<RowUpdatingEventArgs>? RowUpdating;

    /// <summary>
    /// Occurs during <see cref="DbDataAdapter.Update"/> after a command is executed against the data source.
    /// </summary>
    public event EventHandler<RowUpdatedEventArgs>? RowUpdated;

    /// <inheritdoc/>
    protected override void OnRowUpdating(RowUpdatingEventArgs value)
    {
        RowUpdating?.Invoke(this, value);
    }

    /// <inheritdoc/>
    protected override void OnRowUpdated(RowUpdatedEventArgs value)
    {
        RowUpdated?.Invoke(this, value);
    }

    /// <inheritdoc/>
    protected override RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
    {
        return new RowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
    }

    /// <inheritdoc/>
    protected override RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand? command, StatementType statementType, DataTableMapping tableMapping)
    {
        return new RowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the SELECT command.
    /// </summary>
    public new WitDbCommand? SelectCommand
    {
        get => (WitDbCommand?)base.SelectCommand;
        set => base.SelectCommand = value;
    }

    /// <summary>
    /// Gets or sets the INSERT command.
    /// </summary>
    public new WitDbCommand? InsertCommand
    {
        get => (WitDbCommand?)base.InsertCommand;
        set => base.InsertCommand = value;
    }

    /// <summary>
    /// Gets or sets the UPDATE command.
    /// </summary>
    public new WitDbCommand? UpdateCommand
    {
        get => (WitDbCommand?)base.UpdateCommand;
        set => base.UpdateCommand = value;
    }

    /// <summary>
    /// Gets or sets the DELETE command.
    /// </summary>
    public new WitDbCommand? DeleteCommand
    {
        get => (WitDbCommand?)base.DeleteCommand;
        set => base.DeleteCommand = value;
    }

    #endregion
}
