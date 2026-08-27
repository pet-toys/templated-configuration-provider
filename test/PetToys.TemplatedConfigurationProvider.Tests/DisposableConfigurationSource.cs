using System;
using Microsoft.Extensions.Configuration;

namespace PetToys.TemplatedConfigurationProvider.Tests;

/// <summary>
/// A configuration source whose provider records that it was disposed, so a
/// test can observe whether the templated provider released the inner
/// <see cref="ConfigurationRoot"/> it built (the root disposes the providers it
/// owns).
/// </summary>
internal sealed class DisposableConfigurationSource : IConfigurationSource
{
    public bool ProviderDisposed { get; private set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new DisposableProvider(this);

    private sealed class DisposableProvider(DisposableConfigurationSource owner)
        : ConfigurationProvider, IDisposable
    {
        public void Dispose() => owner.ProviderDisposed = true;
    }
}
