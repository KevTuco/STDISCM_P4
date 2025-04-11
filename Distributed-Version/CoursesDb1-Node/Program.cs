using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// GET /status endpoint.
app.MapGet("/status", () =>
{
    return Results.Ok(new { Name = "Edge2", Status = "Online" });
});

// POST /config endpoint: logs the debug string.
app.MapPost("/config", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var debugMessage = await reader.ReadToEndAsync();
    Console.WriteLine($"[Edge1 DB] Received config update: {debugMessage}");
    return Results.Ok(new { Message = "Config updated on Edge1" });
});

// New endpoint: POST /query to simulate a DB query.
app.MapPost("/query", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var payload = await reader.ReadToEndAsync();
    Console.WriteLine($"[Edge1 DB] Processing forwarded DB query: {payload}");
    return Results.Ok(new { Message = "Query processed by Edge1" });
});

app.Run();
