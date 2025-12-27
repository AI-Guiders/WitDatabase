# ALTER TABLE Implementation TODO

**Created:** 2025-01-29  
**Status:** Not Started  
**Priority:** P0 (Required for EF Core migrations)

---

## Overview

ALTER TABLE is critical for EF Core migrations. Three main features need implementation:

1. **ADD CONSTRAINT** - Add named constraints to existing table
2. **DROP CONSTRAINT** - Remove named constraints
3. **ADD COLUMN with DEFAULT** - Populate existing rows with default value

---

## 1. ALTER TABLE ADD CONSTRAINT (P0)

### Current State
- Parser: ? Supported (`AlterActionAddConstraint`)
- Engine: ? Not implemented (throws `NotSupportedException`)

### Constraint Types to Support

| Constraint Type | SQL Example | Implementation Complexity |
|----------------|-------------|--------------------------|
| PRIMARY KEY | `ADD CONSTRAINT pk_name PRIMARY KEY (cols)` | High - requires data migration |
| UNIQUE | `ADD CONSTRAINT uq_name UNIQUE (cols)` | Medium - needs index creation |
| FOREIGN KEY | `ADD CONSTRAINT fk_name FOREIGN KEY (cols) REFERENCES ...` | Medium |
| CHECK | `ADD CONSTRAINT chk_name CHECK (expr)` | Low - metadata only + validation |

### Implementation Steps

#### Step 1.1: Update `DefinitionTable` for Named Constraints
- [ ] Add `NamedConstraints` property: `IReadOnlyList<DefinitionNamedConstraint>?`
- [ ] Create `DefinitionNamedConstraint` class with:
  ```csharp
  public sealed class DefinitionNamedConstraint
  {
      public required string Name { get; init; }
      public required ConstraintType Type { get; init; } // PrimaryKey, Unique, ForeignKey, Check
      public IReadOnlyList<string>? Columns { get; init; }
      public string? CheckExpression { get; init; }
      public DefinitionForeignKey? ForeignKey { get; init; }
  }
  ```
- [ ] Update `DefinitionTable.Is()` and `Clone()` methods

#### Step 1.2: Implement `IDatabase.AddConstraint()`
- [ ] Add method to `IDatabase` interface:
  ```csharp
  void AddConstraint(string tableName, DefinitionNamedConstraint constraint);
  ```
- [ ] Implement in `WitSqlEngine`:
  - For CHECK: Validate all existing rows, add to metadata
  - For UNIQUE: Create unique index, validate no duplicates exist
  - For FOREIGN KEY: Validate all existing values reference valid rows
  - For PRIMARY KEY: Complex - requires data restructuring

#### Step 1.3: Update `StatementExecutor.Ddl.cs`
- [ ] Handle `AlterActionAddConstraint` case:
  ```csharp
  case AlterActionAddConstraint addConstraint:
      ExecuteAddConstraint(alterTable.TableName, addConstraint);
      break;
  ```
- [ ] Implement `ExecuteAddConstraint()` method

#### Step 1.4: Constraint Validation
- [ ] For CHECK: Scan all rows, evaluate expression, throw if any fails
- [ ] For UNIQUE: Check for duplicates before creating index
- [ ] For FOREIGN KEY: Validate referential integrity

#### Step 1.5: Tests
- [ ] `AlterTableAddCheckConstraintTest()`
- [ ] `AlterTableAddUniqueConstraintTest()`
- [ ] `AlterTableAddUniqueConstraintOnDuplicatesThrowsTest()`
- [ ] `AlterTableAddForeignKeyConstraintTest()`
- [ ] `AlterTableAddForeignKeyWithInvalidDataThrowsTest()`
- [ ] `AlterTableAddCheckConstraintWithInvalidDataThrowsTest()`

---

## 2. ALTER TABLE DROP CONSTRAINT (P0)

### Current State
- Parser: ? Supported (`AlterActionDropConstraint`)
- Engine: ? Not implemented (throws `NotSupportedException`)

### Implementation Steps

#### Step 2.1: Implement `IDatabase.DropConstraint()`
- [ ] Add method to `IDatabase` interface:
  ```csharp
  void DropConstraint(string tableName, string constraintName);
  ```
- [ ] Implement in `WitSqlEngine`:
  - Find constraint by name in table metadata
  - For UNIQUE: Drop associated index
  - For CHECK/FOREIGN KEY: Remove from metadata
  - For PRIMARY KEY: Not supported (would require table rebuild)

#### Step 2.2: Update `StatementExecutor.Ddl.cs`
- [ ] Handle `AlterActionDropConstraint` case (already exists but may throw)
- [ ] Call `m_context.Database.DropConstraint()`

#### Step 2.3: Tests
- [ ] `AlterTableDropCheckConstraintTest()`
- [ ] `AlterTableDropUniqueConstraintTest()`
- [ ] `AlterTableDropForeignKeyConstraintTest()`
- [ ] `AlterTableDropNonExistentConstraintThrowsTest()`
- [ ] `AlterTableDropPrimaryKeyThrowsTest()` (not supported)

---

## 3. ALTER TABLE ADD COLUMN with DEFAULT (P0)

### Current State
- Parser: ? Supported
- Engine: ?? Partial - adds column but doesn't populate existing rows

### Current Problem (from test)
```csharp
[Ignore("ALTER TABLE ADD COLUMN with DEFAULT does not yet populate existing rows")]
public void AlterTableAddColumnWithDefaultPopulatesExistingRowsTest()
```

### Implementation Steps

#### Step 3.1: Update `AddColumn()` Logic
- [ ] After adding column metadata, scan existing rows
- [ ] For each row, set new column to default value
- [ ] Update stored row data

#### Step 3.2: Implement Row Population
```csharp
private void PopulateNewColumnWithDefault(string tableName, DefinitionColumn column)
{
    if (column.DefaultValue == null)
        return; // NULL is implicit default
    
    var table = m_schema.GetTable(tableName);
    var defaultExpr = WitSql.ParseExpression(column.DefaultValue);
    var evaluator = new ExpressionEvaluator(new ContextExecution { Database = this });
    
    // Evaluate default value once (if deterministic)
    var defaultValue = evaluator.Evaluate(defaultExpr, new WitSqlRow([], []));
    
    // Scan and update all existing rows
    var tablePrefix = SchemaCatalog.GetTableDataPrefix(tableName);
    var endPrefix = SchemaCatalog.GetTableDataEndPrefix(tableName);
    
    foreach (var (key, value) in m_database.Scan(tablePrefix, endPrefix))
    {
        var row = table.DeserializeRow(value);
        // Add new column with default value
        var newRow = AddColumnToRow(row, column.Name, defaultValue);
        var newValue = table.SerializeRow(newRow);
        m_database.Put(key, newValue);
    }
}
```

#### Step 3.3: Handle Non-Deterministic Defaults
- [ ] `NOW()`, `NEWGUID()` - evaluate per row
- [ ] `INCREMENT()` - evaluate per row

#### Step 3.4: Update Serialization
- [ ] Ensure row serialization handles new column schema
- [ ] Handle schema migration (old rows without new column)

#### Step 3.5: Tests
- [ ] Enable ignored test: `AlterTableAddColumnWithDefaultPopulatesExistingRowsTest()`
- [ ] `AlterTableAddColumnWithNullDefaultTest()`
- [ ] `AlterTableAddNotNullColumnWithDefaultTest()`
- [ ] `AlterTableAddColumnWithExpressionDefaultTest()` (e.g., `NOW()`)
- [ ] `AlterTableAddColumnOnEmptyTableTest()`

---

## 4. Computed Columns in ALTER TABLE (P2)

### Current State
- Parser: ? Supported (`ComputedExpression`, `IsStored`)
- Engine: ? Not implemented for ALTER TABLE

### Implementation Steps (Deferred)

#### Step 4.1: Stored Computed Columns
- [ ] Add column metadata with computed expression
- [ ] Evaluate expression for all existing rows
- [ ] Store computed values

#### Step 4.2: Virtual Computed Columns
- [ ] Add column metadata with computed expression
- [ ] Evaluate on-the-fly during query execution
- [ ] No storage needed

#### Step 4.3: Index on Computed Columns
- [ ] Allow creating index on stored computed column
- [ ] Expression index effectively does this already

---

## File Changes Summary

### Files to Modify

| File | Changes |
|------|---------|
| `Definitions/DefinitionTable.cs` | Add `NamedConstraints` property |
| `Definitions/DefinitionNamedConstraint.cs` | **NEW** - Named constraint definition |
| `Interfaces/IDatabase.cs` | Add `AddConstraint()`, `DropConstraint()` methods |
| `WitSqlEngine.Ddl.Tables.cs` | Implement constraint methods |
| `WitSqlEngine.Dml.cs` | Update `AddColumn()` to populate defaults |
| `Statements/StatementExecutor.Ddl.cs` | Handle `AddConstraint`, `DropConstraint` |
| `Schema/SchemaCatalog.cs` | Persist named constraints |

### Files to Create

| File | Purpose |
|------|---------|
| `Definitions/DefinitionNamedConstraint.cs` | Named constraint model |
| `Tests/WitSqlEngineAlterTableConstraintTests.cs` | Constraint tests |

---

## Dependencies

```
DefinitionNamedConstraint  <--  DefinitionTable
        |
        v
IDatabase.AddConstraint()  -->  WitSqlEngine
        |
        v
StatementExecutor.ExecuteAddConstraint()
        |
        v
Constraint Validation (scan existing data)
```

---

## Test Plan

### Unit Tests
- [ ] `DefinitionNamedConstraintTests.cs` - Model tests

### Integration Tests (in `WitSqlEngineAlterTableConstraintTests.cs`)
- [ ] ADD CONSTRAINT tests (6)
- [ ] DROP CONSTRAINT tests (5)
- [ ] ADD COLUMN with DEFAULT tests (5)

### Regression Tests
- [ ] Verify existing ALTER TABLE tests still pass
- [ ] Verify CREATE TABLE with constraints still works

---

## Implementation Order

1. **Week 1**: ADD COLUMN with DEFAULT (smallest scope, unblocks EF Core)
   - Update `AddColumn()` logic
   - Implement row population
   - Enable ignored test

2. **Week 2**: DROP CONSTRAINT
   - Add `DropConstraint()` method
   - Handle different constraint types
   - Tests

3. **Week 3**: ADD CONSTRAINT (most complex)
   - Create `DefinitionNamedConstraint`
   - Implement validation logic
   - Handle each constraint type
   - Tests

---

## EF Core Migration Patterns

EF Core generates migrations like:

```sql
-- Adding a new column with default
ALTER TABLE "Products" ADD "CreatedAt" DATETIME NOT NULL DEFAULT NOW();

-- Adding a unique constraint
ALTER TABLE "Users" ADD CONSTRAINT "UQ_Users_Email" UNIQUE ("Email");

-- Adding a foreign key
ALTER TABLE "Orders" ADD CONSTRAINT "FK_Orders_Users"
    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id")
    ON DELETE CASCADE;

-- Dropping a constraint
ALTER TABLE "Users" DROP CONSTRAINT "UQ_Users_Email";
```

All these patterns must be supported for EF Core migrations to work.

---

## Notes

- PRIMARY KEY constraint cannot be added to existing table (would require table rebuild)
- NOT NULL constraint with DEFAULT should be handled carefully (set default, then set not null)
- Unique constraint creates an implicit unique index
- Consider transaction support for large table modifications

---

**Last Updated:** 2025-01-29
