using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace OutWit.Database.EntityFramework.Design.Internal;

/// <summary>
/// Generates C# code for WitDatabase-specific annotations.
/// </summary>
public class WitAnnotationCodeGenerator : AnnotationCodeGenerator
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WitAnnotationCodeGenerator"/> class.
    /// </summary>
    public WitAnnotationCodeGenerator(AnnotationCodeGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
        IModel model,
        IDictionary<string, IAnnotation> annotations)
    {
        var fragments = new List<MethodCallCodeFragment>(base.GenerateFluentApiCalls(model, annotations));
        
        // Add any WitDatabase-specific model annotations here
        
        return fragments;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
        IProperty property,
        IDictionary<string, IAnnotation> annotations)
    {
        var fragments = new List<MethodCallCodeFragment>(base.GenerateFluentApiCalls(property, annotations));
        
        // Handle WitDb:Autoincrement annotation
        if (annotations.TryGetValue("WitDb:Autoincrement", out var autoincrementAnnotation) &&
            autoincrementAnnotation.Value is bool isAutoincrement && isAutoincrement)
        {
            fragments.Add(new MethodCallCodeFragment("UseAutoincrement"));
            annotations.Remove("WitDb:Autoincrement");
        }
        
        return fragments;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
        IEntityType entityType,
        IDictionary<string, IAnnotation> annotations)
    {
        var fragments = new List<MethodCallCodeFragment>(base.GenerateFluentApiCalls(entityType, annotations));
        
        // Add any WitDatabase-specific entity type annotations here
        
        return fragments;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
        IIndex index,
        IDictionary<string, IAnnotation> annotations)
    {
        var fragments = new List<MethodCallCodeFragment>(base.GenerateFluentApiCalls(index, annotations));
        
        // Add any WitDatabase-specific index annotations here
        
        return fragments;
    }

    #endregion
}
