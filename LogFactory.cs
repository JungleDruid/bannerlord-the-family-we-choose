using System;
using Bannerlord.ButterLib.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TheFamilyWeChoose;

internal static class LogFactory
{
    internal static ILogger Get<T>()
    {
        IServiceProvider serviceProvider = SubModule.Instance?.GetServiceProvider() ?? SubModule.Instance?.GetTempServiceProvider();

        return serviceProvider?.GetRequiredService<ILogger<T>>() ?? NullLogger<T>.Instance;
    }
}