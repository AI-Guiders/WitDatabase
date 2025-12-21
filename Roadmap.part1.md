# WitDatabase Roadmap - Part 1: Data Types & DDL

**Version:** 1.0  
**Based on:** WitSql.md specification v1.2

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ? | Implemented |
| ?? | Not implemented |
| ?? | Partial |
| N/A | Not applicable for this component |

---

## 1. Data Types (WitSql.md Section 1)

### 1.1 Null Type

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `NULL` type | ? | ? | ?? | §1.1 Null Type |

### 1.2 Integer Types

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `TINYINT` / `INT8` (sbyte) | ? | ? | ?? | §1.2 Integer Types |
| `UTINYINT` / `UINT8` (byte) | ? | ? | ?? | §1.2 Integer Types |
| `SMALLINT` / `INT16` (short) | ? | ? | ?? | §1.2 Integer Types |
| `USMALLINT` / `UINT16` (ushort) | ? | ? | ?? | §1.2 Integer Types |
| `INT` / `INT32` / `INTEGER` (int) | ? | ? | ?? | §1.2 Integer Types |
| `UINT` / `UINT32` (uint) | ? | ? | ?? | §1.2 Integer Types |
| `BIGINT` / `INT64` / `LONG` (long) | ? | ? | ?? | §1.2 Integer Types |
| `UBIGINT` / `UINT64` / `ULONG` (ulong) | ? | ? | ?? | §1.2 Integer Types |
| VarInt encoding for INT/UINT/BIGINT | ? | N/A | ?? | §1.2 Integer Types |

### 1.3 Floating-Point Types

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `FLOAT16` / `HALF` (Half) | ? | ? | ?? | §1.3 Floating-Point Types |
| `FLOAT` / `FLOAT32` / `REAL` (float) | ? | ? | ?? | §1.3 Floating-Point Types |
| `DOUBLE` / `FLOAT64` (double) | ? | ? | ?? | §1.3 Floating-Point Types |
| `DECIMAL` / `MONEY` / `NUMERIC` (decimal) | ? | ? | ?? | §1.3 Floating-Point Types |
| `DECIMAL(precision, scale)` syntax | ? | ? | ?? | §13.2 Extended Type Specs |

### 1.4 Boolean Type

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `BOOLEAN` / `BOOL` (bool) | ? | ? | ?? | §1.4 Boolean Type |
| `TRUE` / `FALSE` literals | ? | ? | ?? | §1.4 Boolean Type |

### 1.5 Date and Time Types

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `DATE` / `DATEONLY` (DateOnly) | ? | ? | ?? | §1.5 Date and Time Types |
| `TIME` / `TIMEONLY` (TimeOnly) | ? | ? | ?? | §1.5 Date and Time Types |
| `DATETIME` / `TIMESTAMP` (DateTime) | ? | ? | ?? | §1.5 Date and Time Types |
| `DATETIMEOFFSET` (DateTimeOffset) | ? | ? | ?? | §1.5 Date and Time Types |
| `INTERVAL` / `TIMESPAN` (TimeSpan) | ? | ? | ?? | §1.5 Date and Time Types |

### 1.6 Unique Identifier

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `GUID` / `UUID` / `UNIQUEIDENTIFIER` (Guid) | ? | ? | ?? | §1.6 Unique Identifier |

### 1.7 String Types

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CHAR(n)` - fixed-length UTF-8 | ? | ? | ?? | §1.7 String Types |
| `VARCHAR(n)` - variable-length UTF-8 | ? | ? | ?? | §1.7 String Types |
| `TEXT` - unlimited UTF-8 | ? | ? | ?? | §1.7 String Types |
| `NCHAR(n)` - alias for CHAR | ? | ? | ?? | §1.7 String Types |
| `NVARCHAR(n)` - alias for VARCHAR | ? | ? | ?? | §1.7 String Types |
| `NTEXT` - alias for TEXT | ? | ? | ?? | §1.7 String Types |

### 1.8 Binary Types

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `BINARY(n)` - fixed-length binary | ? | ? | ?? | §1.8 Binary Types |
| `VARBINARY(n)` - variable-length binary | ? | ? | ?? | §1.8 Binary Types |
| `BLOB` - unlimited binary | ? | ? | ?? | §1.8 Binary Types |

### 1.9 Special Types

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `ROWVERSION` - auto-incrementing version | ?? | ? | ?? | §15.1 Row Version Type |
| `JSON` - JSON document | ?? | ? | ?? | §21.1 JSON Type |
| `JSONB` - binary JSON format | ?? | ? | ?? | §21.1 JSON Type |

---

## 2. DDL Statements - CREATE TABLE (WitSql.md Section 2.1)

### 2.1.1 Basic CREATE TABLE

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE TABLE table_name (...)` | N/A | ? | ?? | §2.1 CREATE TABLE |
| `CREATE TABLE IF NOT EXISTS` | N/A | ? | ?? | §2.1 CREATE TABLE |

### 2.1.2 Column Constraints

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `NOT NULL` | N/A | ? | ?? | §2.1 column_constraint |
| `NULL` | N/A | ? | ?? | §2.1 column_constraint |
| `PRIMARY KEY` | N/A | ? | ?? | §2.1 column_constraint |
| `PRIMARY KEY AUTOINCREMENT` | N/A | ? | ?? | §2.1 column_constraint |
| `UNIQUE` | N/A | ? | ?? | §2.1 column_constraint |
| `DEFAULT literal_value` | N/A | ? | ?? | §2.1 column_constraint |
| `DEFAULT (expression)` | N/A | ? | ?? | §2.1 column_constraint |
| `CHECK (expression)` | N/A | ? | ?? | §2.1 column_constraint |
| `REFERENCES foreign_table(col)` | N/A | ? | ?? | §2.1 column_constraint |
| `ON DELETE action` | N/A | ? | ?? | §2.1 column_constraint |
| `ON UPDATE action` | N/A | ? | ?? | §2.1 column_constraint |
| FK actions: NO ACTION | N/A | ? | ?? | §2.1 action |
| FK actions: RESTRICT | N/A | ? | ?? | §2.1 action |
| FK actions: CASCADE | N/A | ? | ?? | §2.1 action |
| FK actions: SET NULL | N/A | ? | ?? | §2.1 action |
| FK actions: SET DEFAULT | N/A | ? | ?? | §2.1 action |

### 2.1.3 Table Constraints

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `PRIMARY KEY (column_list)` | N/A | ? | ?? | §2.1 table_constraint |
| `UNIQUE (column_list)` | N/A | ? | ?? | §2.1 table_constraint |
| `FOREIGN KEY (cols) REFERENCES ...` | N/A | ? | ?? | §2.1 table_constraint |
| `CHECK (expression)` | N/A | ? | ?? | §2.1 table_constraint |
| `CONSTRAINT name ...` (named constraints) | N/A | ?? | ?? | §13.3 Named Constraints |

### 2.1.4 Computed Columns

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `column AS (expression)` | N/A | ?? | ?? | §20 Computed Columns |
| `STORED` modifier | N/A | ?? | ?? | §20 Computed Columns |
| `VIRTUAL` modifier | N/A | ?? | ?? | §20 Computed Columns |

---

## 3. DDL Statements - DROP TABLE (WitSql.md Section 2.2)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `DROP TABLE table_name` | N/A | ? | ?? | §2.2 DROP TABLE |
| `DROP TABLE IF EXISTS` | N/A | ? | ?? | §2.2 DROP TABLE |

---

## 4. DDL Statements - ALTER TABLE (WitSql.md Section 2.3)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `ALTER TABLE ... ADD [COLUMN] ...` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... DROP [COLUMN] ...` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... RENAME TO ...` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... RENAME [COLUMN] ... TO ...` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... ALTER [COLUMN] ... SET DEFAULT` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... ALTER [COLUMN] ... DROP DEFAULT` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... ALTER [COLUMN] ... SET NOT NULL` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... ALTER [COLUMN] ... DROP NOT NULL` | N/A | ? | ?? | §2.3 ALTER TABLE |
| `ALTER TABLE ... DROP CONSTRAINT name` | N/A | ?? | ?? | §13.3 Named Constraints |

---

## 5. DDL Statements - CREATE INDEX (WitSql.md Section 2.4)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE INDEX name ON table (cols)` | ?? | ? | ?? | §2.4 CREATE INDEX |
| `CREATE UNIQUE INDEX ...` | ?? | ? | ?? | §2.4 CREATE INDEX |
| `CREATE INDEX IF NOT EXISTS ...` | ?? | ? | ?? | §2.4 CREATE INDEX |
| `ASC` / `DESC` column order | ?? | ? | ?? | §2.4 CREATE INDEX |
| `WHERE condition` (partial/filtered index) | ?? | ?? | ?? | §19.1 Partial Indexes |
| Expression indexes `(LOWER(col))` | ?? | ?? | ?? | §19.2 Expression Indexes |
| `INCLUDE (cols)` (covering index) | ?? | ?? | ?? | §19.3 Covering Indexes |

---

## 6. DDL Statements - DROP INDEX (WitSql.md Section 2.5)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `DROP INDEX index_name` | N/A | ? | ?? | §2.5 DROP INDEX |
| `DROP INDEX IF EXISTS` | N/A | ? | ?? | §2.5 DROP INDEX |

---

## 7. DDL Statements - CREATE VIEW (WitSql.md Section 2.6)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE VIEW name AS SELECT ...` | N/A | ? | ?? | §2.6 CREATE VIEW |
| `CREATE VIEW IF NOT EXISTS ...` | N/A | ? | ?? | §2.6 CREATE VIEW |
| `CREATE VIEW name (cols) AS ...` | N/A | ? | ?? | §2.6 CREATE VIEW |

---

## 8. DDL Statements - DROP VIEW (WitSql.md Section 2.7)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `DROP VIEW view_name` | N/A | ? | ?? | §2.7 DROP VIEW |
| `DROP VIEW IF EXISTS` | N/A | ? | ?? | §2.7 DROP VIEW |

---

## 9. DDL Statements - CREATE TRIGGER (WitSql.md Section 2.8)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE TRIGGER name BEFORE ...` | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| `CREATE TRIGGER name AFTER ...` | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| `CREATE TRIGGER name INSTEAD OF ...` | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| `CREATE TRIGGER IF NOT EXISTS ...` | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| Trigger events: `INSERT` | N/A | ? | ?? | §2.8 Trigger Events |
| Trigger events: `UPDATE` | N/A | ? | ?? | §2.8 Trigger Events |
| Trigger events: `UPDATE OF col_list` | N/A | ? | ?? | §2.8 Trigger Events |
| Trigger events: `DELETE` | N/A | ? | ?? | §2.8 Trigger Events |
| `FOR EACH ROW` | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| `WHEN (condition)` | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| `BEGIN ... END` body | N/A | ? | ?? | §2.8 CREATE TRIGGER |
| `OLD.column_name` pseudo-table | N/A | ? | ?? | §2.8 OLD/NEW Pseudo-Tables |
| `NEW.column_name` pseudo-table | N/A | ? | ?? | §2.8 OLD/NEW Pseudo-Tables |
| `SET NEW.column = value` | N/A | ? | ?? | §2.8 OLD/NEW Pseudo-Tables |
| `SIGNAL SQLSTATE ...` (errors) | N/A | ?? | ?? | §2.8 Examples |

---

## 10. DDL Statements - DROP TRIGGER (WitSql.md Section 2.9)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `DROP TRIGGER trigger_name` | N/A | ? | ?? | §2.9 DROP TRIGGER |
| `DROP TRIGGER IF EXISTS` | N/A | ? | ?? | §2.9 DROP TRIGGER |

---

## 11. DDL Statements - SEQUENCE (WitSql.md Section 5.5)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE SEQUENCE name START WITH n` | N/A | ? | ?? | §5.5 ID Generation Functions |
| `ALTER SEQUENCE name RESTART WITH n` | N/A | ? | ?? | §5.5 ID Generation Functions |
| `DROP SEQUENCE name` | N/A | ? | ?? | §5.5 ID Generation Functions |
| `INCREMENT(sequence)` function | N/A | ? | ?? | §5.5 ID Generation Functions |
| `LASTINCREMENT(sequence)` function | N/A | ? | ?? | §5.5 ID Generation Functions |

---

## Summary - Part 1

| Category | Total | Core ? | Parser ? | Notes |
|----------|-------|---------|-----------|-------|
| Basic Types | 26 | 24 | 26 | ROWVERSION, JSON/JSONB missing in Core |
| CREATE TABLE | 22 | 0 | 19 | Named constraints, computed columns missing |
| DROP TABLE | 2 | 0 | 2 | ? Complete |
| ALTER TABLE | 9 | 0 | 8 | DROP CONSTRAINT missing |
| CREATE INDEX | 7 | 0 | 5 | Partial, expression, covering missing |
| DROP INDEX | 2 | 0 | 2 | ? Complete |
| CREATE VIEW | 3 | 0 | 3 | ? Complete |
| DROP VIEW | 2 | 0 | 2 | ? Complete |
| CREATE TRIGGER | 14 | 0 | 12 | SIGNAL missing |
| DROP TRIGGER | 2 | 0 | 2 | ? Complete |
| SEQUENCE | 5 | 0 | 5 | ? Complete |

---

*Continue to [Roadmap.part2.md](Roadmap.part2.md) for DML Statements (SELECT, INSERT, UPDATE, DELETE)*
