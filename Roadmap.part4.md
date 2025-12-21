# WitDatabase Roadmap - Part 4: Built-in Functions

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

**Note:** Parser supports function call syntax. Individual functions listed here are for Engine implementation tracking. Parser recognizes any function name syntactically.

---

## 1. Aggregate Functions (WitSql.md Section 5.1)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `COUNT(*)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `COUNT(expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `COUNT(DISTINCT expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `SUM(expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `AVG(expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `MIN(expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `MAX(expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `GROUP_CONCAT(expr)` | N/A | ? | ?? | §5.1 Aggregate Functions |
| `GROUP_CONCAT(expr, separator)` | N/A | ? | ?? | §5.1 Aggregate Functions |

---

## 2. String Functions (WitSql.md Section 5.2)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `LENGTH(str)` | N/A | ? | ?? | §5.2 String Functions |
| `CHAR_LENGTH(str)` | N/A | ? | ?? | §5.2 String Functions |
| `OCTET_LENGTH(str)` | N/A | ? | ?? | §5.2 String Functions |
| `UPPER(str)` | N/A | ? | ?? | §5.2 String Functions |
| `LOWER(str)` | N/A | ? | ?? | §5.2 String Functions |
| `SUBSTR(str, start, len)` | N/A | ? | ?? | §5.2 String Functions |
| `SUBSTRING(str, start, len)` | N/A | ? | ?? | §5.2 String Functions |
| `LEFT(str, n)` | N/A | ?? | ?? | §5.2 String Functions |
| `RIGHT(str, n)` | N/A | ?? | ?? | §5.2 String Functions |
| `TRIM(str)` | N/A | ? | ?? | §5.2 String Functions |
| `LTRIM(str)` | N/A | ? | ?? | §5.2 String Functions |
| `RTRIM(str)` | N/A | ? | ?? | §5.2 String Functions |
| `TRIM(chars FROM str)` | N/A | ? | ?? | §5.2 String Functions |
| `REPLACE(str, old, new)` | N/A | ? | ?? | §5.2 String Functions |
| `INSTR(str, substr)` | N/A | ? | ?? | §5.2 String Functions |
| `POSITION(substr IN str)` | N/A | ? | ?? | §5.2 String Functions |
| `CONCAT(str1, str2, ...)` | N/A | ? | ?? | §5.2 String Functions |
| `CONCAT_WS(sep, str1, ...)` | N/A | ? | ?? | §5.2 String Functions |
| `REVERSE(str)` | N/A | ? | ?? | §5.2 String Functions |
| `REPEAT(str, n)` | N/A | ? | ?? | §5.2 String Functions |
| `SPACE(n)` | N/A | ? | ?? | §5.2 String Functions |
| `LPAD(str, len, pad)` | N/A | ? | ?? | §5.2 String Functions |
| `RPAD(str, len, pad)` | N/A | ? | ?? | §5.2 String Functions |
| `FORMAT(str, args...)` | N/A | ? | ?? | §5.2 String Functions |

---

## 3. Numeric Functions (WitSql.md Section 5.3)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `ABS(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `SIGN(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `ROUND(x, n)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `FLOOR(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `CEIL(x)` / `CEILING(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `TRUNC(x, n)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `MOD(x, y)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `POWER(x, y)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `SQRT(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `EXP(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `LOG(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `LOG10(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `LOG2(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `SIN(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `COS(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `TAN(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `ASIN(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `ACOS(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `ATAN(x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `ATAN2(y, x)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `PI()` | N/A | ? | ?? | §5.3 Numeric Functions |
| `DEGREES(rad)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `RADIANS(deg)` | N/A | ? | ?? | §5.3 Numeric Functions |
| `RANDOM()` | N/A | ? | ?? | §5.3 Numeric Functions |
| `RANDOM(min, max)` | N/A | ? | ?? | §5.3 Numeric Functions |

---

## 4. Date and Time Functions (WitSql.md Section 5.4)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `NOW()` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `CURRENT_TIMESTAMP` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `CURRENT_DATE` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `CURRENT_TIME` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `DATE(expr)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `TIME(expr)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `YEAR(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `MONTH(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `DAY(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `HOUR(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `MINUTE(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `SECOND(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `DAYOFWEEK(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `DAYOFYEAR(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `WEEKOFYEAR(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `QUARTER(dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `DATEADD(part, n, dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `DATEDIFF(part, dt1, dt2)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `STRFTIME(format, dt)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `MAKEDATE(year, dayofyear)` | N/A | ? | ?? | §5.4 Date and Time Functions |
| `MAKETIME(h, m, s)` | N/A | ? | ?? | §5.4 Date and Time Functions |

---

## 5. ID Generation Functions (WitSql.md Section 5.5)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `NEWGUID()` | N/A | ? | ?? | §5.5 ID Generation Functions |
| `NEWUUID()` | N/A | ? | ?? | §5.5 ID Generation Functions |
| `INCREMENT(sequence)` | N/A | ? | ?? | §5.5 ID Generation Functions |
| `LASTINCREMENT(sequence)` | N/A | ? | ?? | §5.5 ID Generation Functions |

---

## 6. Conversion Functions (WitSql.md Section 5.6)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `CAST(expr AS type)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `CONVERT(type, expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TOSTRING(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TOINT(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TODOUBLE(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TODECIMAL(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TOBOOLEAN(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TODATE(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TODATETIME(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `TOGUID(expr)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `HEX(blob)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `UNHEX(str)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `BASE64(blob)` | N/A | ? | ?? | §5.6 Conversion Functions |
| `UNBASE64(str)` | N/A | ? | ?? | §5.6 Conversion Functions |

---

## 7. Null Handling Functions (WitSql.md Section 5.7)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `COALESCE(expr, ...)` | N/A | ? | ?? | §5.7 Null Handling Functions |
| `NULLIF(a, b)` | N/A | ? | ?? | §5.7 Null Handling Functions |
| `IFNULL(expr, default)` | N/A | ? | ?? | §5.7 Null Handling Functions |
| `NVL(expr, default)` | N/A | ? | ?? | §5.7 Null Handling Functions |

---

## 8. System Functions (WitSql.md Section 5.8)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `DATABASE()` | N/A | ? | ?? | §5.8 System Functions |
| `VERSION()` | N/A | ? | ?? | §5.8 System Functions |
| `TYPEOF(expr)` | N/A | ? | ?? | §5.8 System Functions |
| `ROWID` | N/A | ? | ?? | §5.8 System Functions |
| `CHANGES()` | N/A | ? | ?? | §5.8 System Functions |
| `LAST_INSERT_ROWID()` | N/A | ? | ?? | §5.8 System Functions |

---

## 9. JSON Functions (WitSql.md Section 21.2)

| Function | Core | Parser | Engine | Spec Line |
|----------|------|--------|--------|-----------|
| `JSON_VALUE(json, path)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_QUERY(json, path)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_EXTRACT(json, path)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_SET(json, path, value)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_INSERT(json, path, value)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_REPLACE(json, path, value)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_REMOVE(json, path)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_TYPE(json)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_VALID(str)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_ARRAY(values...)` | N/A | ?? | ?? | §21.2 JSON Functions |
| `JSON_OBJECT(pairs...)` | N/A | ?? | ?? | §21.2 JSON Functions |

---

## Summary - Part 4

| Category | Total | Parser ? | Parser ?? | Notes |
|----------|-------|-----------|-----------|-------|
| Aggregate Functions | 9 | 9 | 0 | ? Complete |
| String Functions | 24 | 22 | 2 | LEFT, RIGHT missing |
| Numeric Functions | 25 | 25 | 0 | ? Complete |
| Date/Time Functions | 21 | 21 | 0 | ? Complete |
| ID Generation Functions | 4 | 4 | 0 | ? Complete |
| Conversion Functions | 14 | 14 | 0 | ? Complete |
| Null Handling Functions | 4 | 4 | 0 | ? Complete |
| System Functions | 6 | 6 | 0 | ? Complete |
| JSON Functions | 11 | 0 | 11 | All missing |

---

*Continue to [Roadmap.part5.md](Roadmap.part5.md) for Window Functions, Transactions, and Advanced Features*
