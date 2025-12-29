using Microsoft.EntityFrameworkCore.Query;
using OutWit.Database.EntityFramework.Query.Translators;

namespace OutWit.Database.EntityFramework.Query;

/// <summary>
/// Provider for WitDatabase method call translators.
/// </summary>
public sealed class WitMethodCallTranslatorProvider : RelationalMethodCallTranslatorProvider
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitMethodCallTranslatorProvider"/> class.
    /// </summary>
    /// <param name="dependencies">The dependencies.</param>
    public WitMethodCallTranslatorProvider(RelationalMethodCallTranslatorProviderDependencies dependencies)
        : base(dependencies)
    {
        var sqlExpressionFactory = dependencies.SqlExpressionFactory;
        var typeMappingSource = dependencies.RelationalTypeMappingSource;

        AddTranslators(
        [
            new WitStringMethodTranslator(sqlExpressionFactory),
            new WitMathMethodTranslator(sqlExpressionFactory),
            new WitDateTimeMethodTranslator(sqlExpressionFactory),
            new WitGuidMethodTranslator(sqlExpressionFactory),
            new WitJsonMethodTranslator(sqlExpressionFactory, typeMappingSource)
        ]);
    }

    #endregion
}
