using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Determine the full path to the Pages folder.
var pagesFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Pages");

// Use default files (which will look for index.html in the pages folder)
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(pagesFolderPath),
    DefaultFileNames = new List<string> { "index.html" }
});

// Serve static files from the Pages folder.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(pagesFolderPath),
    RequestPath = "" // Empty so that files are served at the root (e.g., http://localhost:5001/page1.html)
});

// Example endpoints for status and config if needed.
app.MapGet("/status", () => Results.Ok(new { Name = "View Node", Status = "Online" }));
app.MapPost("/config", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var debugMessage = await reader.ReadToEndAsync();
    Console.WriteLine($"[View Node] Received config update: {debugMessage}");
    return Results.Ok(new { Message = "Config updated on View Node" });
});

app.Run();
