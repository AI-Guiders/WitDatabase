using System.Reflection;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace OutWit.Database.Studio.Syntax;

/// <summary>
/// Provides WitSQL syntax highlighting for AvaloniaEdit.
/// </summary>
public static class WitSqlHighlighting
{
    #region Fields

    private static IHighlightingDefinition? s_definition;
    private static readonly object s_lock = new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets the WitSQL syntax highlighting definition.
    /// </summary>
    public static IHighlightingDefinition Definition
    {
        get
        {
            if (s_definition != null)
                return s_definition;

            lock (s_lock)
            {
                s_definition ??= CreateDefinition();
            }

            return s_definition;
        }
    }

    #endregion

    #region Functions

    private static IHighlightingDefinition CreateDefinition()
    {
        // Try to load from embedded resource first
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "OutWit.Database.Studio.Syntax.WitSql.xshd";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }

        // Fallback: create programmatically
        return CreateProgrammaticDefinition();
    }

    private static IHighlightingDefinition CreateProgrammaticDefinition()
    {
        var xshd = @"<?xml version=""1.0""?>
<SyntaxDefinition name=""WitSQL"" extensions="".sql;.witsql"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <Color name=""Comment"" foreground=""#6A9955"" fontStyle=""italic"" />
  <Color name=""String"" foreground=""#CE9178"" />
  <Color name=""Keyword"" foreground=""#569CD6"" fontWeight=""bold"" />
  <Color name=""DataType"" foreground=""#4EC9B0"" />
  <Color name=""Function"" foreground=""#DCDCAA"" />
  <Color name=""Number"" foreground=""#B5CEA8"" />
  <Color name=""Null"" foreground=""#569CD6"" fontStyle=""italic"" />
  <Color name=""Boolean"" foreground=""#569CD6"" />
  
  <RuleSet ignoreCase=""true"">
    <Span color=""Comment"" begin=""--"" />
    <Span color=""Comment"" multiline=""true"" begin=""/\*"" end=""\*/"" />
    <Span color=""String"" begin=""'"" end=""'"" escapeCharacter=""'"" />
    <Rule color=""Number"">\b\d+\.?\d*\b</Rule>
    <Keywords color=""Null""><Word>NULL</Word></Keywords>
    <Keywords color=""Boolean""><Word>TRUE</Word><Word>FALSE</Word></Keywords>
    <Keywords color=""Keyword"">
      <Word>SELECT</Word><Word>FROM</Word><Word>WHERE</Word><Word>INSERT</Word><Word>INTO</Word>
      <Word>VALUES</Word><Word>UPDATE</Word><Word>SET</Word><Word>DELETE</Word><Word>CREATE</Word>
      <Word>TABLE</Word><Word>VIEW</Word><Word>INDEX</Word><Word>DROP</Word><Word>ALTER</Word>
      <Word>PRIMARY</Word><Word>KEY</Word><Word>FOREIGN</Word><Word>REFERENCES</Word><Word>UNIQUE</Word>
      <Word>AS</Word><Word>ON</Word><Word>AND</Word><Word>OR</Word><Word>NOT</Word><Word>IN</Word>
      <Word>JOIN</Word><Word>INNER</Word><Word>LEFT</Word><Word>RIGHT</Word><Word>OUTER</Word>
      <Word>ORDER</Word><Word>BY</Word><Word>ASC</Word><Word>DESC</Word><Word>GROUP</Word><Word>HAVING</Word>
      <Word>LIMIT</Word><Word>OFFSET</Word><Word>UNION</Word><Word>DISTINCT</Word><Word>ALL</Word>
      <Word>CASE</Word><Word>WHEN</Word><Word>THEN</Word><Word>ELSE</Word><Word>END</Word>
      <Word>BEGIN</Word><Word>COMMIT</Word><Word>ROLLBACK</Word><Word>TRANSACTION</Word>
    </Keywords>
    <Keywords color=""DataType"">
      <Word>INT</Word><Word>INTEGER</Word><Word>BIGINT</Word><Word>SMALLINT</Word><Word>TINYINT</Word>
      <Word>FLOAT</Word><Word>DOUBLE</Word><Word>DECIMAL</Word><Word>VARCHAR</Word><Word>TEXT</Word>
      <Word>BOOLEAN</Word><Word>BOOL</Word><Word>DATE</Word><Word>DATETIME</Word><Word>TIMESTAMP</Word>
      <Word>BLOB</Word><Word>GUID</Word><Word>UUID</Word><Word>JSON</Word>
    </Keywords>
    <Keywords color=""Function"">
      <Word>COUNT</Word><Word>SUM</Word><Word>AVG</Word><Word>MIN</Word><Word>MAX</Word>
      <Word>UPPER</Word><Word>LOWER</Word><Word>LENGTH</Word><Word>SUBSTR</Word><Word>TRIM</Word>
      <Word>COALESCE</Word><Word>NULLIF</Word><Word>NOW</Word><Word>NEWGUID</Word>
    </Keywords>
  </RuleSet>
</SyntaxDefinition>";

        using var reader = XmlReader.Create(new StringReader(xshd));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    /// <summary>
    /// Registers WitSQL highlighting with the global HighlightingManager.
    /// </summary>
    public static void Register()
    {
        HighlightingManager.Instance.RegisterHighlighting(
            "WitSQL",
            [".sql", ".witsql"],
            Definition);
    }

    #endregion
}
