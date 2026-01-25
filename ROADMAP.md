# WitDatabase Roadmap

**Last Updated:** 2026-01-20

This document outlines planned features for future versions of WitDatabase.

---

## Version 2.0 - Planned Features

### Priority 0: Performance Critical

| Feature | Component | Description | Expected Improvement |
|---------|-----------|-------------|---------------------|
| Index Point Seek Optimization | Core | Cursor caching and B-tree seek optimization | 10-30x faster index lookups |
| Index-Only Scans | Engine | Return data directly from index without row fetch | 2-5x for covered queries |
| Query Plan Caching | Engine | Cache and reuse plans for parameterized queries | 2-5x for repeated queries |

### Priority 1: High Value

| Feature | Component | Description |
|---------|-----------|-------------|
| Cursor support | Core | Forward-only and scrollable cursors |
| Fetch batching | Core | Batch retrieval for large result sets |
| VACUUM command | Core/Engine | Reclaim unused space in B+Tree |
| Incremental vacuum | Core | Background space reclamation |
| Parallel query execution | Engine | Multi-threaded table scans |

### Priority 2: SQL Enhancements

**User-Defined Functions**
- CREATE FUNCTION execution
- RETURNS TABLE support (table-valued functions)
- DETERMINISTIC handling for optimization
- DROP FUNCTION execution

**Stored Procedures**
- CREATE PROCEDURE execution
- DROP PROCEDURE execution
- CALL / EXECUTE execution
- Procedure parameters (IN/OUT/INOUT)

**Query Analysis**
- EXPLAIN ANALYZE (actual execution statistics)
- EXPLAIN (FORMAT JSON/TEXT)

**Database Administration**
- CREATE DATABASE / DROP DATABASE
- ATTACH DATABASE / DETACH DATABASE
- ANALYZE command (update statistics)
- PRAGMA support for configuration

### Priority 3: Advanced Features

**Advanced Optimization**
- Statistics histograms for better cardinality estimation
- Adaptive query execution (runtime plan adjustment)
- Predicate pushdown to storage layer
- Parallel aggregation for large GROUP BY

**LSM-Tree Enhancements**
- Leveled compaction strategy
- Page-level compression (LZ4, Snappy)
- Compaction progress API
- Tiered compaction options

**Storage Enhancements**
- SIMD-accelerated operations
- Memory-mapped file support
- Lazy row loading

---

## Component-Specific Roadmaps

Each component has its own detailed roadmap:

| Component | Location |
|-----------|----------|
| OutWit.Database.Core | [Sources/Core/OutWit.Database.Core/ROADMAP.md](Sources/Core/OutWit.Database.Core/ROADMAP.md) |
| OutWit.Database.Core.BouncyCastle | [Sources/Core/OutWit.Database.Core.BouncyCastle/ROADMAP.md](Sources/Core/OutWit.Database.Core.BouncyCastle/ROADMAP.md) |
| OutWit.Database.Core.IndexedDb | [Sources/Core/OutWit.Database.Core.IndexedDb/ROADMAP.md](Sources/Core/OutWit.Database.Core.IndexedDb/ROADMAP.md) |
| OutWit.Database.Parser | [Sources/Engine/OutWit.Database.Parser/ROADMAP.md](Sources/Engine/OutWit.Database.Parser/ROADMAP.md) |
| OutWit.Database | [Sources/Engine/OutWit.Database/ROADMAP.md](Sources/Engine/OutWit.Database/ROADMAP.md) |
| OutWit.Database.AdoNet | [Sources/Providers/OutWit.Database.AdoNet/ROADMAP.md](Sources/Providers/OutWit.Database.AdoNet/ROADMAP.md) |
| OutWit.Database.EntityFramework | [Sources/Providers/OutWit.Database.EntityFramework/ROADMAP.md](Sources/Providers/OutWit.Database.EntityFramework/ROADMAP.md) |

---

## Performance Targets for v2.0

| Metric | Current | Target |
|--------|---------|--------|
| Index point seek vs SQLite | 30-100x slower | Within 3-5x |
| INSERT 10K rows | 115ms | 50-70ms |
| Query plan reuse | Not cached | Full caching |

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

If you want to work on a planned feature:

1. Check the issue tracker for existing work
2. Open an issue to discuss the implementation
3. Fork the repository and create a feature branch
4. Submit a pull request with tests

---

## License

This software is licensed under the **Non-Commercial License (NCL)**.

- Free for personal, educational, and research purposes
- Commercial use requires a separate license agreement
- Contact licensing@ratner.io for commercial licensing inquiries

See the full [LICENSE](LICENSE) file for details.
