using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

public class DashboardController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly string brokerUrl;

    public DashboardController(IHttpClientFactory clientFactory, IConfiguration config)
    {
        _clientFactory = clientFactory;
        // Read the Broker BaseUrl from configuration.
        brokerUrl = config.GetSection("Broker")["BaseUrl"] + "/api/nodes";
    }

    // Show all nodes.
    public async Task<IActionResult> Index()
    {
        var client = _clientFactory.CreateClient();
        var nodes = await client.GetFromJsonAsync<IEnumerable<NodeStatus>>(brokerUrl);
        return View(nodes);
    }
    
    [HttpPost]
    public async Task<IActionResult> Update(string nodeName, bool isActivated, int latency)
    {
        var client = _clientFactory.CreateClient();
        var updateUrl = $"http://localhost:5000/api/node/update/{nodeName}";
        var update = new NodeUpdate { IsActivated = isActivated, Latency = latency };
        var response = await client.PostAsJsonAsync(updateUrl, update);
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = await response.Content.ReadAsStringAsync();
        }
        return RedirectToAction("Index");
    }
}

public record NodeStatus(string Name, string Url, bool IsOnline, bool IsActivated, int Latency);
public record NodeUpdate { public bool IsActivated { get; init; } public int Latency { get; init; } }
