# OutWit.Database.AdoNet - Implementation TODO

**Version:** 1.0  
**Last Updated:** 2025-02-05

---

## Overview

This package provides a standard ADO.NET provider for WitDatabase, allowing it to be used with any .NET application that uses `System.Data.Common` abstractions.

**Target:** Full `System.Data.Common` compatibility for seamless integration with existing .NET data access patterns.

---

## Implementation Progress

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 1: Core Classes | ? Done | All core ADO.NET classes implemented |
| Phase 2: Infrastructure | ? Done | ConnectionStringBuilder, ProviderFactory |
| Phase 3: Data Adapters | ? Done | DataAdapter, CommandBuilder |
| Phase 4: Connection Pooling | ? Done | Pool management |
| Phase 5: Advanced Features | ? Done | Schema discovery |
| Phase 6: Extended Configuration | ? Done | Full modular configuration support |

---

## Implemented Files

```
OutWit.Database.AdoNet/
??? WitDbConnection.cs           ?
??? WitDbCommand.cs              ?
??? WitDbDataReader.cs           ?
??? WitDbParameter.cs            ?
??? WitDbParameterCollection.cs  ?
??? WitDbTransaction.cs          ?
??? WitDbConnectionStringBuilder.cs ? (Extended)
??? WitDbProviderFactory.cs      ?
??? WitDbDataAdapter.cs          ?
??? WitDbCommandBuilder.cs       ?
??? WitDbException.cs            ?
??? Pool/
?   ??? ConnectionPool.cs        ?
?   ??? PooledConnection.cs      ?
?   ??? PoolOptions.cs           ?
??? Schema/
?   ??? SchemaProvider.cs        ?
??? TODO.md
```

---

## Connection String Properties

The `WitDbConnectionStringBuilder` now supports all modular configuration options from `OutWit.Database.Core`.

### Core Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Data Source` | string | - | Path to database file or `:memory:` |
| `Mode` | enum | ReadWriteCreate | Connection mode |
| `Read Only` | bool | false | Open in read-only mode |

### Storage Engine

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Store` | enum | BTree | Storage engine (BTree, LSM) |

### Encryption

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Encryption` | enum | None | Encryption type (None, AesGcm, ChaCha20) |
| `Password` | string | - | Encryption password |
| `User` | string | - | Username for user-based salt derivation |
| `Fast Encryption` | bool | false | Use faster PBKDF2 iterations (for WASM) |

### Transaction Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Isolation Level` | enum | ReadCommitted | Default isolation level |
| `MVCC` | bool | true | Enable Multi-Version Concurrency Control |
| `Transactions` | bool | true | Enable transaction support |

### Locking Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `File Locking` | bool | true | Enable file locking |
| `Lock Timeout` | int | 30 | Lock timeout in seconds |

### Cache/Page Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Cache Size` | int | 2000 | Number of pages to cache |
| `Page Size` | int | 4096 | Page size in bytes |

### Pooling Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Pooling` | bool | false | Enable connection pooling |
| `Min Pool Size` | int | 1 | Minimum pool size |
| `Max Pool Size` | int | 100 | Maximum pool size |
| `Default Timeout` | int | 30 | Default command timeout |

### LSM-Specific Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LSM MemTable Size` | long | 4MB | MemTable size limit |
| `LSM Block Size` | int | 4096 | SSTable block size |
| `LSM WAL` | bool | true | Enable Write-Ahead Log |
| `LSM Sync` | bool | true | Sync WAL to disk |
| `LSM Compaction Trigger` | int | 4 | Level-0 compaction trigger |
| `LSM Block Cache` | bool | true | Enable block cache |
| `LSM Block Cache Size` | long | 64MB | Block cache size |
| `LSM Background Compaction` | bool | true | Enable background compaction |

### Index Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Index Directory` | string | - | Custom index directory |

---

## Connection String Examples

### Simple File Database

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "mydb.witdb"
};
// Result: Data Source=mydb.witdb
```

### In-Memory Database

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = ":memory:",
    Mode = WitDbConnectionMode.Memory
};
// Result: Data Source=:memory:;Mode=Memory
```

### Encrypted Database (Password Only)

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "secure.witdb",
    Encryption = WitDbEncryptionType.AesGcm,
    Password = "MySecurePassword123!"
};
// Result: Data Source=secure.witdb;Encryption=AesGcm;Password=MySecurePassword123!
```

### Encrypted Database (User + Password)

For multi-tenant scenarios where each user has their own encryption key:

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "tenant.witdb",
    Encryption = WitDbEncryptionType.AesGcm,
    User = "tenant1",
    Password = "TenantSecret123"
};
// Result: Data Source=tenant.witdb;Encryption=AesGcm;User=tenant1;Password=TenantSecret123
```

### LSM-Tree for Write-Heavy Workloads

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "./lsm_data",
    Store = WitDbStoreType.LSM,
    LsmMemTableSize = 64 * 1024 * 1024,  // 64MB
    LsmBlockCacheSize = 128 * 1024 * 1024  // 128MB
};
```

### High Concurrency with MVCC

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "concurrent.witdb",
    Mvcc = true,
    IsolationLevel = WitDbIsolationLevel.Snapshot,
    FileLocking = true,
    LockTimeout = 60
};
```

### Blazor WASM Optimized

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "wasm.witdb",
    Encryption = WitDbEncryptionType.AesGcm,
    Password = "WasmPassword",
    FastEncryption = true,  // Faster key derivation
    CacheSize = 500,        // Smaller cache for browser
    PageSize = 4096
};
```

### Connection Pooled

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "pooled.witdb",
    Pooling = true,
    MinPoolSize = 5,
    MaxPoolSize = 50
};
```

---

## Enum Types

### WitDbConnectionMode

| Value | Description |
|-------|-------------|
| `ReadWriteCreate` | Open for R/W; create if missing |
| `ReadWrite` | Open for R/W; fail if missing |
| `ReadOnly` | Open for reading only |
| `Memory` | In-memory database |

### WitDbStoreType

| Value | Description |
|-------|-------------|
| `BTree` | B+Tree (read-optimized, single file) |
| `LSM` | LSM-Tree (write-optimized, directory-based) |

### WitDbEncryptionType

| Value | Description |
|-------|-------------|
| `None` | No encryption |
| `AesGcm` | AES-GCM (built-in, hardware-accelerated) |
| `ChaCha20` | ChaCha20-Poly1305 (requires BouncyCastle) |

### WitDbIsolationLevel

| Value | Description |
|-------|-------------|
| `ReadUncommitted` | Allows dirty reads |
| `ReadCommitted` | Only committed data visible |
| `RepeatableRead` | Locks held for transaction |
| `Serializable` | Full isolation |
| `Snapshot` | MVCC snapshot isolation |

---

## Phase 1: Core Classes (P0) ? COMPLETE

### 1.1 WitDbConnection ?

| Member | Status |
|--------|--------|
| `ConnectionString` property | ? |
| `Database` property | ? |
| `DataSource` property | ? |
| `ServerVersion` property | ? |
| `State` property | ? |
| `Open()` / `OpenAsync()` | ? |
| `Close()` / `CloseAsync()` | ? |
| `ChangeDatabase()` | ? |
| `BeginTransaction()` / `BeginTransactionAsync()` | ? |
| `CreateCommand()` | ? |
| `GetSchema()` | ? |
| `Dispose()` / `DisposeAsync()` | ? |

### 1.2 WitDbCommand ?

| Member | Status |
|--------|--------|
| `CommandText` property | ? |
| `CommandType` property | ? |
| `CommandTimeout` property | ? |
| `Connection` property | ? |
| `Transaction` property | ? |
| `Parameters` property | ? |
| `ExecuteNonQuery()` / `ExecuteNonQueryAsync()` | ? |
| `ExecuteScalar()` / `ExecuteScalarAsync()` | ? |
| `ExecuteReader()` / `ExecuteReaderAsync()` | ? |
| `Prepare()` / `PrepareAsync()` | ? |
| `Cancel()` | ? |
| `CreateParameter()` | ? |

### 1.3 WitDbDataReader ?

| Member | Status |
|--------|--------|
| `FieldCount` property | ? |
| `HasRows` property | ? |
| `IsClosed` property | ? |
| `RecordsAffected` property | ? |
| `Read()` / `ReadAsync()` | ? |
| `NextResult()` / `NextResultAsync()` | ? |
| `Close()` / `CloseAsync()` | ? |
| `GetName()` / `GetOrdinal()` | ? |
| `GetDataTypeName()` / `GetFieldType()` | ? |
| `GetValue()` / `GetValues()` | ? |
| `IsDBNull()` | ? |
| All typed getters | ? |
| `GetFieldValue<T>()` | ? |
| `GetSchemaTable()` | ? |
| Indexers | ? |

### 1.4 WitDbParameter ?

| Member | Status |
|--------|--------|
| `ParameterName` property | ? |
| `Value` property | ? |
| `DbType` property | ? |
| `Direction` property | ? |
| `IsNullable` property | ? |
| `Size` / `Precision` / `Scale` | ? |
| `SourceColumn` / `SourceVersion` | ? |
| `ResetDbType()` | ? |
| `Clone()` | ? |

### 1.5 WitDbParameterCollection ?

| Member | Status |
|--------|--------|
| `Count` property | ? |
| `Add()` / `AddWithValue()` / `AddRange()` | ? |
| `Clear()` | ? |
| `Contains()` / `IndexOf()` | ? |
| `Insert()` / `Remove()` / `RemoveAt()` | ? |
| Indexers | ? |

### 1.6 WitDbTransaction ?

| Member | Status |
|--------|--------|
| `Connection` property | ? |
| `IsolationLevel` property | ? |
| `Commit()` / `CommitAsync()` | ? |
| `Rollback()` / `RollbackAsync()` | ? |
| `Save()` / `SaveAsync()` | ? |
| `Release()` / `ReleaseAsync()` | ? |
| Auto-rollback on dispose | ? |

---

## Phase 2: Infrastructure (P0) ? COMPLETE

### 2.1 WitDbConnectionStringBuilder ? (Extended)

| Property | Status |
|----------|--------|
| `DataSource` | ? |
| `Mode` | ? |
| `Password` | ? |
| `User` | ? New |
| `ReadOnly` | ? |
| `Store` | ? New |
| `Encryption` | ? New |
| `FastEncryption` | ? New |
| `IsolationLevel` | ? |
| `MVCC` | ? New |
| `Transactions` | ? New |
| `FileLocking` | ? New |
| `LockTimeout` | ? New |
| `Pooling` | ? |
| `MinPoolSize` / `MaxPoolSize` | ? |
| `DefaultTimeout` | ? |
| `CacheSize` / `PageSize` | ? |
| `IndexDirectory` | ? New |
| LSM options (8 properties) | ? New |
| `Validate()` method | ? New |
| `ThrowIfInvalid()` method | ? New |

### 2.2 WitDbProviderFactory ?

| Member | Status |
|--------|--------|
| `Instance` static field | ? |
| `CanCreateDataAdapter` | ? |
| `CanCreateCommandBuilder` | ? |
| `CreateConnection()` | ? |
| `CreateCommand()` | ? |
| `CreateParameter()` | ? |
| `CreateDataAdapter()` | ? |
| `CreateCommandBuilder()` | ? |
| `CreateConnectionStringBuilder()` | ? |

---

## Phase 3: Data Adapters (P1) ? COMPLETE

### 3.1 WitDbDataAdapter ?

| Member | Status |
|--------|--------|
| `SelectCommand` / `InsertCommand` / `UpdateCommand` / `DeleteCommand` | ? |
| `RowUpdating` / `RowUpdated` events | ? |

### 3.2 WitDbCommandBuilder ?

| Member | Status |
|--------|--------|
| `DataAdapter` property | ? |
| `GetInsertCommand()` | ? |
| `GetUpdateCommand()` | ? |
| `GetDeleteCommand()` | ? |
| Quote prefix/suffix | ? |

### 3.3 WitDbException ?

| Feature | Status |
|---------|--------|
| Error codes (General, Syntax, Constraint, etc.) | ? |
| `FromException()` factory | ? |
| `WitErrorCode` property | ? |

---

## Phase 4: Connection Pooling (P1) ? COMPLETE

### 4.1 ConnectionPool ?

| Feature | Status |
|---------|--------|
| `GetPool()` static methods | ? |
| `GetConnection()` / `GetConnectionAsync()` | ? |
| `ReturnConnection()` | ? |
| `Clear()` / `ClearAllPools()` / `ClearPool()` | ? |
| Min/Max pool size | ? |
| Connection lifetime | ? |
| Idle timeout | ? |
| Validation on borrow | ? |
| Automatic cleanup timer | ? |

### 4.2 PooledConnection ?

| Feature | Status |
|---------|--------|
| `Open()` / `OpenAsync()` | ? |
| `Validate()` | ? |
| `IsExpired()` / `IsIdle()` | ? |
| `Touch()` | ? |
| Created/LastUsed timestamps | ? |

### 4.3 PoolOptions ?

| Property | Status |
|----------|--------|
| `MinPoolSize` / `MaxPoolSize` | ? |
| `ConnectionLifetime` | ? |
| `IdleTimeout` | ? |
| `ValidateOnBorrow` | ? |
| `ConnectionString` | ? |

---

## Phase 5: Schema Discovery (P2) ? COMPLETE

### 5.1 SchemaProvider ?

| Collection | Status |
|------------|--------|
| `MetaDataCollections` | ? |
| `DataSourceInformation` | ? |
| `DataTypes` | ? |
| `Restrictions` | ? |
| `ReservedWords` | ? |
| `Tables` | ? |
| `Columns` | ? |
| `Indexes` | ? |
| `IndexColumns` | ? |
| `Views` | ? |
| `ForeignKeys` | ? |

---

## Type Mappings

| WitSQL Type | DbType | CLR Type |
|-------------|--------|----------|
| `TINYINT` | `SByte` | `sbyte` |
| `UTINYINT` | `Byte` | `byte` |
| `SMALLINT` | `Int16` | `short` |
| `USMALLINT` | `UInt16` | `ushort` |
| `INT` | `Int32` | `int` |
| `UINT` | `UInt32` | `uint` |
| `BIGINT` | `Int64` | `long` |
| `UBIGINT` | `UInt64` | `ulong` |
| `FLOAT` | `Single` | `float` |
| `DOUBLE` | `Double` | `double` |
| `DECIMAL` | `Decimal` | `decimal` |
| `BOOLEAN` | `Boolean` | `bool` |
| `DATE` | `Date` | `DateOnly` |
| `TIME` | `Time` | `TimeOnly` |
| `DATETIME` | `DateTime` | `DateTime` |
| `DATETIMEOFFSET` | `DateTimeOffset` | `DateTimeOffset` |
| `INTERVAL` | `Object` | `TimeSpan` |
| `GUID` | `Guid` | `Guid` |
| `VARCHAR(n)` | `String` | `string` |
| `TEXT` | `String` | `string` |
| `VARBINARY(n)` | `Binary` | `byte[]` |
| `BLOB` | `Binary` | `byte[]` |
| `JSON` | `String` | `string` |
| `ROWVERSION` | `Binary` | `byte[]` |

---

## Usage Examples

### Basic Connection

```csharp
using var connection = new WitDbConnection("Data Source=mydb.witdb");
connection.Open();

using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Users WHERE Id = @id";
command.Parameters.AddWithValue("@id", 1);

using var reader = command.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"{reader.GetInt32(0)}: {reader.GetString(1)}");
}
```

### Transaction

```csharp
using var connection = new WitDbConnection("Data Source=mydb.witdb");
connection.Open();

using var transaction = connection.BeginTransaction();
try
{
    using var cmd = connection.CreateCommand();
    cmd.Transaction = transaction;
    cmd.CommandText = "INSERT INTO Users (Name) VALUES (@name)";
    cmd.Parameters.AddWithValue("@name", "John");
    cmd.ExecuteNonQuery();
    
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### Provider Factory

```csharp
DbProviderFactories.RegisterFactory("OutWit.Database.AdoNet", WitDbProviderFactory.Instance);

var factory = DbProviderFactories.GetFactory("OutWit.Database.AdoNet");
using var connection = factory.CreateConnection();
connection.ConnectionString = "Data Source=mydb.witdb";
```

### Schema Discovery

```csharp
using var connection = new WitDbConnection("Data Source=mydb.witdb");
connection.Open();

var tables = connection.GetSchema("Tables");
foreach (DataRow row in tables.Rows)
{
    Console.WriteLine(row["TABLE_NAME"]);
}
```

### Encrypted Database with User/Password

```csharp
// Each user gets a unique encryption key derived from their username
var builder = new WitDbConnectionStringBuilder
{
    DataSource = $"user_{userId}.witdb",
    Encryption = WitDbEncryptionType.AesGcm,
    User = userId,
    Password = userPassword
};

using var connection = new WitDbConnection(builder.ConnectionString);
connection.Open();
// Data is encrypted with a key unique to this user
```

### LSM-Tree Write-Heavy Workload

```csharp
var builder = new WitDbConnectionStringBuilder
{
    DataSource = "./high_write_data",
    Store = WitDbStoreType.LSM,
    LsmMemTableSize = 64 * 1024 * 1024,
    LsmBlockCacheSize = 128 * 1024 * 1024,
    LsmBackgroundCompaction = true
};

using var connection = new WitDbConnection(builder.ConnectionString);
connection.Open();
// Optimized for high-throughput writes
```

---

## See Also

- [System.Data.Common](https://docs.microsoft.com/en-us/dotnet/api/system.data.common)
- [DbProviderFactory](https://docs.microsoft.com/en-us/dotnet/api/system.data.common.dbproviderfactory)
- [ADO.NET Provider Model](https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/ado-net-provider-model)
- [OutWit.Database.Core README](../../Core/OutWit.Database.Core/README.md)
