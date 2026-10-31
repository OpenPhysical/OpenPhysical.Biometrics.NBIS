// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers fingerprint processing. Native loading waits until the first operation.</summary>
public static class FingerprintServiceCollectionExtensions
{
    /// <summary>Adds the thread-safe NBIS implementation of <see cref="IFingerprintEngine"/>.</summary>
    /// <param name="services">Application service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <remarks>Registration is idempotent and preserves an existing <see cref="IFingerprintEngine"/> registration.
    /// The singleton uses a registered Microsoft logger when one is available. Native loading waits until
    /// the first extract or match.</remarks>
    public static IServiceCollection AddFingerprintBiometrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFingerprintEngine, NbisFingerprintEngine>();
        return services;
    }
}
