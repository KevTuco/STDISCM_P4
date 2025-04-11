using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

// Create builder and add necessary services.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

// Retrieve secure values from configuration or environment (for simplicity, hardcoded here)
string secretKey = "YourVeryVeryVerySecureSecretKey123!";
string brokerUrl = "http://localhost:5000";  // Broker base URL

// GET /status endpoint: Reports that AuthController is online.
app.MapGet("/status", () =>
{
    return Results.Ok(new { Name = "AuthController", Status = "Online" });
});

// POST /config endpoint: Receives configuration updates from the Broker.
app.MapPost("/config", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    string debugMessage = await reader.ReadToEndAsync();
    Console.WriteLine($"[AuthController] Received config update: {debugMessage}");
    return Results.Ok(new { Message = "Config updated on AuthController" });
});

// POST /process endpoint: Processes forwarded login requests coming from the Broker.
app.MapPost("/process", async (HttpContext context) =>
{
    // Read the incoming payload.
    using var reader = new StreamReader(context.Request.Body);
    string payloadRaw = await reader.ReadToEndAsync();
    Console.WriteLine($"[AuthController] Processing forwarded payload: {payloadRaw}");

    // Parse the JSON payload.
    using JsonDocument doc = JsonDocument.Parse(payloadRaw);
    JsonElement root = doc.RootElement;
    if (!root.TryGetProperty("action", out JsonElement actionElem))
    {
        return Results.BadRequest(new { message = "Action not specified." });
    }
    string action = actionElem.GetString();
    if (action != "login")
    {
        return Results.BadRequest(new { message = "Unsupported action." });
    }

    // Extract credentials.
    if (!root.TryGetProperty("username", out JsonElement userElem) ||
        !root.TryGetProperty("password", out JsonElement passElem))
    {
        return Results.BadRequest(new { message = "Username or password missing." });
    }
    string username = userElem.GetString();
    string password = passElem.GetString();

    // 1. Query the Broker for the current status of all nodes.
    var httpClient = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
    HttpResponseMessage statusResponse = await httpClient.GetAsync($"{brokerUrl}/api/nodes");
    if (!statusResponse.IsSuccessStatusCode)
    {
        return Results.StatusCode((int)statusResponse.StatusCode, new { message = "Failed to retrieve node status from broker." });
    }
    string statusContent = await statusResponse.Content.ReadAsStringAsync();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    List<NodeStatus> allNodes = JsonSerializer.Deserialize<List<NodeStatus>>(statusContent, options);

    // 2. Filter for Users DB nodes (assume names start with "UsersDb").
    var usersDbNodes = allNodes.Where(n => n.Name.StartsWith("UsersDb") && n.IsOnline).ToList();
    if (usersDbNodes.Count == 0)
    {
        return Results.BadRequest(new { message = "No Users DB nodes are currently online." });
    }
    // Select the Users DB node with the lowest latency.
    NodeStatus chosenNode = usersDbNodes.OrderBy(n => n.Latency).First();

    // 3. Construct payload for the DB node to process the login.
    var dbPayload = new
    {
        action = "login",
        username = username,
        password = password
    };
    var dbContent = new StringContent(JsonSerializer.Serialize(dbPayload), Encoding.UTF8, "application/json");

    // 4. Directly contact the chosen DB node's /query endpoint.
    HttpResponseMessage dbResponse = await httpClient.PostAsync($"{chosenNode.Url}/query", dbContent);
    if (!dbResponse.IsSuccessStatusCode)
    {
        string errorMsg = await dbResponse.Content.ReadAsStringAsync();
        return Results.StatusCode((int)dbResponse.StatusCode, new { message = errorMsg });
    }
    string dbResultRaw = await dbResponse.Content.ReadAsStringAsync();
    User user = JsonSerializer.Deserialize<User>(dbResultRaw, options);
    if (user == null)
    {
        return Results.Unauthorized(new { message = "Invalid credentials." });
    }

    // 5. Generate JWT token for the authenticated user.
    string token = GenerateJwtToken(user, secretKey);
    return Results.Ok(new { token = token });
});

app.Run();

// Helper method to generate a JWT token.
string GenerateJwtToken(User user, string secretKey)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var claims = new List<Claim>
    {
        new Claim("user_id", user.UserId.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.Role)
    };
    var token = new JwtSecurityToken(
        issuer: null,
        audience: null,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds
    );
    return new JwtSecurityTokenHandler().WriteToken(token);
}

// Record representing a node status returned by the Broker.
public record NodeStatus
{
    public string Name { get; init; }
    public string Url { get; init; }
    public bool IsOnline { get; init; }
    public bool IsActivated { get; init; }
    public int Latency { get; init; } // in milliseconds
}

// Record representing the User object received from the Users DB node.
public record User
{
    public int UserId { get; init; }
    public string Username { get; init; }
    public string Role { get; init; }
}
