-- Create the Grades table
CREATE TABLE Grades (
    grade_id INTEGER PRIMARY KEY AUTOINCREMENT,
    student_id INT NOT NULL,  -- Should match Users.user_id for students
    course_id INT NOT NULL,   -- Should match Courses.course_id for courses
    grade DECIMAL(2,1) CHECK (grade >= 0.0 AND grade <= 4.0),         -- Grade format (e.g., A, B+, etc.)
    grade_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (student_id, course_id)
    -- Note: Logical relationships exist to the Users and Courses databases.
);
