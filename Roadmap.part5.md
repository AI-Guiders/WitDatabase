# WitDatabase Roadmap - Part 5: Window Functions, Transactions, and Advanced Features

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

## 1. Window Functions (WitSql.md Section 7)

### 1.1 OVER Clause

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `OVER ()` - empty window | N/A | ? | ?? | §7 Window Functions |
| `OVER (PARTITION BY expr)` | N/A | ? | ?? | §7 Window Functions |
| `OVER (ORDER BY expr)` | N/A | ? | ?? | §7 Window Functions |
| `OVER (PARTITION BY ... ORDER BY ...)` | N/A | ? | ?? | §7 Window Functions |

### 1.2 Frame Clause

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `ROWS frame_clause` | N/A | ? | ?? | §7 frame_clause |
| `RANGE frame_clause` | N/A | ? | ?? | §7 frame_clause |
| `UNBOUNDED PRECEDING` | N/A | ? | ?? | §7 frame_start/end |
| `n PRECEDING` | N/A | ? | ?? | §7 frame_start/end |
| `CURRENT ROW` | N/A | ? | ?? | §7 frame_start/end |
| `n FOLLOWING` | N/A | ? | ?? | §7 frame_start/end |
| `UNBOUNDED FOLLOWING` | N/A | ? | ?? | §7 frame_start/end |
| `BETWEEN frame_start AND frame_end` | N/A | ? | ?? | §7 frame_clause |

### 1.3 Ranking Functions

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `ROW_NUMBER()` | N/A | ? | ?? | §7.1 Ranking Functions |
| `RANK()` | N/A | ? | ?? | §7.1 Ranking Functions |
| `DENSE_RANK()` | N/A | ? | ?? | §7.1 Ranking Functions |
| `NTILE(n)` | N/A | ? | ?? | §7.1 Ranking Functions |
| `PERCENT_RANK()` | N/A | ? | ?? | §7.1 Ranking Functions |
| `CUME_DIST()` | N/A | ? | ?? | §7.1 Ranking Functions |

### 1.4 Value Functions

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `FIRST_VALUE(expr)` | N/A | ? | ?? | §7.2 Value Functions |
| `LAST_VALUE(expr)` | N/A | ? | ?? | §7.2 Value Functions |
| `NTH_VALUE(expr, n)` | N/A | ? | ?? | §7.2 Value Functions |
| `LAG(expr, offset, default)` | N/A | ? | ?? | §7.2 Value Functions |
| `LEAD(expr, offset, default)` | N/A | ? | ?? | §7.2 Value Functions |

---

## 2. Transaction Statements (WitSql.md Section 9)

### 2.1 Basic Transactions

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `BEGIN [TRANSACTION]` | ? | ? | ?? | §9 Transactions |
| `COMMIT` | ? | ? | ?? | §9 Transactions |
| `ROLLBACK` | ? | ? | ?? | §9 Transactions |

### 2.2 Savepoints

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `SAVEPOINT savepoint_name` | ?? | ? | ?? | §9 Savepoints |
| `RELEASE SAVEPOINT savepoint_name` | ?? | ? | ?? | §9 Savepoints |
| `ROLLBACK TO SAVEPOINT savepoint_name` | ?? | ? | ?? | §9 Savepoints |

### 2.3 Isolation Levels

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `SET TRANSACTION ISOLATION LEVEL ...` | ?? | ?? | ?? | §14.1 Isolation Levels |
| `READ UNCOMMITTED` | ?? | ?? | ?? | §14.1 Isolation Levels |
| `READ COMMITTED` | ?? | ?? | ?? | §14.1 Isolation Levels |
| `REPEATABLE READ` | ?? | ?? | ?? | §14.1 Isolation Levels |
| `SERIALIZABLE` | ?? | ?? | ?? | §14.1 Isolation Levels |
| `SNAPSHOT` | ?? | ?? | ?? | §14.1 Isolation Levels |

---

## 3. User-Defined Functions (WitSql.md Section 22)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE FUNCTION name (params) RETURNS type AS BEGIN ... END` | N/A | ?? | ?? | §22.1 Scalar Functions |
| `RETURNS TABLE (columns)` | N/A | ?? | ?? | §22.2 Table-Valued Functions |
| `DETERMINISTIC` modifier | N/A | ?? | ?? | §22.1 Scalar Functions |
| `RETURN expression` | N/A | ?? | ?? | §22.1 Scalar Functions |
| `DROP FUNCTION [IF EXISTS] name` | N/A | ?? | ?? | §22 User-Defined Functions |

---

## 4. Stored Procedures (WitSql.md Section 23)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE PROCEDURE name (params) AS BEGIN ... END` | N/A | ?? | ?? | §23 Stored Procedures |
| `DROP PROCEDURE [IF EXISTS] name` | N/A | ?? | ?? | §23 Stored Procedures |
| `CALL procedure_name(args)` | N/A | ?? | ?? | §23 Stored Procedures |
| `EXECUTE procedure_name(args)` | N/A | ?? | ?? | §23 Stored Procedures |

---

## 5. EXPLAIN / Query Analysis (WitSql.md Section 25)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `EXPLAIN select_statement` | N/A | ?? | ?? | §25.1 EXPLAIN |
| `EXPLAIN ANALYZE select_statement` | N/A | ?? | ?? | §25.1 EXPLAIN |
| `EXPLAIN (FORMAT JSON) ...` | N/A | ?? | ?? | §25.1 EXPLAIN |
| `EXPLAIN (FORMAT TEXT) ...` | N/A | ?? | ?? | §25.1 EXPLAIN |

---

## 6. Database Administration (WitSql.md Section 26)

### 6.1 Database Commands

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CREATE DATABASE database_name` | N/A | ?? | ?? | §26.1 Database Commands |
| `DROP DATABASE [IF EXISTS] database_name` | N/A | ?? | ?? | §26.1 Database Commands |
| `ATTACH DATABASE 'path' AS alias` | N/A | ?? | ?? | §26.1 Database Commands |
| `DETACH DATABASE alias` | N/A | ?? | ?? | §26.1 Database Commands |

### 6.2 Maintenance Commands

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `VACUUM` | ?? | ?? | ?? | §26.2 Maintenance Commands |
| `VACUUM table_name` | ?? | ?? | ?? | §26.2 Maintenance Commands |
| `ANALYZE` | ?? | ?? | ?? | §26.2 Maintenance Commands |
| `ANALYZE table_name` | ?? | ?? | ?? | §26.2 Maintenance Commands |
| `PRAGMA integrity_check` | N/A | ?? | ?? | §26.2 Maintenance Commands |

### 6.3 PRAGMA Statements

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `PRAGMA setting_name` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA setting_name = value` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA page_size` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA cache_size` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA journal_mode` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA synchronous` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA foreign_keys` | N/A | ?? | ?? | §26.3 PRAGMA Statements |
| `PRAGMA auto_vacuum` | N/A | ?? | ?? | §26.3 PRAGMA Statements |

---

## 7. Schema Information (WitSql.md Section 13)

### 7.1 INFORMATION_SCHEMA Views

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `INFORMATION_SCHEMA.TABLES` | N/A | N/A | ?? | §13.1 INFORMATION_SCHEMA |
| `INFORMATION_SCHEMA.COLUMNS` | N/A | N/A | ?? | §13.1 INFORMATION_SCHEMA |
| `INFORMATION_SCHEMA.KEY_COLUMN_USAGE` | N/A | N/A | ?? | §13.1 INFORMATION_SCHEMA |
| `INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS` | N/A | N/A | ?? | §13.1 INFORMATION_SCHEMA |
| `INFORMATION_SCHEMA.INDEXES` | N/A | N/A | ?? | §13.1 INFORMATION_SCHEMA |
| `INFORMATION_SCHEMA.VIEWS` | N/A | N/A | ?? | §13.1 INFORMATION_SCHEMA |

---

## 8. Comments (WitSql.md Section 10)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `-- single line comment` | N/A | ? | N/A | §10 Comments |
| `/* multi-line comment */` | N/A | ? | N/A | §10 Comments |

---

## 9. Multiple Result Sets (WitSql.md Section 28)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `BEGIN ... END` with multiple SELECT | ?? | ?? | ?? | §28 Multiple Result Sets |
| `IMultiResultReader` interface | ?? | N/A | ?? | §28 Multiple Result Sets |
| `NextResult()` method | ?? | N/A | ?? | §28 Multiple Result Sets |

---

## Summary - Part 5

| Category | Total | Core ? | Parser ? | Notes |
|----------|-------|---------|-----------|-------|
| OVER Clause | 4 | 0 | 4 | ? Parser complete |
| Frame Clause | 8 | 0 | 8 | ? Parser complete |
| Ranking Functions | 6 | 0 | 6 | ? Parser complete |
| Value Functions | 5 | 0 | 5 | ? Parser complete |
| Basic Transactions | 3 | 3 | 3 | ? Complete |
| Savepoints | 3 | 0 | 3 | Core missing |
| Isolation Levels | 6 | 0 | 0 | All missing |
| User-Defined Functions | 5 | 0 | 0 | All missing |
| Stored Procedures | 4 | 0 | 0 | All missing |
| EXPLAIN | 4 | 0 | 0 | All missing |
| Database Commands | 4 | 0 | 0 | All missing |
| Maintenance Commands | 5 | 0 | 0 | All missing |
| PRAGMA | 8 | 0 | 0 | All missing |
| INFORMATION_SCHEMA | 6 | 0 | 0 | Engine only |
| Comments | 2 | 0 | 2 | ? Parser complete |
| Multiple Result Sets | 3 | 0 | 0 | All missing |

---

*Continue to [Roadmap.part6.md](Roadmap.part6.md) for Core Engine Components*
