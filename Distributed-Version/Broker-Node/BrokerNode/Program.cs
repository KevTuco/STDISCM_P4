using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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

// Initialize our nodes with names, URLs, and default settings.
// We have non-DB nodes: View Node, AuthController, CoursesController, GradesController, ScheduleController
// Then DB nodes: two for each table (Courses, Grades, Users)
var initialNodes = new List<NodeStatus>
{
    new NodeStatus { Name = "View Node", Url = "http://localhost:5001", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "AuthController", Url = "http://localhost:5002", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "CoursesController", Url = "http://localhost:5003", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "GradesController", Url = "http://localhost:5004", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "ScheduleController", Url = "http://localhost:5005", IsOnline = false, IsActivated = false, Latency = 0 },
    
    // DB Nodes for Courses
    new NodeStatus { Name = "CoursesDb1", Url = "http://localhost:5006", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "CoursesDb2", Url = "http://localhost:5007", IsOnline = false, IsActivated = false, Latency = 0 },

    // DB Nodes for Grades
    new NodeStatus { Name = "GradesDb1", Url = "http://localhost:5008", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "GradesDb2", Url = "http://localhost:5009", IsOnline = false, IsActivated = false, Latency = 0 },

    // DB Nodes for Users
    new NodeStatus { Name = "UsersDb1", Url = "http://localhost:5010", IsOnline = false, IsActivated = false, Latency = 0 },
    new NodeStatus { Name = "UsersDb2", Url = "http://localhost:5011", IsOnline = false, IsActivated = false, Latency = 0 }
};

foreach (var node in initialNodes)
{
    nodes[node.Name] = node;
}

var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

// Every 5 seconds, "ping" every node by calling its GET /status endpoint.
// (We now interpret Latency as milliseconds.)
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
                await Task.Delay(node.Latency);
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
    catch (Exception ex)
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

    // Prepare the debug string using milliseconds.
    var statusText = update.IsActivated ? "Activated" : "Deactivated";
    var debugMessage = $"[DEBUG] Status: {statusText} | Latency: {update.Latency} ms";

    // Send the debug message to the node's /config endpoint.
    try
    {
        var client = httpClientFactory.CreateClient();
        // Send a plain text message.
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
    // Wait for the latency value (in milliseconds) before forwarding.
    await Task.Delay(controller.Latency);

    // Forward the call. (This assumes the controller has a /process endpoint.)
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
// Forwards a DB query from a controller to one of the DB nodes according to selection rules.
// Expects a query parameter "dbType" whose value is "Courses", "Grades", or "Users".
app.MapPost("/api/forward/db", async (HttpContext context) =>
{
    // Extract dbType from query string.
    var dbType = context.Request.Query["dbType"].ToString();
    if (string.IsNullOrEmpty(dbType))
    {
        return Results.BadRequest("dbType query parameter is required.");
    }
    
    // Select candidate DB nodes based on dbType.
    List<NodeStatus> candidateDbNodes = dbType.ToLower() switch
    {
        "courses" => nodes.Values.Where(n => n.Name.StartsWith("CoursesDb")).ToList(),
        "grades"  => nodes.Values.Where(n => n.Name.StartsWith("GradesDb")).ToList(),
        "users"   => nodes.Values.Where(n => n.Name.StartsWith("UsersDb")).ToList(),
        _ => null
    };

    if (candidateDbNodes == null || candidateDbNodes.Count == 0)
    {
        return Results.BadRequest("Invalid dbType specified.");
    }

    // Choose the best candidate among the ones that are online (selecting the one with the lowest latency).
    NodeStatus? chosenDB = candidateDbNodes
        .Where(db => db.IsOnline)
        .OrderBy(db => db.Latency)
        .FirstOrDefault();

    if (chosenDB == null)
    {
        return Results.BadRequest($"No {dbType} DB nodes are currently online.");
    }

    // Wait for the chosen DB node's latency (in milliseconds).
    await Task.Delay(chosenDB.Latency);

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

// Record definitions.
record NodeStatus
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsActivated { get; set; }
    public int Latency { get; set; } // Now interpreted in milliseconds.
}

record NodeUpdate
{
    public bool IsActivated { get; init; }
    public int Latency { get; init; } // Milliseconds.
}
