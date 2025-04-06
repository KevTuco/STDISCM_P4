using EnrollmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace EnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/courses")]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private const string DbPath = "./schema/Courses.db";

        [HttpGet]
        public IActionResult GetCourses()
        {
            var courses = new List<Course>();

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT course_id, course_name, teacher_id FROM Courses";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var course = new Course
                {
                    CourseId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    CourseName = reader.IsDBNull(1) ? "Untitled" : reader.GetString(1),
                    TeacherId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                };
                courses.Add(course);
            }

            return Ok(courses);
        }


        [HttpPost("enroll")]
        public IActionResult Enroll([FromBody] EnrollmentRequest request)
        {
            // Dummy logic since there's no enrollment table
            return Ok(new { message = "Enrolled successfully!" });
        }
    }
}
