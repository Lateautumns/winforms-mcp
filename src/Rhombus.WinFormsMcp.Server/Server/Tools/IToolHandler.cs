using System.Text.Json;

namespace Rhombus.WinFormsMcp.Server.Tools;

internal interface IToolHandler {
    string Name { get; }

    ValueTask<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}