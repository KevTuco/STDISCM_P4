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
            var grades = new List<object>();

            using var gradeConn = new SqliteConnection("Data Source=./schema/Grades.db");
            gradeConn.Open();

            var gradeCmd = gradeConn.CreateCommand();
            gradeCmd.CommandText = @"
                SELECT course_id, grade
                FROM Grades
                WHERE student_id = $studentId
                ORDER BY grade_id DESC";
            gradeCmd.Parameters.AddWithValue("$studentId", studentId);

            using var reader = gradeCmd.ExecuteReader();
            while (reader.Read())
            {
                int courseId = reader.GetInt32(0);
                double grade = reader.GetDouble(1);

                string courseName = "N/A";
                using var courseConn = new SqliteConnection("Data Source=./schema/Courses.db");
                courseConn.Open();
                var courseCmd = courseConn.CreateCommand();
                courseCmd.CommandText = "SELECT course_name FROM Courses WHERE course_id = $courseId";
                courseCmd.Parameters.AddWithValue("$courseId", courseId);
                using var courseReader = courseCmd.ExecuteReader();
                if (courseReader.Read())
                    courseName = courseReader.GetString(0);

                grades.Add(new
                {
                    CourseId = courseId,
                    CourseName = courseName,
                    GradeValue = grade
                });
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

            // 1. Save or update grade in Grades.db
            using var gradeConn = new SqliteConnection("Data Source=./schema/Grades.db");
            gradeConn.Open();

            var checkCmd = gradeConn.CreateCommand();
            checkCmd.CommandText = @"
                SELECT COUNT(*) FROM Grades 
                WHERE student_id = $studentId AND course_id = $courseId";
            checkCmd.Parameters.AddWithValue("$studentId", request.StudentId);
            checkCmd.Parameters.AddWithValue("$courseId", request.CourseId);
            var exists = (long)checkCmd.ExecuteScalar();

            if (exists > 0)
            {
                // Update existing grade
                var updateCmd = gradeConn.CreateCommand();
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
                var insertCmd = gradeConn.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Grades (student_id, course_id, grade)
                    VALUES ($studentId, $courseId, $grade)";
                insertCmd.Parameters.AddWithValue("$studentId", request.StudentId);
                insertCmd.Parameters.AddWithValue("$courseId", request.CourseId);
                insertCmd.Parameters.AddWithValue("$grade", request.GradeValue);
                insertCmd.ExecuteNonQuery();
            }

            // 2. Remove from Enrollment table (Courses.db)
            using var enrollConn = new SqliteConnection("Data Source=./schema/Courses.db");
            enrollConn.Open();

            var deleteCmd = enrollConn.CreateCommand();
            deleteCmd.CommandText = @"
                DELETE FROM Enrollment
                WHERE student_id = $studentId AND course_id = $courseId";
            deleteCmd.Parameters.AddWithValue("$studentId", request.StudentId);
            deleteCmd.Parameters.AddWithValue("$courseId", request.CourseId);
            deleteCmd.ExecuteNonQuery();

            return Ok(new
            {
                message = request.GradeValue < 1.0
                    ? "Grade uploaded. Student has failed and must retake the course."
                    : "Grade uploaded successfully!"
            });
        }


    }
}
