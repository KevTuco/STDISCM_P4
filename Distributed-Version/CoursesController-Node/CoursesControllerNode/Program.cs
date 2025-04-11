using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

string brokerUrl = "http://localhost:5000";

// GET /status: Reports the CoursesController is online.
app.MapGet("/status", () =>
{
    return Results.Ok(new { Name = "CoursesController", Status = "Online" });
});

// POST /config: Receives configuration updates from the Broker.
app.MapPost("/config", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    string debugMessage = await reader.ReadToEndAsync();
    Console.WriteLine($"[CoursesController] Received config update: {debugMessage}");
    return Results.Ok(new { Message = "Config updated on CoursesController" });
});

// POST /process: Processes forwarded requests.
// Supports actions: "getCourses", "enroll", "getEnrolled", "getEnrollments".
app.MapPost("/process", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    string payloadRaw = await reader.ReadToEndAsync();
    Console.WriteLine($"[CoursesController] Processing forwarded payload: {payloadRaw}");

    using JsonDocument doc = JsonDocument.Parse(payloadRaw);
    JsonElement root = doc.RootElement;

    if (!root.TryGetProperty("action", out JsonElement actionElem))
        return Results.BadRequest(new { message = "Action not specified." });

    string action = actionElem.GetString();

    // Retrieve studentId from the JWT token if available.
    int studentId = 0;
    if (context.User.Identity is { IsAuthenticated: true })
    {
        int.TryParse(context.User.FindFirst("user_id")?.Value, out studentId);
    }

    // Obtain the list of node statuses from the Broker.
    var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    HttpResponseMessage statusResponse = await httpClient.GetAsync($"{brokerUrl}/api/nodes");
    if (!statusResponse.IsSuccessStatusCode)
        return Results.StatusCode(500, new { message = "Failed to retrieve node status from broker." });

    string statusContent = await statusResponse.Content.ReadAsStringAsync();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    List<NodeStatus> allNodes = JsonSerializer.Deserialize<List<NodeStatus>>(statusContent, options);

    if (action == "getCourses")
    {
        // Select best Courses DB node.
        var coursesNodes = allNodes.Where(n => n.Name.StartsWith("CoursesDb") && n.IsOnline).ToList();
        if (!coursesNodes.Any())
            return Results.BadRequest(new { message = "No Courses DB nodes are online." });

        NodeStatus chosenCoursesDb = coursesNodes.OrderBy(n => n.Latency).First();
        var dbPayload = new { action = "getCourses" };
        var dbContent = new StringContent(JsonSerializer.Serialize(dbPayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage dbResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", dbContent);
        if (!dbResponse.IsSuccessStatusCode)
        {
            string errorMsg = await dbResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)dbResponse.StatusCode, new { message = errorMsg });
        }
        string dbResultRaw = await dbResponse.Content.ReadAsStringAsync();
        var courses = JsonSerializer.Deserialize<List<Course>>(dbResultRaw, options);

        // For each course, if TeacherId != 0, query Users DB node for the teacher's username.
        var usersNodes = allNodes.Where(n => n.Name.StartsWith("UsersDb") && n.IsOnline).ToList();
        foreach (var course in courses)
        {
            if (course.TeacherId != 0 && usersNodes.Any())
            {
                NodeStatus chosenUsersDb = usersNodes.OrderBy(n => n.Latency).First();
                var userPayload = new { action = "getUser", userId = course.TeacherId };
                var userContent = new StringContent(JsonSerializer.Serialize(userPayload), Encoding.UTF8, "application/json");
                await Task.Delay(chosenUsersDb.Latency);
                HttpResponseMessage userResponse = await httpClient.PostAsync($"{chosenUsersDb.Url}/query", userContent);
                if (userResponse.IsSuccessStatusCode)
                {
                    string userResultRaw = await userResponse.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(userResultRaw, options);
                    if (user != null)
                        course.TeacherName = user.Username;
                }
            }
        }
        return Results.Ok(courses);
    }
    else if (action == "enroll")
    {
        if (!root.TryGetProperty("courseId", out JsonElement courseIdElem))
            return Results.BadRequest(new { message = "courseId is required for enrollment." });
        int courseId = courseIdElem.GetInt32();

        // Use Courses DB to verify enrollment status.
        var coursesNodes = allNodes.Where(n => n.Name.StartsWith("CoursesDb") && n.IsOnline).ToList();
        if (!coursesNodes.Any())
            return Results.BadRequest(new { message = "No Courses DB nodes are online." });

        NodeStatus chosenCoursesDb = coursesNodes.OrderBy(n => n.Latency).First();
        var checkPayload = new { action = "checkEnrollment", studentId, courseId };
        var checkContent = new StringContent(JsonSerializer.Serialize(checkPayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage checkResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", checkContent);
        if (!checkResponse.IsSuccessStatusCode)
        {
            string errorMsg = await checkResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)checkResponse.StatusCode, new { message = errorMsg });
        }
        string checkResultRaw = await checkResponse.Content.ReadAsStringAsync();
        var enrollmentCheck = JsonSerializer.Deserialize<EnrollmentCheckResult>(checkResultRaw, options);
        if (enrollmentCheck.AlreadyEnrolled)
            return Results.BadRequest(new { message = "Already enrolled in this course." });

        // Use Grades DB to check if the subject was already passed.
        var gradesNodes = allNodes.Where(n => n.Name.StartsWith("GradesDb") && n.IsOnline).ToList();
        if (!gradesNodes.Any())
            return Results.BadRequest(new { message = "No Grades DB nodes are online." });
        NodeStatus chosenGradesDb = gradesNodes.OrderBy(n => n.Latency).First();
        var gradeCheckPayload = new { action = "checkGrade", studentId, courseId };
        var gradeCheckContent = new StringContent(JsonSerializer.Serialize(gradeCheckPayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenGradesDb.Latency);
        HttpResponseMessage gradeCheckResponse = await httpClient.PostAsync($"{chosenGradesDb.Url}/query", gradeCheckContent);
        if (!gradeCheckResponse.IsSuccessStatusCode)
        {
            string errorMsg = await gradeCheckResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)gradeCheckResponse.StatusCode, new { message = errorMsg });
        }
        string gradeCheckResultRaw = await gradeCheckResponse.Content.ReadAsStringAsync();
        var gradeCheckResult = JsonSerializer.Deserialize<GradeCheckResult>(gradeCheckResultRaw, options);
        if (gradeCheckResult.Passed)
            return Results.BadRequest(new { message = "Subject already passed and cannot be retaken." });

        // Enroll the student.
        var enrollPayload = new { action = "enroll", studentId, courseId };
        var enrollContent = new StringContent(JsonSerializer.Serialize(enrollPayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage enrollResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", enrollContent);
        if (!enrollResponse.IsSuccessStatusCode)
        {
            string errorMsg = await enrollResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)enrollResponse.StatusCode, new { message = errorMsg });
        }
        // Update course slots.
        var updateSlotPayload = new { action = "updateSlots", courseId };
        var updateSlotContent = new StringContent(JsonSerializer.Serialize(updateSlotPayload), Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage updateSlotResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", updateSlotContent);
        if (!updateSlotResponse.IsSuccessStatusCode)
        {
            string errorMsg = await updateSlotResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)updateSlotResponse.StatusCode, new { message = errorMsg });
        }
        return Results.Ok(new { message = "Enrolled successfully!" });
    }
    else if (action == "getEnrolled")
    {
        // Retrieve enrolled courses for the student.
        var coursesNodes = allNodes.Where(n => n.Name.StartsWith("CoursesDb") && n.IsOnline).ToList();
        if (!coursesNodes.Any())
            return Results.BadRequest(new { message = "No Courses DB nodes are online." });
        NodeStatus chosenCoursesDb = coursesNodes.OrderBy(n => n.Latency).First();
        var payloadObj = new { action = "getEnrolled", studentId };
        var payloadStr = JsonSerializer.Serialize(payloadObj);
        var contentPayload = new StringContent(payloadStr, Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage dbResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", contentPayload);
        if (!dbResponse.IsSuccessStatusCode)
        {
            string errorMsg = await dbResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)dbResponse.StatusCode, new { message = errorMsg });
        }
        string resultRaw = await dbResponse.Content.ReadAsStringAsync();
        var enrolledCourses = JsonSerializer.Deserialize<List<Course>>(resultRaw, options);

        // For each enrolled course, fetch teacher info from Users DB.
        var usersNodes = allNodes.Where(n => n.Name.StartsWith("UsersDb") && n.IsOnline).ToList();
        foreach (var course in enrolledCourses)
        {
            if (course.TeacherId != 0 && usersNodes.Any())
            {
                NodeStatus chosenUsersDb = usersNodes.OrderBy(n => n.Latency).First();
                var userPayload = new { action = "getUser", userId = course.TeacherId };
                var userContent = new StringContent(JsonSerializer.Serialize(userPayload), Encoding.UTF8, "application/json");
                await Task.Delay(chosenUsersDb.Latency);
                HttpResponseMessage userResponse = await httpClient.PostAsync($"{chosenUsersDb.Url}/query", userContent);
                if (userResponse.IsSuccessStatusCode)
                {
                    string userResultRaw = await userResponse.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(userResultRaw, options);
                    if (user != null)
                        course.TeacherName = user.Username;
                }
            }
        }
        return Results.Ok(enrolledCourses);
    }
    else if (action == "getEnrollments")
    {
        // For teacher: retrieve enrolled students for a specific course.
        if (!root.TryGetProperty("courseId", out JsonElement courseIdElem))
            return Results.BadRequest(new { message = "courseId is required." });
        int courseId = courseIdElem.GetInt32();
        var coursesNodes = allNodes.Where(n => n.Name.StartsWith("CoursesDb") && n.IsOnline).ToList();
        if (!coursesNodes.Any())
            return Results.BadRequest(new { message = "No Courses DB nodes are online." });
        NodeStatus chosenCoursesDb = coursesNodes.OrderBy(n => n.Latency).First();
        var payloadObj = new { action = "getEnrollments", courseId };
        var payloadStr = JsonSerializer.Serialize(payloadObj);
        var contentPayload = new StringContent(payloadStr, Encoding.UTF8, "application/json");
        await Task.Delay(chosenCoursesDb.Latency);
        HttpResponseMessage dbResponse = await httpClient.PostAsync($"{chosenCoursesDb.Url}/query", contentPayload);
        if (!dbResponse.IsSuccessStatusCode)
        {
            string errorMsg = await dbResponse.Content.ReadAsStringAsync();
            return Results.StatusCode((int)dbResponse.StatusCode, new { message = errorMsg });
        }
        string resultRaw = await dbResponse.Content.ReadAsStringAsync();
        // Assume the DB returns a list of student IDs.
        var enrolledStudents = JsonSerializer.Deserialize<List<int>>(resultRaw, options);
        var usersNodes = allNodes.Where(n => n.Name.StartsWith("UsersDb") && n.IsOnline).ToList();
        var students = new List<object>();
        foreach (var sid in enrolledStudents)
        {
            if (usersNodes.Any())
            {
                NodeStatus chosenUsersDb = usersNodes.OrderBy(n => n.Latency).First();
                var userPayload = new { action = "getUser", userId = sid };
                var userContent = new StringContent(JsonSerializer.Serialize(userPayload), Encoding.UTF8, "application/json");
                await Task.Delay(chosenUsersDb.Latency);
                HttpResponseMessage userResponse = await httpClient.PostAsync($"{chosenUsersDb.Url}/query", userContent);
                if (userResponse.IsSuccessStatusCode)
                {
                    string userResultRaw = await userResponse.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(userResultRaw, options);
                    if (user != null)
                        students.Add(new { userId = user.UserId, username = user.Username });
                }
            }
        }
        return Results.Ok(students);
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
