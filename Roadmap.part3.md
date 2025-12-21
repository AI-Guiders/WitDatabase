# WitDatabase Roadmap - Part 3: Expressions and Operators

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

## 1. Comparison Operators (WitSql.md Section 4.1)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `=` Equal | N/A | ? | ?? | §4.1 Comparison Operators |
| `<>` Not equal | N/A | ? | ?? | §4.1 Comparison Operators |
| `!=` Not equal (alias) | N/A | ? | ?? | §4.1 Comparison Operators |
| `<` Less than | N/A | ? | ?? | §4.1 Comparison Operators |
| `<=` Less than or equal | N/A | ? | ?? | §4.1 Comparison Operators |
| `>` Greater than | N/A | ? | ?? | §4.1 Comparison Operators |
| `>=` Greater than or equal | N/A | ? | ?? | §4.1 Comparison Operators |
| `IS NULL` | N/A | ? | ?? | §4.1 Comparison Operators |
| `IS NOT NULL` | N/A | ? | ?? | §4.1 Comparison Operators |
| `BETWEEN x AND y` | N/A | ? | ?? | §4.1 Comparison Operators |
| `NOT BETWEEN x AND y` | N/A | ? | ?? | §4.1 Comparison Operators |
| `IN (...)` | N/A | ? | ?? | §4.1 Comparison Operators |
| `NOT IN (...)` | N/A | ? | ?? | §4.1 Comparison Operators |
| `LIKE pattern` | N/A | ? | ?? | §4.1 Comparison Operators |
| `NOT LIKE pattern` | N/A | ? | ?? | §4.1 Comparison Operators |
| `LIKE ... ESCAPE char` | N/A | ? | ?? | §4.1 LIKE Patterns |
| `GLOB pattern` | N/A | ? | ?? | §4.1 Comparison Operators |
| `%` wildcard (any sequence) | N/A | ? | ?? | §4.1 LIKE Patterns |
| `_` wildcard (single char) | N/A | ? | ?? | §4.1 LIKE Patterns |

---

## 2. Logical Operators (WitSql.md Section 4.2)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `AND` | N/A | ? | ?? | §4.2 Logical Operators |
| `OR` | N/A | ? | ?? | §4.2 Logical Operators |
| `NOT` | N/A | ? | ?? | §4.2 Logical Operators |
| Precedence: NOT > AND > OR | N/A | ? | ?? | §4.2 Logical Operators |

---

## 3. Arithmetic Operators (WitSql.md Section 4.3)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `+` Addition | N/A | ? | ?? | §4.3 Arithmetic Operators |
| `-` Subtraction | N/A | ? | ?? | §4.3 Arithmetic Operators |
| `*` Multiplication | N/A | ? | ?? | §4.3 Arithmetic Operators |
| `/` Division | N/A | ? | ?? | §4.3 Arithmetic Operators |
| `%` Modulo | N/A | ? | ?? | §4.3 Arithmetic Operators |
| `-expr` Unary minus | N/A | ? | ?? | §4.3 Arithmetic Operators |
| `+expr` Unary plus | N/A | ? | ?? | §4.3 Arithmetic Operators |

---

## 4. String Operators (WitSql.md Section 4.4)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `\|\|` String concatenation | N/A | ? | ?? | §4.4 String Operators |

---

## 5. Bitwise Operators (WitSql.md Section 4.5)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `&` Bitwise AND | N/A | ? | ?? | §4.5 Bitwise Operators |
| `\|` Bitwise OR | N/A | ? | ?? | §4.5 Bitwise Operators |
| `~` Bitwise NOT | N/A | ? | ?? | §4.5 Bitwise Operators |
| `<<` Left shift | N/A | ? | ?? | §4.5 Bitwise Operators |
| `>>` Right shift | N/A | ? | ?? | §4.5 Bitwise Operators |

---

## 6. Conditional Expressions (WitSql.md Section 4.6)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `CASE expr WHEN val THEN result END` | N/A | ? | ?? | §4.6 Conditional Expressions |
| `CASE WHEN cond THEN result END` (searched) | N/A | ? | ?? | §4.6 Conditional Expressions |
| `ELSE default_result` in CASE | N/A | ? | ?? | §4.6 Conditional Expressions |
| `COALESCE(expr1, expr2, ...)` | N/A | ? | ?? | §4.6 Conditional Expressions |
| `NULLIF(expr1, expr2)` | N/A | ? | ?? | §4.6 Conditional Expressions |
| `IIF(condition, true_val, false_val)` | N/A | ? | ?? | §4.6 Conditional Expressions |
| `CAST(expression AS data_type)` | N/A | ? | ?? | §4.6 Conditional Expressions |

---

## 7. Literals (WitSql.md Sections 1.2-1.8)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| Integer literals | N/A | ? | ?? | §1.2 Integer Types |
| Floating-point literals | N/A | ? | ?? | §1.3 Floating-Point Types |
| String literals `'text'` | N/A | ? | ?? | §1.7 String Types |
| Boolean literals `TRUE`, `FALSE` | N/A | ? | ?? | §1.4 Boolean Type |
| `NULL` literal | N/A | ? | ?? | §1.1 Null Type |
| Hex blob `X'48656C6C6F'` | N/A | ? | ?? | §1.8 Binary Types |
| Date literal `'2024-01-01'` | N/A | ? | ?? | §1.5 Date and Time Types |
| DateTime literal `'2024-01-01 12:00:00'` | N/A | ? | ?? | §1.5 Date and Time Types |

---

## 8. Column References

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| Simple column name `column` | N/A | ? | ?? | §3.1 SELECT |
| Qualified column `table.column` | N/A | ? | ?? | §3.1 SELECT |
| Alias column `alias.column` | N/A | ? | ?? | §3.1 SELECT |
| `*` all columns | N/A | ? | ?? | §3.1 select_list |
| `table.*` all columns from table | N/A | ? | ?? | §3.1 select_list |

---

## 9. Parameters (WitSql.md Section 11)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| Named parameter `@paramName` | N/A | ? | ?? | §11 Parameters |
| Named parameter `:paramName` | N/A | ? | ?? | §11 Parameters |
| Positional parameter `?` | N/A | ? | ?? | §11 Parameters |
| Positional parameter `$1`, `$2` | N/A | ? | ?? | §11 Parameters |

---

## 10. Collation (WitSql.md Section 24)

| Feature | Core | Parser | Engine | Spec Line |
|---------|------|--------|--------|-----------|
| `COLLATE collation_name` in column def | N/A | ?? | ?? | §24 Collation |
| `COLLATE collation_name` in expression | N/A | ?? | ?? | §24 Collation |
| `COLLATE collation_name` in ORDER BY | N/A | ?? | ?? | §24 Collation |
| `BINARY` collation | N/A | ?? | ?? | §24 Supported Collations |
| `NOCASE` collation | N/A | ?? | ?? | §24 Supported Collations |
| `UNICODE` collation | N/A | ?? | ?? | §24 Supported Collations |
| `UNICODE_CI` collation | N/A | ?? | ?? | §24 Supported Collations |

---

## Summary - Part 3

| Category | Total | Parser ? | Parser ?? | Notes |
|----------|-------|-----------|-----------|-------|
| Comparison Operators | 19 | 19 | 0 | ? Complete |
| Logical Operators | 4 | 4 | 0 | ? Complete |
| Arithmetic Operators | 7 | 7 | 0 | ? Complete |
| String Operators | 1 | 1 | 0 | ? Complete |
| Bitwise Operators | 5 | 5 | 0 | ? Complete |
| Conditional Expressions | 7 | 7 | 0 | ? Complete |
| Literals | 8 | 8 | 0 | ? Complete |
| Column References | 5 | 5 | 0 | ? Complete |
| Parameters | 4 | 4 | 0 | ? Complete |
| Collation | 7 | 0 | 7 | All missing |

---

*Continue to [Roadmap.part4.md](Roadmap.part4.md) for Built-in Functions*
