using EnrollmentSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace EnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/grades")]
    [Authorize]
    public class GradesController : ControllerBase
    {
        private const string DbPath = "./schema/Grades.db";
     
        [HttpGet]
        public IActionResult GetGrades()
        {
            int studentId = int.Parse(User.FindFirst("user_id")?.Value ?? "0");
            var grades = new List<Grade>();

            using var connection = new SqliteConnection("Data Source=./schema/Grades.db");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT grade_id, student_id, course_id, grade FROM Grades WHERE student_id = $studentId";
            command.Parameters.AddWithValue("$studentId", studentId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // Use IsDBNull check before accessing columns
                var grade = new Grade
                {
                    GradeId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    StudentId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    CourseId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    GradeValue = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3)
                };
                grades.Add(grade);
            }

            return Ok(grades);
        }

        [HttpPost("upload")]
        [Authorize(Roles = "teacher")]
        public IActionResult UploadGrade([FromBody] GradeUploadRequest request)
        {
            if (request.GradeValue < 0.0 || request.GradeValue > 4.0)
            {
                return BadRequest(new { message = "Grade must be between 0.0 and 4.0" });
            }

            using var connection = new SqliteConnection("Data Source=./schema/Grades.db");
            connection.Open();

            // Optional: Check if grade already exists for this student & course
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT COUNT(*) FROM Grades 
                WHERE student_id = $studentId AND course_id = $courseId";
            checkCmd.Parameters.AddWithValue("$studentId", request.StudentId);
            checkCmd.Parameters.AddWithValue("$courseId", request.CourseId);
            var exists = (long)checkCmd.ExecuteScalar();

            if (exists > 0)
            {
                // Overwrite grade if already exists
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE Grades
                    SET grade = $grade
                    WHERE student_id = $studentId AND course_id = $courseId";
                updateCmd.Parameters.AddWithValue("$grade", request.GradeValue);
                updateCmd.Parameters.AddWithValue("$studentId", request.StudentId);
                updateCmd.Parameters.AddWithValue("$courseId", request.CourseId);
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                // Insert new grade
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Grades (student_id, course_id, grade)
                    VALUES ($studentId, $courseId, $grade)";
                insertCmd.Parameters.AddWithValue("$studentId", request.StudentId);
                insertCmd.Parameters.AddWithValue("$courseId", request.CourseId);
                insertCmd.Parameters.AddWithValue("$grade", request.GradeValue);
                insertCmd.ExecuteNonQuery();
            }

            return Ok(new { message = "Grade uploaded successfully!" });
        }

    }
}
