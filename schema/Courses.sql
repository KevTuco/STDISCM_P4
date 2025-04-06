-- Create the Courses table
CREATE TABLE Courses (
    course_id INTEGER PRIMARY KEY AUTOINCREMENT,
    course_name VARCHAR(100) NOT NULL,
    description TEXT,
    max_slots INT NOT NULL,
    teacher_id INT,  -- In a distributed system, this might be a logical reference to Users.user_id
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create the Enrollment table to track student course enrollments.
CREATE TABLE Enrollment (
    enrollment_id INTEGER PRIMARY KEY AUTOINCREMENT,
    student_id INT NOT NULL,  -- References the student in the Users table (user_id)
    course_id INT NOT NULL,   -- References the course in the Courses table
    enrollment_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (student_id, course_id)
    -- Note: In a distributed scenario, you may not enforce foreign keys across databases,
    -- but these comments indicate the logical relationships.
);
