using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Protocol;

using Rhombus.WinFormsMcp.Server.Tools;

namespace Rhombus.WinFormsMcp.Server;

internal static class McpServerRegistration {
    public static IServiceCollection AddWinFormsMcpServer(this IServiceCollection services) {
        services.AddWinFormsToolHandlers();
        services.AddSingleton<ToolRegistry>();

        services
            .AddMcpServer(options => {
                options.ServerInfo = new Implementation {
                    Name = "Rhombus.WinFormsMcp",
                    Title = "WinForms MCP",
                    Version = ServerVersion.Current,
                    Description = "WinForms development, automation, inspection, and rendering tools"
                };
            })
            .WithStdioServerTransport()
            .WithListToolsHandler((context, _) => {
                var registry = context.Services!.GetRequiredService<ToolRegistry>();
                return ValueTask.FromResult(new ListToolsResult { Tools = registry.Tools });
            })
            .WithCallToolHandler((context, cancellationToken) => {
                var registry = context.Services!.GetRequiredService<ToolRegistry>();
                return registry.ExecuteAsync(context.Params, cancellationToken);
            });

        return services;
    }
}