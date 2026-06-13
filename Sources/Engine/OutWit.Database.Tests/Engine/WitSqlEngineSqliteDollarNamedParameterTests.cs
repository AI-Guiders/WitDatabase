namespace OutWit.Database.Tests;

/// <summary>
/// SQLite-style $name parameters (Microsoft.Data.Sqlite / EF ADO compat).
/// </summary>
[TestFixture]
public sealed class WitSqlEngineSqliteDollarNamedParameterTests : WitSqlEngineTestsBase
{
    [Test]
    public void SelectWhereDollarNamedParameterTest()
    {
        m_engine.Execute("CREATE TABLE history (MigrationId TEXT PRIMARY KEY)");
        m_engine.Execute(
            "INSERT INTO history (MigrationId) VALUES (@seed)",
            new Dictionary<string, object?> { ["seed"] = "20260612034124_Initial" });

        var count = m_engine.ExecuteScalar(
            """
            SELECT COUNT(*) FROM history
            WHERE MigrationId = $id
            """,
            new Dictionary<string, object?> { ["$id"] = "20260612034124_Initial" }).AsInt64();

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void NumberedParameterStillDistinctFromDollarNamedTest()
    {
        m_engine.Execute("CREATE TABLE t (slot INTEGER PRIMARY KEY, label TEXT)");
        m_engine.Execute("INSERT INTO t (slot, label) VALUES (1, 'first')");

        var label = m_engine.ExecuteScalar(
            "SELECT label FROM t WHERE slot = $1",
            new Dictionary<string, object?> { ["$1"] = 1L }).AsString();

        Assert.That(label, Is.EqualTo("first"));
    }
}
