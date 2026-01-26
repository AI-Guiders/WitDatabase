using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using OutWit.Database.EntityFramework.Design.Internal;
using OutWit.Database.EntityFramework.Extensions;

namespace OutWit.Database.EntityFramework.Design;

/// <summary>
/// Design-time services for WitDatabase EF Core provider.
/// Enables 'dotnet ef migrations' commands.
/// </summary>
public class WitDesignTimeServices : IDesignTimeServices
{
    #region IDesignTimeServices

    /// <summary>
    /// Configures design-time services for WitDatabase.
    /// </summary>
    public void ConfigureDesignTimeServices(IServiceCollection services)
    {
        // Add all WitDb EF Core services for type mapping etc.
        services.AddEntityFrameworkWitDb();
        
        // Add design-time specific services
        services.AddSingleton<AnnotationCodeGeneratorDependencies>();
        services.AddSingleton<IAnnotationCodeGenerator, WitAnnotationCodeGenerator>();
        services.AddSingleton<IDatabaseModelFactory, WitDatabaseModelFactory>();
        services.AddSingleton<IProviderConfigurationCodeGenerator, WitCodeGenerator>();
    }

    #endregion
}
