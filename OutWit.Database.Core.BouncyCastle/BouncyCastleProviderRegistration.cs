using System.Runtime.CompilerServices;
using OutWit.Database.Core.Interfaces;
using OutWit.Database.Core.Providers;

namespace OutWit.Database.Core.BouncyCastle;

/// <summary>
/// Registers BouncyCastle crypto providers with the ProviderRegistry.
/// Called automatically via ModuleInitializer when the assembly is loaded.
/// </summary>
public static class BouncyCastleProviderRegistration
{
    private static bool m_initialized;
    private static readonly Lock m_lock = new();

    /// <summary>
    /// Registers BouncyCastle providers. Safe to call multiple times.
    /// Called automatically via ModuleInitializer, but can be called explicitly
    /// to ensure registration before using providers.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        if (m_initialized) return;

        lock (m_lock)
        {
            if (m_initialized) return;

            RegisterCryptoProviders();

            m_initialized = true;
        }
    }

    /// <summary>
    /// Ensures BouncyCastle providers are registered.
    /// Alias for Initialize() for more explicit API.
    /// </summary>
    public static void EnsureRegistered() => Initialize();

    private static void RegisterCryptoProviders()
    {
        // ChaCha20-Poly1305
        ProviderRegistry.Instance.RegisterOrReplace<ICryptoProvider>(BouncyCastleCryptoProvider.PROVIDER_KEY, p =>
        {
            var key = p.GetRequired<byte[]>("key");
            return new BouncyCastleCryptoProvider(key);
        });
    }
}
