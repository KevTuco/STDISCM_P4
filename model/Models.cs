// Models.cs
namespace EnrollmentSystem.Models
{
    public class LoginRequest 
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class User 
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; } // "student" or "teacher"
    }

    public class Course
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string Description { get; set; } = "N/A";
        public int MaxSlots { get; set; }
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = "Unknown";
    }

    public class EnrollmentRequest 
    {
        public int CourseId { get; set; }
    }

    public class Grade 
    {
        public int GradeId { get; set; }
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public double GradeValue { get; set; } // should be between 0.0 and 4.0
    }

    public class GradeUploadRequest 
    {
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public double GradeValue { get; set; }
    }
}
