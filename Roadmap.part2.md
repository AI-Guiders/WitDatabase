# WitDatabase Roadmap - Part 2: DML Statements

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

## 1. SELECT Statement (WitSql.md Section 3.1)

### 1.1 Basic SELECT

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `SELECT *` | N/A | ? | ?? | §3.1 SELECT |
| `SELECT column_list` | N/A | ? | ?? | §3.1 SELECT |
| `SELECT expression AS alias` | N/A | ? | ?? | §3.1 select_list |
| `SELECT DISTINCT` | N/A | ? | ?? | §3.1 SELECT |
| `SELECT ALL` | N/A | ? | ?? | §3.1 SELECT |

### 1.2 FROM Clause

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `FROM table_name` | N/A | ? | ?? | §3.1 table_reference |
| `FROM table AS alias` | N/A | ? | ?? | §3.1 table_reference |
| `FROM (subquery) AS alias` | N/A | ? | ?? | §3.1 table_reference |
| `FROM table1, table2` (implicit join) | N/A | ? | ?? | §3.1 table_reference |

### 1.3 JOIN Clauses

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `INNER JOIN ... ON ...` | N/A | ? | ?? | §3.1 join_type |
| `LEFT [OUTER] JOIN ... ON ...` | N/A | ? | ?? | §3.1 join_type |
| `RIGHT [OUTER] JOIN ... ON ...` | N/A | ? | ?? | §3.1 join_type |
| `FULL [OUTER] JOIN ... ON ...` | N/A | ? | ?? | §3.1 join_type |
| `CROSS JOIN` | N/A | ? | ?? | §3.1 join_type |

### 1.4 WHERE, GROUP BY, HAVING

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `WHERE condition` | N/A | ? | ?? | §3.1 SELECT |
| `GROUP BY expression` | N/A | ? | ?? | §3.1 SELECT |
| `GROUP BY expr1, expr2, ...` | N/A | ? | ?? | §3.1 SELECT |
| `HAVING condition` | N/A | ? | ?? | §3.1 SELECT |

### 1.5 ORDER BY

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `ORDER BY expression` | N/A | ? | ?? | §3.1 SELECT |
| `ORDER BY expr ASC` | N/A | ? | ?? | §3.1 SELECT |
| `ORDER BY expr DESC` | N/A | ? | ?? | §3.1 SELECT |
| `ORDER BY expr NULLS FIRST` | N/A | ? | ?? | §3.1 SELECT |
| `ORDER BY expr NULLS LAST` | N/A | ? | ?? | §3.1 SELECT |

### 1.6 LIMIT / OFFSET

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `LIMIT count` | N/A | ? | ?? | §3.1 SELECT |
| `LIMIT count OFFSET offset` | N/A | ? | ?? | §3.1 SELECT |
| `OFFSET offset` (standalone) | N/A | ? | ?? | §3.1 SELECT |

### 1.7 Locking Hints

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `FOR UPDATE` | ?? | ?? | ?? | §14.2 Locking Hints |
| `FOR SHARE` | ?? | ?? | ?? | §14.2 Locking Hints |
| `FOR UPDATE NOWAIT` | ?? | ?? | ?? | §14.2 Locking Hints |
| `FOR UPDATE SKIP LOCKED` | ?? | ?? | ?? | §14.2 Locking Hints |

---

## 2. INSERT Statement (WitSql.md Section 3.2)

### 2.1 Basic INSERT

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `INSERT INTO table (cols) VALUES (...)` | N/A | ? | ?? | §3.2 INSERT |
| `INSERT INTO table VALUES (...)` | N/A | ? | ?? | §3.2 INSERT |
| Multi-row insert `VALUES (...), (...)` | N/A | ? | ?? | §3.2 INSERT |
| `INSERT INTO ... SELECT ...` | N/A | ? | ?? | §3.2 INSERT |
| `INSERT ... RETURNING select_list` | N/A | ? | ?? | §3.2 INSERT |
| `INSERT ... RETURNING *` | N/A | ? | ?? | §3.2 INSERT |

### 2.2 UPSERT / ON CONFLICT

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `INSERT OR REPLACE INTO ...` | N/A | ?? | ?? | §16.1 INSERT OR REPLACE |
| `INSERT ... ON CONFLICT (cols) DO UPDATE SET ...` | N/A | ?? | ?? | §16.2 INSERT ON CONFLICT |
| `INSERT ... ON CONFLICT (cols) DO NOTHING` | N/A | ?? | ?? | §16.2 INSERT ON CONFLICT |
| `EXCLUDED.column` reference | N/A | ?? | ?? | §16 Examples |

---

## 3. UPDATE Statement (WitSql.md Section 3.3)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `UPDATE table SET col = expr` | N/A | ? | ?? | §3.3 UPDATE |
| `UPDATE table SET col1 = expr1, col2 = expr2` | N/A | ? | ?? | §3.3 UPDATE |
| `UPDATE ... WHERE condition` | N/A | ? | ?? | §3.3 UPDATE |
| `UPDATE ... RETURNING select_list` | N/A | ? | ?? | §3.3 UPDATE |
| `UPDATE ... RETURNING *` | N/A | ? | ?? | §3.3 UPDATE |
| `UPDATE ... FROM other_table WHERE ...` | N/A | ?? | ?? | §17.2 UPDATE with FROM |

---

## 4. DELETE Statement (WitSql.md Section 3.4)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `DELETE FROM table` | N/A | ? | ?? | §3.4 DELETE |
| `DELETE FROM table WHERE condition` | N/A | ? | ?? | §3.4 DELETE |
| `DELETE FROM ... RETURNING select_list` | N/A | ? | ?? | §3.4 DELETE |
| `DELETE FROM ... RETURNING *` | N/A | ? | ?? | §3.4 DELETE |
| `DELETE FROM ... USING other_table WHERE ...` | N/A | ?? | ?? | §17.3 DELETE with FROM |

---

## 5. TRUNCATE Statement (WitSql.md Section 17.1)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `TRUNCATE TABLE table_name` | N/A | ? | ?? | §17.1 TRUNCATE TABLE |

---

## 6. MERGE Statement (WitSql.md Section 16.3)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `MERGE INTO target USING source ON (...)` | N/A | ?? | ?? | §16.3 MERGE Statement |
| `WHEN MATCHED THEN UPDATE SET ...` | N/A | ?? | ?? | §16.3 MERGE Statement |
| `WHEN NOT MATCHED THEN INSERT (...)` | N/A | ?? | ?? | §16.3 MERGE Statement |

---

## 7. Common Table Expressions - CTE (WitSql.md Section 6)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `WITH cte_name AS (SELECT ...)` | N/A | ? | ?? | §6 CTE |
| `WITH cte_name (cols) AS (SELECT ...)` | N/A | ? | ?? | §6 CTE |
| Multiple CTEs: `WITH cte1 AS (...), cte2 AS (...)` | N/A | ? | ?? | §6 CTE |
| `WITH RECURSIVE cte_name ...` | N/A | ? | ?? | §6 Recursive CTE |
| Anchor member | N/A | ? | ?? | §6 Recursive CTE |
| Recursive member with `UNION ALL` | N/A | ? | ?? | §6 Recursive CTE |

---

## 8. Set Operations (WitSql.md Section 8)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `SELECT ... UNION SELECT ...` | N/A | ? | ?? | §8 Set Operations |
| `SELECT ... UNION ALL SELECT ...` | N/A | ? | ?? | §8 Set Operations |
| `SELECT ... INTERSECT SELECT ...` | N/A | ? | ?? | §8 Set Operations |
| `SELECT ... EXCEPT SELECT ...` | N/A | ? | ?? | §8 Set Operations |

---

## 9. Subqueries (WitSql.md Section 18)

### 9.1 Scalar & Table Subqueries

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| Scalar subquery in SELECT list | N/A | ? | ?? | §3.1 Examples |
| Table subquery in FROM | N/A | ? | ?? | §3.1 table_reference |
| Subquery in WHERE with `IN` | N/A | ? | ?? | §3.1 Examples |

### 9.2 EXISTS Operator

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `EXISTS (subquery)` | N/A | ? | ?? | §18.1 EXISTS |
| `NOT EXISTS (subquery)` | N/A | ? | ?? | §18.1 EXISTS |

### 9.3 ANY / SOME / ALL Operators

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `expression > ANY (subquery)` | N/A | ?? | ?? | §18.2 ANY/SOME/ALL |
| `expression > SOME (subquery)` | N/A | ?? | ?? | §18.2 ANY/SOME/ALL |
| `expression > ALL (subquery)` | N/A | ?? | ?? | §18.2 ANY/SOME/ALL |
| All comparison operators with ANY/SOME/ALL | N/A | ?? | ?? | §18.2 ANY/SOME/ALL |

---

## Summary - Part 2

| Category | Total | Parser ? | Parser ?? | Notes |
|----------|-------|-----------|-----------|-------|
| Basic SELECT | 5 | 5 | 0 | ? Complete |
| FROM Clause | 4 | 4 | 0 | ? Complete |
| JOIN Clauses | 5 | 5 | 0 | ? Complete |
| WHERE/GROUP/HAVING | 4 | 4 | 0 | ? Complete |
| ORDER BY | 5 | 5 | 0 | ? Complete |
| LIMIT/OFFSET | 3 | 3 | 0 | ? Complete |
| Locking Hints | 4 | 0 | 4 | FOR UPDATE/SHARE missing |
| Basic INSERT | 6 | 6 | 0 | ? Complete |
| UPSERT/ON CONFLICT | 4 | 0 | 4 | All missing |
| UPDATE | 6 | 5 | 1 | UPDATE FROM missing |
| DELETE | 5 | 4 | 1 | DELETE USING missing |
| TRUNCATE | 1 | 1 | 0 | ? Complete |
| MERGE | 3 | 0 | 3 | All missing |
| CTE | 6 | 6 | 0 | ? Complete |
| Set Operations | 4 | 4 | 0 | ? Complete |
| Subqueries Basic | 3 | 3 | 0 | ? Complete |
| EXISTS | 2 | 2 | 0 | ? Complete |
| ANY/SOME/ALL | 4 | 0 | 4 | All missing |

---

*Continue to [Roadmap.part3.md](Roadmap.part3.md) for Expressions and Operators*
