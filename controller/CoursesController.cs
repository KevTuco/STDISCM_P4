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

            using var courseConn = new SqliteConnection("Data Source=./schema/Courses.db");
            courseConn.Open();

            var courseCmd = courseConn.CreateCommand();
            courseCmd.CommandText = @"
                SELECT course_id, course_name, description, max_slots, teacher_id FROM Courses";
            
            using var reader = courseCmd.ExecuteReader();
            while (reader.Read())
            {
                var teacherId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

                var course = new Course
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                    MaxSlots = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    TeacherId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4), // ✅ required
                    TeacherName = "Unknown"
                };



                if (course.TeacherId != 0)
                {
                    using var userConn = new SqliteConnection("Data Source=./schema/Users.db");
                    userConn.Open();
                    var userCmd = userConn.CreateCommand();
                    userCmd.CommandText = "SELECT username FROM Users WHERE user_id = $id";
                    userCmd.Parameters.AddWithValue("$id", course.TeacherId);
                    using var userReader = userCmd.ExecuteReader();
                    if (userReader.Read())
                    {
                        course.TeacherName = userReader.GetString(0);
                    }
                }

                courses.Add(course);

            }

            return Ok(courses);
        }


        [HttpPost("enroll")]
        [Authorize(Roles = "student")]
        public IActionResult Enroll([FromBody] EnrollmentRequest request)
        {
            int studentId = int.Parse(User.FindFirst("user_id")?.Value ?? "0");

            using var courseConnection = new SqliteConnection($"Data Source={DbPath}");
            courseConnection.Open();

            using var gradeConnection = new SqliteConnection("Data Source=./schema/Grades.db");
            gradeConnection.Open();

            // Already enrolled check
            var checkCmd = courseConnection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT COUNT(*) FROM Enrollment WHERE student_id = $studentId AND course_id = $courseId";
            checkCmd.Parameters.AddWithValue("$studentId", studentId);
            checkCmd.Parameters.AddWithValue("$courseId", request.CourseId);
            if ((long)checkCmd.ExecuteScalar() > 0)
            {
                return BadRequest(new { message = "Already enrolled in this course." });
            }

            // Passed check
            var gradeCmd = gradeConnection.CreateCommand();
            gradeCmd.CommandText = @"
                SELECT grade FROM Grades
                WHERE student_id = $studentId AND course_id = $courseId
                ORDER BY grade_id DESC LIMIT 1";
            gradeCmd.Parameters.AddWithValue("$studentId", studentId);
            gradeCmd.Parameters.AddWithValue("$courseId", request.CourseId);

            using var reader = gradeCmd.ExecuteReader();
            if (reader.Read())
            {
                double grade = reader.IsDBNull(0) ? 0.0 : reader.GetDouble(0);
                if (grade > 1.0)
                {
                    return BadRequest(new { message = "Subject already passed and cannot be retaken." });
                }
            }

            // Enroll student
            var insertCmd = courseConnection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Enrollment (student_id, course_id)
                VALUES ($studentId, $courseId)";
            insertCmd.Parameters.AddWithValue("$studentId", studentId);
            insertCmd.Parameters.AddWithValue("$courseId", request.CourseId);
            insertCmd.ExecuteNonQuery();

            // Deduct a slot from the course
            var updateSlotsCmd = courseConnection.CreateCommand();
            updateSlotsCmd.CommandText = @"
                UPDATE Courses
                SET max_slots = max_slots - 1
                WHERE course_id = $courseId AND max_slots > 0";
            updateSlotsCmd.Parameters.AddWithValue("$courseId", request.CourseId);
            updateSlotsCmd.ExecuteNonQuery();


            return Ok(new { message = "Enrolled successfully!" });
        }

        [HttpGet("enrolled")]
        [Authorize(Roles = "student")]
        public IActionResult GetEnrolledCourses()
        {
            int studentId = int.Parse(User.FindFirst("user_id")?.Value ?? "0");
            var enrolledCourses = new List<Course>();

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.course_id, c.course_name, c.description, c.max_slots, c.teacher_id
                FROM Courses c
                JOIN Enrollment e ON c.course_id = e.course_id
                WHERE e.student_id = $studentId";
            command.Parameters.AddWithValue("$studentId", studentId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var course = new Course
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                    MaxSlots = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    TeacherName = "Unknown"
                };

                int teacherId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

                if (teacherId != 0)
                {
                    using var userConn = new SqliteConnection("Data Source=./schema/Users.db");
                    userConn.Open();

                    var userCmd = userConn.CreateCommand();
                    userCmd.CommandText = "SELECT username FROM Users WHERE user_id = $id";
                    userCmd.Parameters.AddWithValue("$id", teacherId);

                    using var userReader = userCmd.ExecuteReader();
                    if (userReader.Read())
                    {
                        course.TeacherName = userReader.GetString(0);
                    }
                }

                enrolledCourses.Add(course);
            }

            return Ok(enrolledCourses);
        }

        [HttpGet("{courseId}/enrollments")]
        [Authorize(Roles = "teacher")]
        public IActionResult GetEnrolledStudents(int courseId)
        {
            var studentIds = new List<int>();

            using (var courseConn = new SqliteConnection("Data Source=./schema/Courses.db"))
            {
                courseConn.Open();
                var cmd = courseConn.CreateCommand();
                cmd.CommandText = "SELECT student_id FROM Enrollment WHERE course_id = $courseId";
                cmd.Parameters.AddWithValue("$courseId", courseId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    studentIds.Add(reader.GetInt32(0));
                }
            }

            var students = new List<object>();
            using (var userConn = new SqliteConnection("Data Source=./schema/Users.db"))
            {
                userConn.Open();
                foreach (int id in studentIds)
                {
                    var userCmd = userConn.CreateCommand();
                    userCmd.CommandText = "SELECT user_id, username FROM Users WHERE user_id = $id";
                    userCmd.Parameters.AddWithValue("$id", id);
                    using var userReader = userCmd.ExecuteReader();
                    if (userReader.Read())
                    {
                        students.Add(new
                        {
                            userId = userReader.GetInt32(0),
                            username = userReader.GetString(1)
                        });
                    }
                }
            }

            return Ok(students);
        }
    }
}