using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Create builder and load configuration from appsettings.json
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// In‑memory node storage; each node’s record is updated on config updates or via ping.
var nodes = new ConcurrentDictionary<string, NodeStatus>();

// Initialize our nodes with names, URLs, and default settings. (These URLs and ports should be updated via the environment files on each node.)
var initialNodes = new List<NodeStatus>
{
    new NodeStatus { Name = "View Node", Url = "http://localhost:5001", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "AuthController", Url = "http://localhost:5002", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "CoursesController", Url = "http://localhost:5003", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "GradesController", Url = "http://localhost:5004", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "ScheduleController", Url = "http://localhost:5005", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "Edge1", Url = "http://localhost:5006", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "Edge2", Url = "http://localhost:5007", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "Central", Url = "http://localhost:5008", IsOnline = false, IsActivated = false, Latency = 0 }
};

foreach (var node in initialNodes)
{
    nodes[node.Name] = node;
}

var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

// Every 5 seconds, "ping" every node by calling its GET /status endpoint. 
// (We simulate latency by waiting and update online status accordingly.)
var pingTimer = new System.Timers.Timer(5000);
pingTimer.Elapsed += async (sender, args) =>
{
    foreach (var kv in nodes)
    {
        var node = kv.Value;
        try
        {
            var client = httpClientFactory.CreateClient();
            if (node.Latency > 0)
            {
                await Task.Delay(node.Latency * 1000);
            }
            var response = await client.GetAsync($"{node.Url}/status");
            node.IsOnline = response.IsSuccessStatusCode;
            if (!node.IsOnline)
            {
                // When unreachable, auto-deactivate and clear latency.
                node.IsActivated = false;
                node.Latency = 0;
            }
        }
        catch
        {
            node.IsOnline = false;
            node.IsActivated = false;
            node.Latency = 0;
        }
    }
};
pingTimer.Start();

// Endpoint: GET /api/nodes returns the list of node statuses.
app.MapGet("/api/nodes", () =>
{
    try 
    {
        var snapshot = nodes.Values.ToList();
        return Results.Ok(snapshot);
    }
    catch(Exception ex)
    {
        Console.WriteLine("Error serializing nodes: " + ex.Message);
        return Results.Problem("An error occurred while retrieving node statuses.");
    }
});

// Endpoint: POST /api/node/update/{nodeName}
// Updates activation/latency settings and sends a debug message to the affected node.
app.MapPost("/api/node/update/{nodeName}", async (string nodeName, NodeUpdate update) =>
{
    if (!nodes.TryGetValue(nodeName, out var node))
    {
        return Results.NotFound($"Node '{nodeName}' not found.");
    }
    if (!node.IsOnline)
    {
        return Results.BadRequest($"Node '{nodeName}' is offline. Update not permitted.");
    }

    // Record the new settings.
    node.IsActivated = update.IsActivated;
    node.Latency = update.Latency;

    // Prepare the debug string.
    var statusText = update.IsActivated ? "Activated" : "Deactivated";
    var debugMessage = $"[DEBUG] Status: {statusText} | Latency: {update.Latency} seconds";

    // Send the debug message to the node's /config endpoint.
    try
    {
        var client = httpClientFactory.CreateClient();
        // Here we send a plain text message.
        var content = new StringContent(JsonSerializer.Serialize(debugMessage), System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync($"{node.Url}/config", content);
        if (!resp.IsSuccessStatusCode)
        {
            return Results.Problem($"Failed to update node '{nodeName}' config on the node.");
        }
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error sending config update to node '{nodeName}': {ex.Message}");
    }
    return Results.Ok(node);
});

// Endpoint: POST /api/forward/controller/{controllerName}
// Forwards a message from the View to the designated controller if it is activated.
app.MapPost("/api/forward/controller/{controllerName}", async (string controllerName, HttpContext context) =>
{
    if (!nodes.TryGetValue(controllerName, out var controller))
    {
        return Results.NotFound($"Controller '{controllerName}' not found.");
    }
    if (!controller.IsOnline || !controller.IsActivated)
    {
        return Results.BadRequest($"Controller '{controllerName}' is not activated.");
    }
    // Wait for the latency value (in seconds) before forwarding.
    await Task.Delay(controller.Latency * 1000);

    // Forward the call. For this example, we assume the controller has a /process endpoint.
    try
    {
        var client = httpClientFactory.CreateClient();
        // Read payload from the original request.
        var payload = await new System.IO.StreamReader(context.Request.Body).ReadToEndAsync();
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{controller.Url}/process", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        return Results.Content(responseBody, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error forwarding to controller '{controllerName}': {ex.Message}");
    }
});

// Endpoint: POST /api/forward/db
// Forwards a DB query from a controller to one of the DB nodes, according to selection rules.
app.MapPost("/api/forward/db", async (HttpContext context) =>
{
    // For this example, we assume the request payload is passed through directly.
    // Determine which DB node to use among Edge1, EdgeRedundant, and Central.
    NodeStatus? chosenDB = null;
    var edge1 = nodes.GetValueOrDefault("Edge1");
    var edgeRedundant = nodes.GetValueOrDefault("EdgeRedundant");
    var central = nodes.GetValueOrDefault("Central");

    // Determine the best edge (if any are online).
    if (edge1 != null && edgeRedundant != null)
    {
        if (edge1.IsOnline && edgeRedundant.IsOnline)
        {
            chosenDB = (edge1.Latency <= edgeRedundant.Latency) ? edge1 : edgeRedundant;
        }
        else if (edge1.IsOnline)
        {
            chosenDB = edge1;
        }
        else if (edgeRedundant.IsOnline)
        {
            chosenDB = edgeRedundant;
        }
    }
    // If no edge is online, try Central.
    if (chosenDB == null && central != null && central.IsOnline)
    {
        chosenDB = central;
    }
    // If no DB node is available, throw an error.
    if (chosenDB == null)
    {
        return Results.BadRequest("No DB nodes are currently online.");
    }

    // Wait for the chosen DB node's latency.
    await Task.Delay(chosenDB.Latency * 1000);

    // Forward the payload to the chosen DB node's /query endpoint.
    try
    {
        var client = httpClientFactory.CreateClient();
        var payload = await new System.IO.StreamReader(context.Request.Body).ReadToEndAsync();
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{chosenDB.Url}/query", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        return Results.Content(responseBody, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error forwarding to DB node '{chosenDB.Name}': {ex.Message}");
    }
});

app.Run();

record NodeStatus
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsActivated { get; set; }
    public int Latency { get; set; }
}

record NodeUpdate
{
    public bool IsActivated { get; init; }
    public int Latency { get; init; }
}
