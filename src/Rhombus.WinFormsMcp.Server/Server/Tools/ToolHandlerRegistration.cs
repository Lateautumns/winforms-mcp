using Microsoft.Extensions.DependencyInjection;

namespace Rhombus.WinFormsMcp.Server.Tools;

internal static class ToolHandlerRegistration {
    public static IServiceCollection AddWinFormsToolHandlers(this IServiceCollection services) {
        var handlerType = typeof(IToolHandler);
        var implementations = handlerType.Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && handlerType.IsAssignableFrom(type));

        foreach (var implementation in implementations)
            services.AddSingleton(handlerType, implementation);

        return services;
    }
}