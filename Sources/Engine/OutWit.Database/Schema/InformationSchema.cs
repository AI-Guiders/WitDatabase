using OutWit.Database.Definitions;
using OutWit.Database.Interfaces;
using OutWit.Database.Types;
using OutWit.Database.Values;

namespace OutWit.Database.Schema;

/// <summary>
/// INFORMATION_SCHEMA implementation for database metadata access.
/// Provides SQL-standard system views: TABLES, COLUMNS, KEY_COLUMN_USAGE, etc.
/// </summary>
public sealed class InformationSchema
{
    #region Constants

    public const string SCHEMA_NAME = "INFORMATION_SCHEMA";

    private static readonly string[] TABLES_COLUMNS = ["TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "TABLE_TYPE", "TABLE_COMMENT"];
    private static readonly WitSqlType[] TABLES_TYPES = [WitSqlType.Text, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text];

    private static readonly string[] COLUMNS_COLUMNS = [
        "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "COLUMN_NAME",
        "ORDINAL_POSITION", "COLUMN_DEFAULT", "IS_NULLABLE", "DATA_TYPE",
        "CHARACTER_MAXIMUM_LENGTH", "NUMERIC_PRECISION", "NUMERIC_SCALE",
        "GENERATION_EXPRESSION", "IS_GENERATED"
    ];
    private static readonly WitSqlType[] COLUMNS_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Integer, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Integer, WitSqlType.Integer, WitSqlType.Integer,
        WitSqlType.Text, WitSqlType.Text
    ];

    private static readonly string[] KEY_COLUMN_USAGE_COLUMNS = [
        "CONSTRAINT_CATALOG", "CONSTRAINT_SCHEMA", "CONSTRAINT_NAME",
        "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "COLUMN_NAME",
        "ORDINAL_POSITION", "POSITION_IN_UNIQUE_CONSTRAINT",
        "REFERENCED_TABLE_SCHEMA", "REFERENCED_TABLE_NAME", "REFERENCED_COLUMN_NAME"
    ];
    private static readonly WitSqlType[] KEY_COLUMN_USAGE_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Integer, WitSqlType.Integer,
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text
    ];

    private static readonly string[] TABLE_CONSTRAINTS_COLUMNS = [
        "CONSTRAINT_CATALOG", "CONSTRAINT_SCHEMA", "CONSTRAINT_NAME",
        "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "CONSTRAINT_TYPE",
        "IS_DEFERRABLE", "INITIALLY_DEFERRED"
    ];
    private static readonly WitSqlType[] TABLE_CONSTRAINTS_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Text
    ];

    private static readonly string[] REFERENTIAL_CONSTRAINTS_COLUMNS = [
        "CONSTRAINT_CATALOG", "CONSTRAINT_SCHEMA", "CONSTRAINT_NAME",
        "UNIQUE_CONSTRAINT_CATALOG", "UNIQUE_CONSTRAINT_SCHEMA", "UNIQUE_CONSTRAINT_NAME",
        "MATCH_OPTION", "UPDATE_RULE", "DELETE_RULE"
    ];
    private static readonly WitSqlType[] REFERENTIAL_CONSTRAINTS_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text
    ];

    private static readonly string[] INDEXES_COLUMNS = [
        "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME", "INDEX_NAME",
        "COLUMN_NAME", "ORDINAL_POSITION", "IS_UNIQUE", "INDEX_TYPE", "FILTER_CONDITION"
    ];
    private static readonly WitSqlType[] INDEXES_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Integer, WitSqlType.Text, WitSqlType.Text, WitSqlType.Text
    ];

    private static readonly string[] VIEWS_COLUMNS = [
        "TABLE_CATALOG", "TABLE_SCHEMA", "TABLE_NAME",
        "VIEW_DEFINITION", "CHECK_OPTION", "IS_UPDATABLE"
    ];
    private static readonly WitSqlType[] VIEWS_TYPES = [
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text,
        WitSqlType.Text, WitSqlType.Text, WitSqlType.Text
    ];

    #endregion

    #region Fields

    private readonly SchemaCatalog m_catalog;

    #endregion

    #region Constructors

    public InformationSchema(SchemaCatalog catalog)
    {
        m_catalog = catalog;
    }

    #endregion

    #region INFORMATION_SCHEMA.TABLES

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.TABLES view data.
    /// Returns information about all tables and views in the database.
    /// </summary>
    public IEnumerable<WitSqlRow> GetTables()
    {
        // Add tables
        foreach (var table in m_catalog.Tables)
        {
            yield return new WitSqlRow([
                WitSqlValue.FromText("WitDB"),          // TABLE_CATALOG
                WitSqlValue.FromText("public"),         // TABLE_SCHEMA
                WitSqlValue.FromText(table.Name),       // TABLE_NAME
                WitSqlValue.FromText("BASE TABLE"),     // TABLE_TYPE
                WitSqlValue.Null,                       // TABLE_COMMENT
            ], TABLES_COLUMNS);
        }

        // Add views
        foreach (var view in m_catalog.GetViews())
        {
            yield return new WitSqlRow([
                WitSqlValue.FromText("WitDB"),          // TABLE_CATALOG
                WitSqlValue.FromText("public"),         // TABLE_SCHEMA
                WitSqlValue.FromText(view.Name),        // TABLE_NAME
                WitSqlValue.FromText("VIEW"),           // TABLE_TYPE
                WitSqlValue.Null,                       // TABLE_COMMENT
            ], TABLES_COLUMNS);
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.TABLES.
    /// </summary>
    public static IReadOnlyList<string> GetTablesColumns() => TABLES_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.TABLES.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetTablesColumnTypes() => TABLES_TYPES;

    #endregion

    #region INFORMATION_SCHEMA.COLUMNS

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.COLUMNS view data.
    /// Returns information about all columns in all tables.
    /// </summary>
    public IEnumerable<WitSqlRow> GetColumns()
    {
        foreach (var table in m_catalog.Tables)
        {
            foreach (var column in table.Columns)
            {
                // PK columns are implicitly NOT NULL
                var isNullable = column.Nullable && !column.IsPrimaryKey;
                
                yield return new WitSqlRow([
                    WitSqlValue.FromText("WitDB"),                                    // TABLE_CATALOG
                    WitSqlValue.FromText("public"),                                   // TABLE_SCHEMA
                    WitSqlValue.FromText(table.Name),                                 // TABLE_NAME
                    WitSqlValue.FromText(column.Name),                                // COLUMN_NAME
                    WitSqlValue.FromInt(column.Ordinal + 1),                          // ORDINAL_POSITION (1-based)
                    column.DefaultValue != null 
                        ? WitSqlValue.FromText(column.DefaultValue) 
                        : WitSqlValue.Null,                                           // COLUMN_DEFAULT
                    WitSqlValue.FromText(isNullable ? "YES" : "NO"),                  // IS_NULLABLE
                    WitSqlValue.FromText(GetDataTypeName(column.Type)),               // DATA_TYPE
                    column.MaxLength.HasValue 
                        ? WitSqlValue.FromInt(column.MaxLength.Value) 
                        : WitSqlValue.Null,                                           // CHARACTER_MAXIMUM_LENGTH
                    column.Precision.HasValue 
                        ? WitSqlValue.FromInt(column.Precision.Value) 
                        : WitSqlValue.Null,                                           // NUMERIC_PRECISION
                    column.Scale.HasValue 
                        ? WitSqlValue.FromInt(column.Scale.Value) 
                        : WitSqlValue.Null,                                           // NUMERIC_SCALE
                    column.IsComputed 
                        ? WitSqlValue.FromText(column.ComputedExpression!) 
                        : WitSqlValue.Null,                                           // GENERATION_EXPRESSION
                    WitSqlValue.FromText(column.IsComputed 
                        ? (column.IsStored ? "STORED" : "VIRTUAL") 
                        : "NEVER"),                                                   // IS_GENERATED
                ], COLUMNS_COLUMNS);
            }
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.COLUMNS.
    /// </summary>
    public static IReadOnlyList<string> GetColumnsColumns() => COLUMNS_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.COLUMNS.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetColumnsColumnTypes() => COLUMNS_TYPES;

    #endregion

    #region INFORMATION_SCHEMA.KEY_COLUMN_USAGE

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.KEY_COLUMN_USAGE view data.
    /// Returns information about columns that are constrained as keys.
    /// </summary>
    public IEnumerable<WitSqlRow> GetKeyColumnUsage()
    {
        foreach (var table in m_catalog.Tables)
        {
            // Primary key columns
            if (table.PrimaryKey != null)
            {
                int position = 1;
                foreach (var columnName in table.PrimaryKey)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),                               // CONSTRAINT_CATALOG
                        WitSqlValue.FromText("public"),                              // CONSTRAINT_SCHEMA
                        WitSqlValue.FromText($"PK_{table.Name}"),                    // CONSTRAINT_NAME
                        WitSqlValue.FromText("WitDB"),                               // TABLE_CATALOG
                        WitSqlValue.FromText("public"),                              // TABLE_SCHEMA
                        WitSqlValue.FromText(table.Name),                            // TABLE_NAME
                        WitSqlValue.FromText(columnName),                            // COLUMN_NAME
                        WitSqlValue.FromInt(position++),                             // ORDINAL_POSITION
                        WitSqlValue.Null,                                            // POSITION_IN_UNIQUE_CONSTRAINT
                        WitSqlValue.Null,                                            // REFERENCED_TABLE_SCHEMA
                        WitSqlValue.Null,                                            // REFERENCED_TABLE_NAME
                        WitSqlValue.Null,                                            // REFERENCED_COLUMN_NAME
                    ], KEY_COLUMN_USAGE_COLUMNS);
                }
            }

            // Table-level unique constraints
            if (table.UniqueConstraints != null)
            {
                int constraintIndex = 1;
                foreach (var uniqueColumns in table.UniqueConstraints)
                {
                    int position = 1;
                    foreach (var columnName in uniqueColumns)
                    {
                        yield return new WitSqlRow([
                            WitSqlValue.FromText("WitDB"),
                            WitSqlValue.FromText("public"),
                            WitSqlValue.FromText($"UQ_{table.Name}_{constraintIndex}"),
                            WitSqlValue.FromText("WitDB"),
                            WitSqlValue.FromText("public"),
                            WitSqlValue.FromText(table.Name),
                            WitSqlValue.FromText(columnName),
                            WitSqlValue.FromInt(position++),
                            WitSqlValue.Null,
                            WitSqlValue.Null,
                            WitSqlValue.Null,
                            WitSqlValue.Null,
                        ], KEY_COLUMN_USAGE_COLUMNS);
                    }
                    constraintIndex++;
                }
            }
            
            // Column-level unique constraints (from IsUnique property)
            int colUniqueIndex = 1;
            foreach (var column in table.Columns)
            {
                if (column.IsUnique && !column.IsPrimaryKey)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"UQ_{table.Name}_{column.Name}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText(column.Name),
                        WitSqlValue.FromInt(1),
                        WitSqlValue.Null,
                        WitSqlValue.Null,
                        WitSqlValue.Null,
                        WitSqlValue.Null,
                    ], KEY_COLUMN_USAGE_COLUMNS);
                    colUniqueIndex++;
                }
            }

            // Table-level foreign key columns
            if (table.ForeignKeys != null)
            {
                int fkIndex = 1;
                foreach (var fk in table.ForeignKeys)
                {
                    int position = 1;
                    for (int i = 0; i < fk.Columns.Count; i++)
                    {
                        var localColumn = fk.Columns[i];
                        var foreignColumn = fk.ForeignColumns != null && i < fk.ForeignColumns.Count
                            ? fk.ForeignColumns[i]
                            : localColumn;

                        yield return new WitSqlRow([
                            WitSqlValue.FromText("WitDB"),
                            WitSqlValue.FromText("public"),
                            WitSqlValue.FromText($"FK_{table.Name}_{fk.ForeignTable}_{fkIndex}"),
                            WitSqlValue.FromText("WitDB"),
                            WitSqlValue.FromText("public"),
                            WitSqlValue.FromText(table.Name),
                            WitSqlValue.FromText(localColumn),
                            WitSqlValue.FromInt(position++),
                            WitSqlValue.FromInt(position - 1),
                            WitSqlValue.FromText("public"),
                            WitSqlValue.FromText(fk.ForeignTable),
                            WitSqlValue.FromText(foreignColumn),
                        ], KEY_COLUMN_USAGE_COLUMNS);
                    }
                    fkIndex++;
                }
            }
            
            // Column-level foreign key constraints (from ForeignKey property)
            foreach (var column in table.Columns)
            {
                if (column.ForeignKey != null)
                {
                    var fk = column.ForeignKey;
                    var foreignColumn = fk.ForeignColumns != null && fk.ForeignColumns.Count > 0
                        ? fk.ForeignColumns[0]
                        : column.Name;

                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"FK_{table.Name}_{fk.ForeignTable}_{column.Name}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText(column.Name),
                        WitSqlValue.FromInt(1),
                        WitSqlValue.FromInt(1),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(fk.ForeignTable),
                        WitSqlValue.FromText(foreignColumn),
                    ], KEY_COLUMN_USAGE_COLUMNS);
                }
            }
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.KEY_COLUMN_USAGE.
    /// </summary>
    public static IReadOnlyList<string> GetKeyColumnUsageColumns() => KEY_COLUMN_USAGE_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.KEY_COLUMN_USAGE.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetKeyColumnUsageColumnTypes() => KEY_COLUMN_USAGE_TYPES;

    #endregion

    #region INFORMATION_SCHEMA.TABLE_CONSTRAINTS

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.TABLE_CONSTRAINTS view data.
    /// Returns information about table constraints.
    /// </summary>
    public IEnumerable<WitSqlRow> GetTableConstraints()
    {
        foreach (var table in m_catalog.Tables)
        {
            // Primary key
            if (table.PrimaryKey != null)
            {
                yield return new WitSqlRow([
                    WitSqlValue.FromText("WitDB"),                    // CONSTRAINT_CATALOG
                    WitSqlValue.FromText("public"),                   // CONSTRAINT_SCHEMA
                    WitSqlValue.FromText($"PK_{table.Name}"),         // CONSTRAINT_NAME
                    WitSqlValue.FromText("WitDB"),                    // TABLE_CATALOG
                    WitSqlValue.FromText("public"),                   // TABLE_SCHEMA
                    WitSqlValue.FromText(table.Name),                 // TABLE_NAME
                    WitSqlValue.FromText("PRIMARY KEY"),              // CONSTRAINT_TYPE
                    WitSqlValue.FromText("NO"),                       // IS_DEFERRABLE
                    WitSqlValue.FromText("NO"),                       // INITIALLY_DEFERRED
                ], TABLE_CONSTRAINTS_COLUMNS);
            }

            // Table-level unique constraints
            if (table.UniqueConstraints != null)
            {
                int constraintIndex = 1;
                foreach (var _ in table.UniqueConstraints)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"UQ_{table.Name}_{constraintIndex++}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText("UNIQUE"),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                }
            }
            
            // Column-level unique constraints
            foreach (var column in table.Columns)
            {
                if (column.IsUnique && !column.IsPrimaryKey)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"UQ_{table.Name}_{column.Name}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText("UNIQUE"),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                }
            }

            // Table-level foreign keys
            if (table.ForeignKeys != null)
            {
                int fkIndex = 1;
                foreach (var fk in table.ForeignKeys)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"FK_{table.Name}_{fk.ForeignTable}_{fkIndex++}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText("FOREIGN KEY"),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                }
            }
            
            // Column-level foreign key constraints
            foreach (var column in table.Columns)
            {
                if (column.ForeignKey != null)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"FK_{table.Name}_{column.ForeignKey.ForeignTable}_{column.Name}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText("FOREIGN KEY"),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                }
            }

            // Table-level check constraints
            if (table.CheckExpressions != null)
            {
                int checkIndex = 1;
                foreach (var _ in table.CheckExpressions)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"CK_{table.Name}_{checkIndex++}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText("CHECK"),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                }
            }
            
            // Column-level check constraints
            int colCheckIndex = 1;
            foreach (var column in table.Columns)
            {
                if (!string.IsNullOrEmpty(column.CheckExpression))
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText($"CK_{table.Name}_{column.Name}"),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText("CHECK"),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                    colCheckIndex++;
                }
            }

            // Named constraints
            if (table.NamedConstraints != null)
            {
                foreach (var constraint in table.NamedConstraints)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(constraint.Name),
                        WitSqlValue.FromText("WitDB"),
                        WitSqlValue.FromText("public"),
                        WitSqlValue.FromText(table.Name),
                        WitSqlValue.FromText(GetConstraintTypeName(constraint.Type)),
                        WitSqlValue.FromText("NO"),
                        WitSqlValue.FromText("NO"),
                    ], TABLE_CONSTRAINTS_COLUMNS);
                }
            }
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.TABLE_CONSTRAINTS.
    /// </summary>
    public static IReadOnlyList<string> GetTableConstraintsColumns() => TABLE_CONSTRAINTS_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.TABLE_CONSTRAINTS.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetTableConstraintsColumnTypes() => TABLE_CONSTRAINTS_TYPES;

    #endregion

    #region INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS view data.
    /// Returns information about foreign key constraints.
    /// </summary>
    public IEnumerable<WitSqlRow> GetReferentialConstraints()
    {
        foreach (var table in m_catalog.Tables)
        {
            // Table-level foreign keys
            if (table.ForeignKeys != null)
            {
                int fkIndex = 1;
                foreach (var fk in table.ForeignKeys)
                {
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),                                              // CONSTRAINT_CATALOG
                        WitSqlValue.FromText("public"),                                             // CONSTRAINT_SCHEMA
                        WitSqlValue.FromText($"FK_{table.Name}_{fk.ForeignTable}_{fkIndex++}"),     // CONSTRAINT_NAME
                        WitSqlValue.FromText("WitDB"),                                              // UNIQUE_CONSTRAINT_CATALOG
                        WitSqlValue.FromText("public"),                                             // UNIQUE_CONSTRAINT_SCHEMA
                        WitSqlValue.FromText($"PK_{fk.ForeignTable}"),                              // UNIQUE_CONSTRAINT_NAME
                        WitSqlValue.FromText("NONE"),                                               // MATCH_OPTION
                        WitSqlValue.FromText(GetReferenceActionName(fk.OnUpdate)),                  // UPDATE_RULE
                        WitSqlValue.FromText(GetReferenceActionName(fk.OnDelete)),                  // DELETE_RULE
                    ], REFERENTIAL_CONSTRAINTS_COLUMNS);
                }
            }
            
            // Column-level foreign key constraints
            foreach (var column in table.Columns)
            {
                if (column.ForeignKey != null)
                {
                    var fk = column.ForeignKey;
                    yield return new WitSqlRow([
                        WitSqlValue.FromText("WitDB"),                                              // CONSTRAINT_CATALOG
                        WitSqlValue.FromText("public"),                                             // CONSTRAINT_SCHEMA
                        WitSqlValue.FromText($"FK_{table.Name}_{fk.ForeignTable}_{column.Name}"),   // CONSTRAINT_NAME
                        WitSqlValue.FromText("WitDB"),                                              // UNIQUE_CONSTRAINT_CATALOG
                        WitSqlValue.FromText("public"),                                             // UNIQUE_CONSTRAINT_SCHEMA
                        WitSqlValue.FromText($"PK_{fk.ForeignTable}"),                              // UNIQUE_CONSTRAINT_NAME
                        WitSqlValue.FromText("NONE"),                                               // MATCH_OPTION
                        WitSqlValue.FromText(GetReferenceActionName(fk.OnUpdate)),                  // UPDATE_RULE
                        WitSqlValue.FromText(GetReferenceActionName(fk.OnDelete)),                  // DELETE_RULE
                    ], REFERENTIAL_CONSTRAINTS_COLUMNS);
                }
            }
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS.
    /// </summary>
    public static IReadOnlyList<string> GetReferentialConstraintsColumns() => REFERENTIAL_CONSTRAINTS_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetReferentialConstraintsColumnTypes() => REFERENTIAL_CONSTRAINTS_TYPES;

    #endregion

    #region INFORMATION_SCHEMA.INDEXES

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.INDEXES view data.
    /// Returns information about all indexes (non-standard extension).
    /// </summary>
    public IEnumerable<WitSqlRow> GetIndexes()
    {
        foreach (var index in m_catalog.GetIndexes())
        {
            int position = 1;
            foreach (var columnName in index.Columns)
            {
                yield return new WitSqlRow([
                    WitSqlValue.FromText("WitDB"),                                     // TABLE_CATALOG
                    WitSqlValue.FromText("public"),                                    // TABLE_SCHEMA
                    WitSqlValue.FromText(index.TableName),                             // TABLE_NAME
                    WitSqlValue.FromText(index.Name),                                  // INDEX_NAME
                    WitSqlValue.FromText(columnName),                                  // COLUMN_NAME
                    WitSqlValue.FromInt(position++),                                   // ORDINAL_POSITION
                    WitSqlValue.FromText(index.IsUnique ? "YES" : "NO"),               // IS_UNIQUE
                    WitSqlValue.Null,                                                  // INDEX_TYPE (B-tree, etc.)
                    index.WhereExpression != null 
                        ? WitSqlValue.FromText(index.WhereExpression) 
                        : WitSqlValue.Null,                                            // FILTER_CONDITION
                ], INDEXES_COLUMNS);
            }
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.INDEXES.
    /// </summary>
    public static IReadOnlyList<string> GetIndexesColumns() => INDEXES_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.INDEXES.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetIndexesColumnTypes() => INDEXES_TYPES;

    #endregion

    #region INFORMATION_SCHEMA.VIEWS

    /// <summary>
    /// Gets the INFORMATION_SCHEMA.VIEWS view data.
    /// Returns information about all views.
    /// </summary>
    public IEnumerable<WitSqlRow> GetViews()
    {
        foreach (var view in m_catalog.GetViews())
        {
            yield return new WitSqlRow([
                WitSqlValue.FromText("WitDB"),                     // TABLE_CATALOG
                WitSqlValue.FromText("public"),                    // TABLE_SCHEMA
                WitSqlValue.FromText(view.Name),                   // TABLE_NAME
                WitSqlValue.FromText(view.SelectSql),              // VIEW_DEFINITION
                WitSqlValue.FromText("NONE"),                      // CHECK_OPTION
                WitSqlValue.FromText("NO"),                        // IS_UPDATABLE
            ], VIEWS_COLUMNS);
        }
    }

    /// <summary>
    /// Gets the column definitions for INFORMATION_SCHEMA.VIEWS.
    /// </summary>
    public static IReadOnlyList<string> GetViewsColumns() => VIEWS_COLUMNS;

    /// <summary>
    /// Gets the column types for INFORMATION_SCHEMA.VIEWS.
    /// </summary>
    public static IReadOnlyList<WitSqlType> GetViewsColumnTypes() => VIEWS_TYPES;

    #endregion

    #region Helpers

    private static string GetDataTypeName(WitDataType type)
    {
        return type switch
        {
            WitDataType.Boolean => "BOOLEAN",
            WitDataType.Int8 => "TINYINT",
            WitDataType.UInt8 => "UTINYINT",
            WitDataType.Int16 => "SMALLINT",
            WitDataType.UInt16 => "USMALLINT",
            WitDataType.Int32 => "INTEGER",
            WitDataType.UInt32 => "UINT",
            WitDataType.Int64 => "BIGINT",
            WitDataType.UInt64 => "UBIGINT",
            WitDataType.Float16 => "FLOAT16",
            WitDataType.Float32 => "FLOAT",
            WitDataType.Float64 => "DOUBLE",
            WitDataType.Decimal => "DECIMAL",
            WitDataType.DateTime => "DATETIME",
            WitDataType.DateOnly => "DATE",
            WitDataType.TimeOnly => "TIME",
            WitDataType.DateTimeOffset => "DATETIMEOFFSET",
            WitDataType.TimeSpan => "INTERVAL",
            WitDataType.Guid => "GUID",
            WitDataType.StringFixed => "CHAR",
            WitDataType.StringVariable => "VARCHAR",
            WitDataType.BinaryFixed => "BINARY",
            WitDataType.BinaryVariable => "VARBINARY",
            WitDataType.RowVersion => "ROWVERSION",
            WitDataType.Json => "JSON",
            _ => type.ToString().ToUpperInvariant()
        };
    }

    private static string GetConstraintTypeName(ConstraintType type)
    {
        return type switch
        {
            ConstraintType.Check => "CHECK",
            ConstraintType.Unique => "UNIQUE",
            ConstraintType.ForeignKey => "FOREIGN KEY",
            ConstraintType.PrimaryKey => "PRIMARY KEY",
            _ => "UNKNOWN"
        };
    }

    private static string GetReferenceActionName(ReferenceAction action)
    {
        return action switch
        {
            ReferenceAction.NoAction => "NO ACTION",
            ReferenceAction.Restrict => "RESTRICT",
            ReferenceAction.Cascade => "CASCADE",
            ReferenceAction.SetNull => "SET NULL",
            ReferenceAction.SetDefault => "SET DEFAULT",
            _ => "NO ACTION"
        };
    }

    #endregion
}
