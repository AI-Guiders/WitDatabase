# OutWit.Database (Engine) - Roadmap

**Version:** 2.8  
**Based on:** WitSql.md specification v1.2  
**Last Updated:** 2025-02-05

---

## Legend

| Symbol | Meaning |
|--------|---------|
| [x] | Implemented |
| [ ] | Not implemented |

**Priority Legend:**
- **P0** = Critical (required for basic functionality)
- **P1** = Important (production-ready features)
- **P2** = Optional (nice-to-have features)

**Version Legend:**
- **v1** = First release
- **v2** = Deferred to second release

---

## Progress Summary

**Current Status: ~97% v1 Complete - ADO.NET Provider Not Started**

The Engine component (`OutWit.Database`) is responsible for:
- SQL execution against the Core storage layer
- Query planning and optimization
- Type system implementation
- Function evaluation
- ADO.NET provider implementation

### v1 Completed Components
- `WitSqlType` - Runtime SQL type enumeration
- `WitSqlValue` - Variant type for SQL values with type coercion
- `WitSqlRow` - Row representation with column lookup
- `WitSqlResult` - Query result container
- `WitSqlColumnInfo` - Column schema information
- `WitDataType` - Storage type enumeration
- `ExpressionEvaluator` - Full expression evaluation including subqueries
- `AggregateExpressionEvaluator` - Aggregate function evaluation in GROUP BY context
- `StatementExecutor` - DDL/DML execution with triggers and validation
- `QueryPlanner` - Query plan building with iterator model
- All iterator types (Filter, Project, Sort, Limit, Distinct, Join, GroupBy, Union, Intersect, Except, Alias, Locking, Window)
- Subquery support (Scalar, EXISTS, IN, ANY/SOME/ALL, Correlated)
- All scalar functions (60+)
- All aggregate functions
- **JSON Functions** (12 functions)
- Constraint validation (NOT NULL, UNIQUE, CHECK, FOREIGN KEY)
- Trigger execution (BEFORE/AFTER/INSTEAD OF)
- **Transaction support** (BEGIN/COMMIT/ROLLBACK, Isolation Levels, Savepoints)
- **FOR UPDATE / FOR SHARE** locking hints with NOWAIT/SKIP LOCKED
- **Index Implementation** (Index seek, range scan, auto-update, partial indexes, expression indexes, covering indexes)
- **ALTER TABLE** (ADD/DROP CONSTRAINT, ADD COLUMN with DEFAULT)
- **Computed Columns** (STORED with auto-recalculation, VIRTUAL with on-the-fly evaluation)
- **CTE Execution** (Simple CTEs, Multiple CTEs, Recursive CTEs, Caching)
- **Window Functions** (All ranking, value, aggregate functions with Frame clause)
- **DML Enhancements** (RETURNING clause, INSERT OR REPLACE, ON CONFLICT, TRUNCATE, MERGE)
- **UPDATE ... FROM** (join-based updates)
- **DELETE ... USING** (join-based deletes)
- **ROWVERSION** (auto-increment on INSERT/UPDATE)
- **INFORMATION_SCHEMA** views (7 views)
- **EXPLAIN / EXPLAIN QUERY PLAN**
- **Query Optimization** (Index selection, Join ordering, Plan caching)

### v1 Not Started
- ADO.NET Provider (WitDbConnection, WitDbCommand, WitDbDataReader, etc.)

---

## 1. Query Execution Infrastructure (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| Query executor interface | [x] | P0 | v1 |
| AST to execution plan converter | [x] | P0 | v1 |
| Expression evaluator | [x] | P0 | v1 |
| Aggregate expression evaluator | [x] | P0 | v1 |
| Type coercion system | [x] | P0 | v1 |
| Result set builder | [x] | P0 | v1 |
| Query context (AffectedRows, LastInsertId) | [x] | P0 | v1 |
| Parameter binding | [x] | P0 | v1 |
| Query timeout support | [x] | P1 | v1 |
| CancellationToken support | [x] | P0 | v1 |

---

## 2. Data Type Implementation (100%)

| Type | Status | Priority | Version |
|------|--------|----------|---------|
| NULL handling | [x] | P0 | v1 |
| Integer types (TINYINT to BIGINT) | [x] | P0 | v1 |
| Floating types (FLOAT16, FLOAT, DOUBLE, DECIMAL) | [x] | P0 | v1 |
| BOOLEAN | [x] | P0 | v1 |
| Date/Time (DATE, TIME, DATETIME, DATETIMEOFFSET, INTERVAL) | [x] | P0 | v1 |
| String (CHAR, VARCHAR, TEXT) | [x] | P0 | v1 |
| Binary (BINARY, VARBINARY, BLOB) | [x] | P0 | v1 |
| GUID | [x] | P0 | v1 |
| ROWVERSION | [x] | P1 | v1 |
| JSON / JSONB | [x] | P1 | v1 |

---

## 3. DDL Execution (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| CREATE TABLE | [x] | P0 | v1 |
| DROP TABLE | [x] | P0 | v1 |
| ALTER TABLE (all operations) | [x] | P0 | v1 |
| CREATE/DROP INDEX | [x] | P0 | v1 |
| Partial/Expression/Covering indexes | [x] | P1 | v1 |
| CREATE/DROP VIEW | [x] | P1 | v1 |
| CREATE/DROP TRIGGER | [x] | P1 | v1 |
| CREATE/ALTER/DROP SEQUENCE | [x] | P0 | v1 |
| Constraint validation | [x] | P0 | v1 |
| Computed columns (STORED/VIRTUAL) | [x] | P2 | v1 |

---

## 4. DML Execution (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| SELECT (all clauses) | [x] | P0 | v1 |
| JOINs (INNER, LEFT, RIGHT, FULL, CROSS) | [x] | P0 | v1 |
| INSERT (VALUES, SELECT, RETURNING) | [x] | P0 | v1 |
| INSERT OR REPLACE / ON CONFLICT | [x] | P1 | v1 |
| UPDATE (WHERE, FROM, RETURNING) | [x] | P0 | v1 |
| DELETE (WHERE, USING, RETURNING) | [x] | P0 | v1 |
| TRUNCATE TABLE | [x] | P1 | v1 |
| MERGE | [x] | P1 | v1 |
| Cascading deletes | [x] | P1 | v1 |

---

## 5. Expression Evaluation (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| All comparison operators | [x] | P0 | v1 |
| Logical operators (AND, OR, NOT) | [x] | P0 | v1 |
| Arithmetic operators | [x] | P0 | v1 |
| String/Bitwise operators | [x] | P0 | v1 |
| BETWEEN, IN, LIKE, GLOB | [x] | P0 | v1 |
| IS NULL / IS NOT NULL | [x] | P0 | v1 |
| CASE, COALESCE, NULLIF, IIF | [x] | P0 | v1 |
| CAST / type conversion | [x] | P0 | v1 |
| Subquery operators (EXISTS, IN, ANY, ALL) | [x] | P0 | v1 |

---

## 6. Built-in Functions (100%)

| Category | Status | Count |
|----------|--------|-------|
| Aggregate functions | [x] | 6 |
| String functions | [x] | 20+ |
| Numeric functions | [x] | 20+ |
| Date/Time functions | [x] | 15+ |
| Conversion functions | [x] | 15+ |
| Null handling | [x] | 4 |
| ID generation | [x] | 4 |
| System functions | [x] | 6 |
| JSON functions | [x] | 12 |
| **Total** | [x] | **60+** |

---

## 7. Window Functions (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| OVER clause (PARTITION BY, ORDER BY) | [x] | P1 | v1 |
| Frame clause (ROWS/RANGE BETWEEN) | [x] | P1 | v1 |
| ROW_NUMBER, RANK, DENSE_RANK, NTILE | [x] | P1 | v1 |
| PERCENT_RANK, CUME_DIST | [x] | P1 | v1 |
| LAG, LEAD (with offset/default) | [x] | P1 | v1 |
| FIRST_VALUE, LAST_VALUE, NTH_VALUE | [x] | P1 | v1 |
| Aggregate OVER (SUM, AVG, COUNT, MIN, MAX) | [x] | P1 | v1 |

---

## 8. CTE and Set Operations (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| WITH clause | [x] | P1 | v1 |
| Multiple CTEs | [x] | P1 | v1 |
| Recursive CTE | [x] | P1 | v1 |
| CTE Caching | [x] | P1 | v1 |
| UNION / UNION ALL | [x] | P0 | v1 |
| INTERSECT / EXCEPT | [x] | P1 | v1 |

---

## 9. Transaction Support (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| BEGIN/COMMIT/ROLLBACK | [x] | P0 | v1 |
| SAVEPOINT | [x] | P1 | v1 |
| Isolation levels | [x] | P0 | v1 |
| FOR UPDATE / FOR SHARE | [x] | P1 | v1 |
| NOWAIT / SKIP LOCKED | [x] | P1 | v1 |

---

## 10. Index Implementation (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| Index seek (equality) | [x] | P0 | v1 |
| Index range scan | [x] | P0 | v1 |
| Index auto-update on DML | [x] | P0 | v1 |
| Partial indexes (WHERE) | [x] | P1 | v1 |
| Expression indexes | [x] | P1 | v1 |
| Covering indexes (INCLUDE) | [x] | P1 | v1 |

---

## 11. Query Optimization (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| Cost-based index selection | [x] | P1 | v1 |
| Join order optimization | [x] | P1 | v1 |
| Query plan caching (LRU) | [x] | P1 | v1 |
| EXPLAIN output | [x] | P1 | v1 |
| EXPLAIN QUERY PLAN | [x] | P1 | v1 |

---

## 12. Schema Information (100%)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| INFORMATION_SCHEMA.TABLES | [x] | P1 | v1 |
| INFORMATION_SCHEMA.COLUMNS | [x] | P1 | v1 |
| INFORMATION_SCHEMA.INDEXES | [x] | P1 | v1 |
| INFORMATION_SCHEMA.KEY_COLUMN_USAGE | [x] | P1 | v1 |
| INFORMATION_SCHEMA.TABLE_CONSTRAINTS | [x] | P1 | v1 |
| INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS | [x] | P1 | v1 |
| INFORMATION_SCHEMA.VIEWS | [x] | P1 | v1 |

---

## 13. ADO.NET Provider (0% - v1)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| WitDbConnection | [ ] | P0 | v1 |
| WitDbCommand | [ ] | P0 | v1 |
| WitDbDataReader | [ ] | P0 | v1 |
| WitDbParameter | [ ] | P0 | v1 |
| WitDbTransaction | [ ] | P0 | v1 |
| WitDbConnectionStringBuilder | [ ] | P0 | v1 |
| WitDbProviderFactory | [ ] | P0 | v1 |
| Async methods | [ ] | P0 | v1 |
| Connection pooling | [ ] | P1 | v1 |

---

## 14. v2 Features (Deferred)

| Feature | Status | Priority | Version |
|---------|--------|----------|---------|
| User-Defined Functions | [ ] | P2 | v2 |
| Stored Procedures | [ ] | P2 | v2 |
| EXPLAIN ANALYZE | [ ] | P2 | v2 |
| EXPLAIN (FORMAT JSON/TEXT) | [ ] | P2 | v2 |
| CREATE/DROP DATABASE | [ ] | P2 | v2 |
| ATTACH/DETACH DATABASE | [ ] | P2 | v2 |
| VACUUM | [ ] | P2 | v2 |
| ANALYZE | [ ] | P2 | v2 |
| PRAGMA | [ ] | P2 | v2 |
| Statistics histograms | [ ] | P2 | v2 |

---

## Test Coverage

| Component | Tests |
|-----------|-------|
| ExpressionEvaluator | 194 |
| StatementExecutor | 162 |
| Iterators | 119 |
| QueryPlanner | 50 |
| QueryOptimizer | 51 |
| WitSqlValue | 148 |
| WitSqlEngine Integration | 132 |
| WitSqlEngine Index | 67 |
| WitSqlEngine ALTER TABLE | 60 |
| WitSqlEngine Transactions | 46 |
| WitSqlEngine CTE | 43 |
| WitSqlEngine JSON | 42 |
| WitSqlEngine INFORMATION_SCHEMA | 42 |
| WitSqlEngine Window Functions | 37 |
| WitSqlEngine DML Enhancements | 62 |
| WitSqlEngine EXPLAIN | 13 |
| **Total** | **1395+** |

---

## Recent Changes

### 2025-02-05
- ROWVERSION implementation complete
- UPDATE ... FROM implementation complete
- DELETE ... USING implementation complete
- Window function frame clause complete
- Documentation audit and update

### 2025-02-04
- Query Optimization complete (index selection, plan caching, join ordering)

### 2025-02-03
- JSON Functions complete (12 functions)

### 2025-02-02
- DML Enhancements complete (RETURNING, UPSERT, TRUNCATE, MERGE)

### 2025-02-01
- Window Functions complete
- CTE Execution complete

### 2025-01-31
- ALTER TABLE implementation complete
- Computed columns complete

### 2025-01-30
- Transaction support complete
- Index implementation complete

---

## Summary

**v1 Status:**
- SQL Execution Engine: 100% complete
- Query Optimization: 100% complete  
- ADO.NET Provider: 0% (not started)

**v2 Deferred:**
- User-Defined Functions
- Stored Procedures
- Database Administration
- Advanced Query Analysis
