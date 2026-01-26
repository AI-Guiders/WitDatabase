using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;

namespace OutWit.Database.EntityFramework.Design.Internal;

/// <summary>
/// Generates configuration code for WitDatabase provider.
/// </summary>
public class WitCodeGenerator : ProviderCodeGenerator
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitCodeGenerator"/> class.
    /// </summary>
    public WitCodeGenerator(ProviderCodeGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public override MethodCallCodeFragment GenerateUseProvider(
        string connectionString,
        MethodCallCodeFragment? providerOptions)
    {
        return new MethodCallCodeFragment(
            "UseWitDb",
            providerOptions == null
                ? [connectionString]
                : [connectionString, new NestedClosureCodeFragment("x", providerOptions)]);
    }

    #endregion
}
