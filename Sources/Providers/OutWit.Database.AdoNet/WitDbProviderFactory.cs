using System.Data.Common;

namespace OutWit.Database.AdoNet;

/// <summary>
/// A factory for creating instances of the WitDatabase ADO.NET provider classes.
/// </summary>
public sealed class WitDbProviderFactory : DbProviderFactory
{
    #region Constants

    /// <summary>
    /// The invariant name for this provider.
    /// </summary>
    public const string PROVIDER_INVARIANT_NAME = "OutWit.Database.AdoNet";

    #endregion

    #region Fields

    /// <summary>
    /// Gets the singleton instance of <see cref="WitDbProviderFactory"/>.
    /// </summary>
    public static readonly WitDbProviderFactory Instance = new();

    #endregion

    #region Constructors

    private WitDbProviderFactory()
    {
    }

    #endregion

    #region Factory Methods

    /// <inheritdoc/>
    public override DbConnection CreateConnection()
    {
        return new WitDbConnection();
    }

    /// <inheritdoc/>
    public override DbCommand CreateCommand()
    {
        return new WitDbCommand();
    }

    /// <inheritdoc/>
    public override DbParameter CreateParameter()
    {
        return new WitDbParameter();
    }

    /// <inheritdoc/>
    public override DbConnectionStringBuilder CreateConnectionStringBuilder()
    {
        return new WitDbConnectionStringBuilder();
    }

    /// <inheritdoc/>
    public override DbDataAdapter CreateDataAdapter()
    {
        return new WitDbDataAdapter();
    }

    /// <inheritdoc/>
    public override DbCommandBuilder CreateCommandBuilder()
    {
        return new WitDbCommandBuilder();
    }

    #endregion

    #region Properties

    /// <inheritdoc/>
    public override bool CanCreateDataAdapter => true;

    /// <inheritdoc/>
    public override bool CanCreateCommandBuilder => true;

    #endregion
}
