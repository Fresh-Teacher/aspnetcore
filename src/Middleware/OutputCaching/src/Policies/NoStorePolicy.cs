// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.OutputCaching;

/// <summary>
/// A policy that prevents the response from being cached.
/// </summary>
internal class NoStorePolicy : IOutputCachingPolicy
{
    public static NoStorePolicy Instance = new();

    private NoStorePolicy()
    {
    }

    /// <inheritdoc />
    Task IOutputCachingPolicy.OnServeResponseAsync(IOutputCachingContext context)
    {
        context.AllowCacheStorage = false;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task IOutputCachingPolicy.OnServeFromCacheAsync(IOutputCachingContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task IOutputCachingPolicy.OnRequestAsync(IOutputCachingContext context)
    {
        return Task.CompletedTask;
    }
}
