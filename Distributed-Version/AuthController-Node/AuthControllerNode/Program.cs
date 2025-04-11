using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// GET /status endpoint.
app.MapGet("/status", () =>
{
    return Results.Ok(new { Name = "AuthController", Status = "Online" });
});

// POST /config endpoint that logs the received debug message.
app.MapPost("/config", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var debugMessage = await reader.ReadToEndAsync();
    Console.WriteLine($"[AuthController] Received config update: {debugMessage}");
    return Results.Ok(new { Message = "Config updated on AuthController" });
});

// New endpoint: POST /process to simulate processing a forwarded message.
app.MapPost("/process", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var payload = await reader.ReadToEndAsync();
    Console.WriteLine($"[AuthController] Processing forwarded payload: {payload}");
    return Results.Ok(new { Message = "Request processed by AuthController" });
});

app.Run();
