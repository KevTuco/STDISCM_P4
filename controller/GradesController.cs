// Controllers/GradesController.cs
using EnrollmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/grades")]
    [Authorize]
    public class GradesController : ControllerBase
    {
        // Sample in-memory grades list.
        private static readonly List<Grade> _grades = new List<Grade>
        {
            new Grade { GradeId = 1, StudentId = 1, CourseId = 1, GradeValue = 3.8 },
            new Grade { GradeId = 2, StudentId = 1, CourseId = 2, GradeValue = 3.5 }
        };

        [HttpGet]
        public IActionResult GetGrades()
        {
            int studentId = int.Parse(User.FindFirst("user_id")?.Value ?? "0");
            var studentGrades = _grades.Where(g => g.StudentId == studentId).ToList();
            return Ok(studentGrades);
        }

        [HttpPost("upload")]
        [Authorize(Roles = "teacher")]
        public IActionResult UploadGrade([FromBody] GradeUploadRequest request)
        {
            if (request.GradeValue < 0.0 || request.GradeValue > 4.0)
            {
                return BadRequest(new { message = "Grade must be between 0.0 and 4.0" });
            }

            // In a real application, persist the grade record in the database.
            int newGradeId = _grades.Any() ? _grades.Max(g => g.GradeId) + 1 : 1;
            var newGrade = new Grade
            {
                GradeId = newGradeId,
                StudentId = request.StudentId,
                CourseId = request.CourseId,
                GradeValue = request.GradeValue
            };

            _grades.Add(newGrade);
            return Ok(new { message = "Grade uploaded successfully" });
        }
    }
}
