// Controllers/CoursesController.cs
using EnrollmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/courses")]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        // Sample in-memory course data.
        private readonly List<Course> _courses = new List<Course>
        {
            new Course { CourseId = 1, CourseName = "Math 101", TeacherId = 2 },
            new Course { CourseId = 2, CourseName = "Physics 101", TeacherId = 2 },
            new Course { CourseId = 3, CourseName = "Chemistry 101", TeacherId = 2 }
        };

        // In-memory enrollment list (for demonstration only)
        private static readonly List<(int StudentId, int CourseId)> _enrollments = new List<(int, int)>();

        [HttpGet]
        public IActionResult GetCourses()
        {
            return Ok(_courses);
        }

        [HttpPost("enroll")]
        public IActionResult Enroll([FromBody] EnrollmentRequest request)
        {
            // Get the current user's ID from the JWT claims
            int studentId = int.Parse(User.FindFirst("user_id")?.Value ?? "0");

            // Check for duplicate enrollment.
            if (_enrollments.Any(e => e.StudentId == studentId && e.CourseId == request.CourseId))
            {
                return BadRequest(new { message = "Already enrolled in this course" });
            }

            // In a real system, insert into your database.
            _enrollments.Add((studentId, request.CourseId));

            return Ok(new { message = "Enrolled successfully" });
        }
    }
}
