# OutWit.Database.EntityFramework - Implementation TODO

**Version:** 1.0  
**Last Updated:** 2025-02-05

---

## Overview

This package provides an Entity Framework Core provider for WitDatabase, enabling full ORM support including migrations, LINQ queries, change tracking, and all standard EF Core features.

**Target:** Full EF Core 9.0/10.0 compatibility with support for all standard features.

**Prerequisite:** `OutWit.Database.AdoNet` must be completed first.

---

## Implementation Progress

### Completed Phases

- [x] **Phase 1:** Core Provider Infrastructure (P0) - COMPLETED
- [x] **Phase 2:** Database Provider (P0) - COMPLETED  
- [x] **Phase 3:** SQL Generation (P0) - COMPLETED (basic)
- [x] **Phase 4:** Type Mapping (P0) - COMPLETED
- [x] **Phase 5:** Model Building (P0) - COMPLETED (basic)
- [x] **Phase 6:** Update Pipeline (P0) - COMPLETED (basic)
- [x] **Phase 7:** Migrations (P1) - COMPLETED
- [x] **Phase 8:** Database Creation (P1) - COMPLETED

### Pending Phases

- [ ] **Phase 9:** Function Translations (P1)
- [ ] **Phase 10:** Advanced Features (P2)

### Current Test Status

- **138 tests passing**
- **2 integration tests skipped** (require full provider implementation)

---

## Implementation Plan

### Phase 1: Core Provider Infrastructure (P0) - COMPLETED

#### 1.1 WitDbContextOptionsExtension

Implemented in: `Infrastructure/WitDbContextOptionsExtension.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `Info` property | Done | Extension info for logging |
| `ApplyServices()` | Done | Register provider services |
| `Validate()` | Done | Validate options |
| `ConnectionString` property | Done | Database connection string |
| `Connection` property | Done | Existing connection |
| `InMemory` property | Done | In-memory mode |

#### 1.2 WitDbContextOptionsBuilder

Implemented in: `Extensions/WitDbContextOptionsBuilderExtensions.cs`

| Method | Status | Description |
|--------|--------|-------------|
| `UseWitDb(connectionString)` | Done | Configure with connection string |
| `UseWitDb(connection)` | Done | Configure with existing connection |
| `UseWitDbInMemory()` | Done | Configure for in-memory |
| `EnableSensitiveDataLogging()` | Done | Log parameter values |
| `UseQuerySplittingBehavior()` | Done | Split/single query mode |

---

### Phase 2: Database Provider (P0) - COMPLETED

#### 2.1 WitDatabaseProvider

Implemented in: `Infrastructure/WitDatabaseProvider.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `Name` property | Done | Provider name ("OutWit.Database.EntityFramework") |
| `IsConfigured()` | Done | Check if provider is configured |

#### 2.2 WitRelationalConnection

Implemented in: `Storage/WitRelationalConnection.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `CreateDbConnection()` | Done | Create WitDbConnection |
| `ConnectionString` property | Done | Connection string |

---

### Phase 3: SQL Generation (P0) - COMPLETED (basic)

#### 3.1 WitSqlGenerationHelper

Implemented in: `Storage/WitSqlGenerationHelper.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `DelimitIdentifier()` | Done | Quote identifiers with `"` |
| `EscapeIdentifier()` | Done | Escape special characters |
| `GenerateParameterName()` | Done | Generate @param names |
| `GenerateParameterNamePlaceholder()` | Done | Generate @param placeholders |
| `StatementTerminator` property | Done | Return `;` |
| `BatchTerminator` property | Done | Return empty (no GO) |

#### 3.2 WitQuerySqlGenerator

Implemented in: `Query/WitQuerySqlGenerator.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `VisitSqlBinary()` | Done | Generate binary expressions (string concatenation with `\|\|`) |
| `GenerateLimitOffset()` | Done | Generate LIMIT/OFFSET |
| `GenerateTop()` | Done | Empty (WitDB doesn't use TOP) |

#### 3.3 WitQuerySqlGeneratorFactory

Implemented in: `Query/WitQuerySqlGeneratorFactory.cs`

---

### Phase 4: Type Mapping (P0) - COMPLETED

#### 4.1 WitTypeMappingSource

Implemented in: `Storage/WitTypeMappingSource.cs`

| CLR Type | WitSQL Type | Status |
|----------|-------------|--------|
| `bool` | `BOOLEAN` | Done |
| `byte` | `UTINYINT` | Done |
| `sbyte` | `TINYINT` | Done |
| `short` | `SMALLINT` | Done |
| `ushort` | `USMALLINT` | Done |
| `int` | `INT` | Done |
| `uint` | `UINT` | Done |
| `long` | `BIGINT` | Done |
| `ulong` | `UBIGINT` | Done |
| `float` | `FLOAT` | Done |
| `double` | `DOUBLE` | Done |
| `decimal` | `DECIMAL` | Done |
| `string` | `TEXT` | Done |
| `byte[]` | `BLOB` | Done |
| `DateTime` | `DATETIME` | Done |
| `DateTimeOffset` | `DATETIMEOFFSET` | Done |
| `DateOnly` | `DATE` | Done |
| `TimeOnly` | `TIME` | Done |
| `TimeSpan` | `INTERVAL` | Done |
| `Guid` | `GUID` | Done |
| `Enum` | `INT` | Done |

---

### Phase 5: Model Building (P0) - COMPLETED (basic)

#### 5.1 WitModelValidator

Implemented in: `Metadata/WitModelValidator.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `ValidateModel()` | Done | Validate model against WitDB constraints |
| `ValidateNoSchemas()` | Done | WitDB doesn't support schemas |

#### 5.2 WitAnnotationProvider

Implemented in: `Metadata/WitAnnotationProvider.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `For(IColumn)` | Done | Column annotations (autoincrement) |

---

### Phase 6: Update Pipeline (P0) - COMPLETED (basic)

#### 6.1 WitUpdateSqlGenerator

Implemented in: `Update/WitUpdateSqlGenerator.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `AppendValues()` | Done | Handle DEFAULT VALUES |
| `GenerateNextSequenceValueOperation()` | Done | Generate INCREMENT() |

#### 6.2 WitModificationCommandBatchFactory

Implemented in: `Update/WitModificationCommandBatchFactory.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `Create()` | Done | Create modification batch |

---

### Phase 7: Migrations (P1) - COMPLETED

#### 7.1 WitMigrationsSqlGenerator

Implemented in: `Migrations/WitMigrationsSqlGenerator.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `Generate(CreateTableOperation)` | Done | CREATE TABLE |
| `Generate(DropTableOperation)` | Done | DROP TABLE IF EXISTS |
| `Generate(RenameTableOperation)` | Done | ALTER TABLE RENAME TO |
| `Generate(AddColumnOperation)` | Done | ALTER TABLE ADD COLUMN |
| `Generate(DropColumnOperation)` | Done | ALTER TABLE DROP COLUMN |
| `Generate(AlterColumnOperation)` | Done | ALTER COLUMN SET/DROP |
| `Generate(RenameColumnOperation)` | Done | ALTER TABLE RENAME COLUMN |
| `Generate(CreateIndexOperation)` | Done | CREATE INDEX IF NOT EXISTS |
| `Generate(DropIndexOperation)` | Done | DROP INDEX IF EXISTS |
| `Generate(AddForeignKeyOperation)` | Done | Comment (limited support) |
| `Generate(DropForeignKeyOperation)` | Done | Comment (limited support) |
| `Generate(AddPrimaryKeyOperation)` | Done | Comment (limited support) |
| `Generate(DropPrimaryKeyOperation)` | Done | Comment (limited support) |
| `Generate(AddUniqueConstraintOperation)` | Done | Create unique index |
| `Generate(DropUniqueConstraintOperation)` | Done | Drop index |
| `Generate(AddCheckConstraintOperation)` | Done | Comment (future support) |
| `Generate(DropCheckConstraintOperation)` | Done | Comment (future support) |
| `Generate(CreateSequenceOperation)` | Done | CREATE SEQUENCE |
| `Generate(DropSequenceOperation)` | Done | DROP SEQUENCE |
| `Generate(AlterSequenceOperation)` | Done | ALTER SEQUENCE RESTART |
| `Generate(SqlOperation)` | Done | Raw SQL |
| `ColumnDefinition()` | Done | Column definition with types |

#### 7.2 WitHistoryRepository

Implemented in: `Migrations/WitHistoryRepository.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `ExistsSql` property | Done | SELECT from INFORMATION_SCHEMA |
| `GetCreateScript()` | Done | CREATE TABLE IF NOT EXISTS |
| `GetCreateIfNotExistsScript()` | Done | Same as GetCreateScript |
| `GetBeginIfNotExistsScript()` | Done | Empty (not supported) |
| `GetBeginIfExistsScript()` | Done | Empty (not supported) |
| `GetEndIfScript()` | Done | Empty (not supported) |
| `GetInsertScript()` | Done | INSERT INTO __EFMigrationsHistory |
| `GetDeleteScript()` | Done | DELETE FROM __EFMigrationsHistory |
| `AcquireDatabaseLock()` | Done | No-op lock (single-user) |
| `AcquireDatabaseLockAsync()` | Done | No-op lock (single-user) |
| `InterpretExistsResult()` | Done | Check for non-null result |
| `LockReleaseBehavior` property | Done | Explicit release |
| `ConfigureTable()` | Done | Configure column types |

---

### Phase 8: Database Creation (P1) - COMPLETED

#### 8.1 WitDatabaseCreator

Implemented in: `Storage/WitDatabaseCreator.cs`

| Member | Status | Description |
|--------|--------|-------------|
| `Exists()` | Done | Check if database file exists |
| `ExistsAsync()` | Done | Async version |
| `HasTables()` | Done | Query INFORMATION_SCHEMA.TABLES |
| `HasTablesAsync()` | Done | Async version |
| `Create()` | Done | Open/close connection creates file |
| `CreateAsync()` | Done | Async version |
| `Delete()` | Done | Delete database file |
| `DeleteAsync()` | Done | Async version |

Note: `EnsureCreated()`, `EnsureDeleted()`, `CreateTables()`, `CanConnect()` are provided by base class `RelationalDatabaseCreator`.

---

### Phase 9: Function Translations (P1) - NOT STARTED

#### 9.1 WitMethodCallTranslator

```csharp
public class WitMethodCallTranslator : IMethodCallTranslator
```

**String Functions:**

| C# Method | WitSQL Function |
|-----------|-----------------|
| `string.Length` | `LENGTH()` |
| `string.ToUpper()` | `UPPER()` |
| `string.ToLower()` | `LOWER()` |
| `string.Trim()` | `TRIM()` |
| `string.TrimStart()` | `LTRIM()` |
| `string.TrimEnd()` | `RTRIM()` |
| `string.Substring()` | `SUBSTR()` |
| `string.Replace()` | `REPLACE()` |
| `string.Contains()` | `INSTR() > 0` |
| `string.StartsWith()` | `LIKE 'x%'` |
| `string.EndsWith()` | `LIKE '%x'` |
| `string.IndexOf()` | `INSTR()` |
| `string.Concat()` | `\|\|` or `CONCAT()` |
| `string.IsNullOrEmpty()` | `IS NULL OR = ''` |
| `string.IsNullOrWhiteSpace()` | `IS NULL OR TRIM() = ''` |

**Math Functions:**

| C# Method | WitSQL Function |
|-----------|-----------------|
| `Math.Abs()` | `ABS()` |
| `Math.Ceiling()` | `CEIL()` |
| `Math.Floor()` | `FLOOR()` |
| `Math.Round()` | `ROUND()` |
| `Math.Truncate()` | `TRUNC()` |
| `Math.Pow()` | `POWER()` |
| `Math.Sqrt()` | `SQRT()` |
| `Math.Log()` | `LOG()` |
| `Math.Log10()` | `LOG10()` |
| `Math.Exp()` | `EXP()` |
| `Math.Sin/Cos/Tan()` | `SIN/COS/TAN()` |
| `Math.Max()` | `MAX()` |
| `Math.Min()` | `MIN()` |

**DateTime Functions:**

| C# Property/Method | WitSQL Function |
|--------------------|-----------------|
| `DateTime.Now` | `NOW()` |
| `DateTime.UtcNow` | `NOW()` |
| `DateTime.Today` | `DATE(NOW())` |
| `DateTime.Year` | `YEAR()` |
| `DateTime.Month` | `MONTH()` |
| `DateTime.Day` | `DAY()` |
| `DateTime.Hour` | `HOUR()` |
| `DateTime.Minute` | `MINUTE()` |
| `DateTime.Second` | `SECOND()` |
| `DateTime.Date` | `DATE()` |
| `DateTime.TimeOfDay` | `TIME()` |
| `DateTime.AddDays()` | `DATEADD('day', ...)` |
| `DateTime.AddMonths()` | `DATEADD('month', ...)` |
| `DateTime.AddYears()` | `DATEADD('year', ...)` |

**GUID Functions:**

| C# Method | WitSQL Function |
|-----------|-----------------|
| `Guid.NewGuid()` | `NEWGUID()` |

**Null Functions:**

| C# Expression | WitSQL Function |
|---------------|-----------------|
| `??` | `COALESCE()` |
| `EF.Functions.NullIf()` | `NULLIF()` |

#### 9.2 WitMemberTranslator

```csharp
public class WitMemberTranslator : IMemberTranslator
```

| C# Member | Translation |
|-----------|-------------|
| `string.Length` | `LENGTH(column)` |
| `DateTime.Year/Month/Day/etc.` | Extract functions |
| `TimeSpan.TotalDays/etc.` | Interval calculations |

---

### Phase 10: Advanced Features (P2) - NOT STARTED

#### 10.1 JSON Support

| Feature | Priority | Description |
|---------|----------|-------------|
| `ToJson()` | P2 | Map entity to JSON column |
| `FromJson()` | P2 | Map JSON column to entity |
| JSON path queries | P2 | `JSON_VALUE`, `JSON_QUERY` |
| JSON updates | P2 | `JSON_SET`, `JSON_REMOVE` |

#### 10.2 Computed Columns

| Feature | Priority | Description |
|---------|----------|-------------|
| `HasComputedColumnSql()` | P2 | Define computed column |
| `IsStored()` | P2 | STORED vs VIRTUAL |

#### 10.3 Value Converters

| Feature | Priority | Description |
|---------|----------|-------------|
| Enum to string | P2 | Store enum as TEXT |
| Enum to int | P0 | Store enum as INT |
| JSON serialization | P2 | Complex types as JSON |

#### 10.4 Concurrency

| Feature | Priority | Description |
|---------|----------|-------------|
| `IsRowVersion()` | P1 | ROWVERSION columns |
| `IsConcurrencyToken()` | P1 | Optimistic concurrency |

---

## Service Registration

Current registered services in `WitDbServiceCollectionExtensions.cs`:

```csharp
builder
    // Core services
    .TryAdd<LoggingDefinitions, WitLoggingDefinitions>()
    .TryAdd<IDatabaseProvider, WitDatabaseProvider>()
    
    // Connection and type mapping
    .TryAdd<IRelationalTypeMappingSource, WitTypeMappingSource>()
    .TryAdd<ISqlGenerationHelper, WitSqlGenerationHelper>()
    .TryAdd<IRelationalConnection, WitRelationalConnection>()
    
    // Query generation
    .TryAdd<IQuerySqlGeneratorFactory, WitQuerySqlGeneratorFactory>()
    
    // Update pipeline
    .TryAdd<IUpdateSqlGenerator, WitUpdateSqlGenerator>()
    .TryAdd<IModificationCommandBatchFactory, WitModificationCommandBatchFactory>()
    
    // Model building
    .TryAdd<IRelationalAnnotationProvider, WitAnnotationProvider>()
    .TryAdd<IModelValidator, WitModelValidator>()
    
    // Migrations
    .TryAdd<IMigrationsSqlGenerator, WitMigrationsSqlGenerator>()
    .TryAdd<IHistoryRepository, WitHistoryRepository>()
    
    // Database creation
    .TryAdd<IRelationalDatabaseCreator, WitDatabaseCreator>()
```

---

## Current File Structure

```
OutWit.Database.EntityFramework/
+-- Diagnostics/
|   +-- WitLoggingDefinitions.cs          [Done]
+-- Extensions/
|   +-- WitDbContextOptionsBuilderExtensions.cs  [Done]
|   +-- WitDbServiceCollectionExtensions.cs      [Done]
+-- Infrastructure/
|   +-- WitDbContextOptionsExtension.cs   [Done]
|   +-- WitDbContextOptionsBuilder.cs     [Done]
|   +-- WitDatabaseProvider.cs            [Done]
+-- Metadata/
|   +-- WitAnnotationProvider.cs          [Done]
|   +-- WitModelValidator.cs              [Done]
+-- Migrations/
|   +-- WitHistoryRepository.cs           [Done]
|   +-- WitMigrationsSqlGenerator.cs      [Done]
+-- Query/
|   +-- WitQuerySqlGenerator.cs           [Done]
|   +-- WitQuerySqlGeneratorFactory.cs    [Done]
+-- Storage/
|   +-- WitDatabaseCreator.cs             [Done]
|   +-- WitRelationalConnection.cs        [Done]
|   +-- WitSqlGenerationHelper.cs         [Done]
|   +-- WitTypeMappingSource.cs           [Done]
+-- Update/
|   +-- WitModificationCommandBatchFactory.cs  [Done]
|   +-- WitUpdateSqlGenerator.cs          [Done]
+-- TODO.md
```

---

## Multi-targeting Configuration

The project targets both .NET 9 and .NET 10 with appropriate EF Core versions:

```xml
<PropertyGroup>
  <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
</PropertyGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net9.0'">
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.6" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.0-preview.5.25277.114" />
</ItemGroup>
```

---

## Test Structure

```
OutWit.Database.EntityFramework.Tests/
+-- Extensions/
|   +-- WitDbContextOptionsBuilderExtensionsTests.cs  [Done] (14 tests)
+-- Infrastructure/
|   +-- WitDbContextOptionsExtensionTests.cs          [Done] (16 tests)
|   +-- WitDatabaseProviderTests.cs                   [Done] (3 tests)
+-- Integration/
|   +-- BasicDbContextTests.cs                        (2 tests skipped)
+-- Migrations/
|   +-- WitHistoryRepositoryTests.cs                  [Done] (18 tests)
|   +-- WitMigrationsSqlGeneratorTests.cs             [Done] (18 tests)
+-- Storage/
|   +-- WitDatabaseCreatorTests.cs                    [Done] (12 tests)
|   +-- WitSqlGenerationHelperTests.cs                [Done] (17 tests)
|   +-- WitTypeMappingSourceTests.cs                  [Done] (37 tests)
```

---

## Next Steps

1. **Phase 9:** Implement function translations
   - WitMethodCallTranslator for string, math, datetime functions
   - WitMemberTranslator for property access translations

2. **Enable skipped integration tests**
   - Once full provider works, enable BasicDbContextTests

3. **Phase 10:** Advanced features
   - JSON support
   - Computed columns
   - Concurrency tokens

---

## Dependencies

| Package | Version (net9.0) | Version (net10.0) | Purpose |
|---------|------------------|-------------------|---------|
| Microsoft.EntityFrameworkCore.Relational | 9.0.6 | 10.0.0-preview.5 | EF Core base |
| OutWit.Database.AdoNet | 1.0.0 | 1.0.0 | ADO.NET provider |

---

## See Also

- [EF Core Provider Documentation](https://docs.microsoft.com/en-us/ef/core/providers/)
- [Writing an EF Core Provider](https://docs.microsoft.com/en-us/ef/core/providers/writing-a-provider)
- [EF Core Source Code](https://github.com/dotnet/efcore)
- [SQLite Provider](https://github.com/dotnet/efcore/tree/main/src/EFCore.Sqlite.Core) - Reference implementation
