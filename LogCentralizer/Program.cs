using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string root = Environment.GetEnvironmentVariable("EASYSAVE_CENTRAL_LOG_DIR")
              ?? Path.Combine(AppContext.BaseDirectory, "CentralLogs");
Directory.CreateDirectory(root);

var locks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
object GetLock(string path) => locks.GetOrAdd(path, _ => new object());

app.MapGet("/", () => Results.Ok(new
{
    service = "EasySave Log Centralizer",
    status = "ok",
    storage = root
}));

app.MapPost("/api/logs", async (HttpContext ctx) =>
{
    JsonNode? payload = await JsonNode.ParseAsync(ctx.Request.Body);
    if (payload is null)
        return Results.BadRequest("Invalid JSON payload");

    string clientId = payload["clientId"]?.GetValue<string>() ?? "unknown-client";
    JsonNode? entry = payload["entry"];
    if (entry is null)
        return Results.BadRequest("Missing 'entry' object");

    string day = DateTime.UtcNow.ToString("yyyy-MM-dd");
    string file = Path.Combine(root, $"{day}.ndjson");

    var lineObj = new JsonObject
    {
        ["serverTimestamp"] = DateTime.UtcNow.ToString("o"),
        ["clientId"] = clientId,
        ["format"] = payload["format"]?.GetValue<string>() ?? "Unknown",
        ["entry"] = entry.DeepClone()
    };

    string line = lineObj.ToJsonString() + Environment.NewLine;
    lock (GetLock(file))
    {
        File.AppendAllText(file, line);
    }

    return Results.Accepted();
});

app.Run("http://0.0.0.0:8080");
