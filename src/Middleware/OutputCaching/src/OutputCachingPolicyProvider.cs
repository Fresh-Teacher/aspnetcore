// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.OutputCaching;

internal sealed class OutputCachingPolicyProvider : IOutputCachingPolicyProvider
{
    private readonly OutputCachingOptions _options;

    public OutputCachingPolicyProvider(IOptions<OutputCachingOptions> options)
    {
        _options = options.Value;
    }

    public bool HasPolicies(HttpContext httpContext)
    {
        if (_options.BasePolicy != null)
        {
            return true;
        }

        if (httpContext.Features.Get<IOutputCachingFeature>()?.Policies.Any() ?? false)
        {
            return true;
        }

        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<IPoliciesMetadata>()?.Policy != null)
        {
            return true;
        }

        return false;
    }

    public async Task OnRequestAsync(IOutputCachingContext context)
    {
        if (_options.BasePolicy != null)
        {
            await _options.BasePolicy.OnRequestAsync(context);
        }

        var policiesMetadata = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IPoliciesMetadata>();

        if (policiesMetadata != null)
        {
            // TODO: Log only?

            if (context.HttpContext.Response.HasStarted)
            {
                throw new InvalidOperationException("Can't define output caching policies after headers have been sent to client.");
            }

            await policiesMetadata.Policy.OnRequestAsync(context);
        }
    }

    public async Task OnServeFromCacheAsync(IOutputCachingContext context)
    {
        if (_options.BasePolicy != null)
        {
            await _options.BasePolicy.OnServeFromCacheAsync(context);
        }

        // Apply response policies defined on the feature, e.g. from action attributes

        var responsePolicies = context.HttpContext.Features.Get<IOutputCachingFeature>()?.Policies;

        if (responsePolicies != null)
        {
            foreach (var policy in responsePolicies)
            {
                await policy.OnServeFromCacheAsync(context);
            }
        }

        var policiesMetadata = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IPoliciesMetadata>();

        if (policiesMetadata != null)
        {
            await policiesMetadata.Policy.OnServeFromCacheAsync(context);
        }
    }

    public async Task OnServeResponseAsync(IOutputCachingContext context)
    {
        if (_options.BasePolicy != null)
        {
            await _options.BasePolicy.OnServeResponseAsync(context);
        }

        // Apply response policies defined on the feature, e.g. from action attributes

        var responsePolicies = context.HttpContext.Features.Get<IOutputCachingFeature>()?.Policies;

        if (responsePolicies != null)
        {
            foreach (var policy in responsePolicies)
            {
                await policy.OnServeResponseAsync(context);
            }
        }

        var policiesMetadata = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IPoliciesMetadata>();

        if (policiesMetadata != null)
        {
            await policiesMetadata.Policy.OnServeResponseAsync(context);
        }
    }
}
