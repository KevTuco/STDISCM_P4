using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

string brokerUrl = "http://localhost:5000";

// GET /status: Reports that the GradesController is online.
app.MapGet("/status", () =>
{
    return Results.Ok(new { Name = "GradesController", Status = "Online" });
});

// POST /config: Receives configuration updates from the Broker.
app.MapPost("/config", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    string debugMessage = await reader.ReadToEndAsync();
    Console.WriteLine($"[GradesController] Received config update: {debugMessage}");
    return Results.Ok(new { Message = "Config updated on GradesController" });
});

// POST /process: Processes forwarded requests.
// Supports actions: "getGrades" and "uploadGrade".
app.MapPost("/process", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    string payloadRaw = await reader.ReadToEndAsync();
    Console.WriteLine($"[GradesController] Processing forwarded payload: {payloadRaw}");

    using JsonDocument doc = JsonDocument.Parse(payloadRaw);
    JsonElement root = doc.RootElement;

    if (!root.TryGetProperty("action", out JsonElement actionElem))
        return Results.BadRequest(new { message = "Action not specified." });

    string action = actionElem.GetString();

    int studentId = 0;
    if (context.User.Identity is { IsAuthenticated: true })
    {
        int.TryParse(context.User.FindFirst("user_id")?.Value, out studentId);
    }

    var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    HttpResponseMessage statusResponse = await httpClient.GetAsync($"{brokerUrl}/api/nodes");
    if (!statusResponse.IsSuccessStatusCode)
        return Results.StatusCode(500, new { message = "Failed to retrieve node status from broker." });
    string statusContent = await statusResponse.Content.ReadAsStringAsync();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    List<NodeStatus> allNodes = JsonSerializer.Deserialize<List<NodeStatus>>(statusContent, options);

    if (action == "getGrades")
    {
        // Use Grades DB node to get the grades.
        var gradesNodes = allNodes.Where(n => n.Name.StartsWith("GradesDb") && n.IsOnline).ToList();
        if (!gradesNodes.Any())
            return Results.BadRequest(new { message = "No Grades DB nodes are online." });
        NodeStatus chosenGradesDb = gradesNodes.OrderBy(n => n.Latency).First();
        var payloadObj = new { action = "getGrades", studentId };
        var payloadStr = JsonSerializer.Serialize(payloadObj);
        var contentPayload = new StringContent(payloadStr, Encoding.UTF8, "application/json");
        await Task.Delay(chosenGradesDb.Latency);
        HttpResponseMessage dbResponse = await httpClient.PostAsync($"{chosenGradesDb.Url}/query", contentPayload);
        if (!dbResponse.IsSuccessStatusCode)
        {
            string errorMsg = await dbResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)dbResponse.StatusCode, new { message = errorMsg });
        }
        string dbResultRaw = await dbResponse.Content.ReadAsStringAsync();
        var grades = JsonSerializer.Deserialize<List<GradeRecord>>(dbResultRaw, options);

        // For each grade, fetch the course name via Courses DB node.
        var coursesNodes = allNodes.Where(n => n.Name.StartsWith("CoursesDb") && n.IsOnline).ToList();
        foreach (var grade in grades)
        {
            if (grade.CourseId != 0 && coursesNodes.Any())
            {
                NodeStatus chosenCoursesDb = coursesNodes.OrderBy(n => n.Latency).First();
                var coursePayload = new { action = "getCourse", courseId = grade.CourseId };
                var courseContent = new StringContent(JsonSerializer.Serialize(coursePayload), Encoding.UTF8, "application/json");
                await Task.Delay(chosenCoursesDb.Latency);
                HttpResponseMessage courseResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", courseContent);
                if (courseResponse.IsSuccessStatusCode)
                {
                    string courseResultRaw = await courseResponse.Content.ReadAsStringAsync();
                    var course = JsonSerializer.Deserialize<Course>(courseResultRaw, options);
                    if (course != null)
                        grade.CourseName = course.CourseName;
                }
            }
        }
        return Results.Ok(grades);
    }
    else if (action == "uploadGrade")
    {
        if (!root.TryGetProperty("studentId", out JsonElement studentIdElem) ||
            !root.TryGetProperty("courseId", out JsonElement courseIdElem) ||
            !root.TryGetProperty("gradeValue", out JsonElement gradeValueElem))
        {
            return Results.BadRequest(new { message = "Missing parameters for uploading grade." });
        }
        int targetStudentId = studentIdElem.GetInt32();
        int courseId = courseIdElem.GetInt32();
        double gradeValue = gradeValueElem.GetDouble();

        if (gradeValue < 0.0 || gradeValue > 4.0)
            return Results.BadRequest(new { message = "Grade must be between 0.0 and 4.0" });

        // Use Grades DB node for updating/inserting the grade.
        var gradesNodes = allNodes.Where(n => n.Name.StartsWith("GradesDb") && n.IsOnline).ToList();
        if (!gradesNodes.Any())
            return Results.BadRequest(new { message = "No Grades DB nodes are online." });
        NodeStatus chosenGradesDb = gradesNodes.OrderBy(n => n.Latency).First();
        var gradePayload = new { action = "uploadGrade", studentId = targetStudentId, courseId, gradeValue };
        var gradeContent = new StringContent(JsonSerializer.Serialize(gradePayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenGradesDb.Latency);
        HttpResponseMessage gradeResponse = await httpClient.PostAsync($"{chosenGradesDb.Url}/query", gradeContent);
        if (!gradeResponse.IsSuccessStatusCode)
        {
            string errorMsg = await gradeResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)gradeResponse.StatusCode, new { message = errorMsg });
        }

        // Use Courses DB node to remove the enrollment record.
        var coursesNodes = allNodes.Where(n => n.Name.StartsWith("CoursesDb") && n.IsOnline).ToList();
        if (!coursesNodes.Any())
            return Results.BadRequest(new { message = "No Courses DB nodes are online for enrollment update." });
        NodeStatus chosenCoursesDb = coursesNodes.OrderBy(n => n.Latency).First();
        var removePayload = new { action = "removeEnrollment", studentId = targetStudentId, courseId };
        var removeContent = new StringContent(JsonSerializer.Serialize(removePayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage removeResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", removeContent);
        if (!removeResponse.IsSuccessStatusCode)
        {
            string errorMsg = await removeResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)removeResponse.StatusCode, new { message = errorMsg });
        }
        string uploadMsg = gradeValue < 1.0
            ? "Grade uploaded. Student has failed and must retake the course."
            : "Grade uploaded successfully!";
        return Results.Ok(new { message = uploadMsg });
    }
    else
    {
        return Results.BadRequest(new { message = "Unsupported action." });
    }
});

app.Run();

// Record definitions.
record NodeStatus(string Name, string Url, bool IsOnline, bool IsActivated, int Latency);
record GradeRecord
{
    public int CourseId { get; init; }
    public double GradeValue { get; init; }
    public string CourseName { get; set; } = "N/A";
}
record Course
{
    public int CourseId { get; init; }
    public string CourseName { get; init; }
}
