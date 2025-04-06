-- ========================================
-- Populate the User Database with 15 users:
-- First 10 users are students and the next 5 are teachers.
-- ========================================

INSERT INTO Users (username, password_hash, email, role)
VALUES
  ('student1', 'hashed_password1', 'student1@example.com', 'student'),
  ('student2', 'hashed_password2', 'student2@example.com', 'student'),
  ('student3', 'hashed_password3', 'student3@example.com', 'student'),
  ('student4', 'hashed_password4', 'student4@example.com', 'student'),
  ('student5', 'hashed_password5', 'student5@example.com', 'student'),
  ('student6', 'hashed_password6', 'student6@example.com', 'student'),
  ('student7', 'hashed_password7', 'student7@example.com', 'student'),
  ('student8', 'hashed_password8', 'student8@example.com', 'student'),
  ('student9', 'hashed_password9', 'student9@example.com', 'student'),
  ('student10', 'hashed_password10', 'student10@example.com', 'student'),
  ('teacher1', 'hashed_password11', 'teacher1@example.com', 'teacher'),
  ('teacher2', 'hashed_password12', 'teacher2@example.com', 'teacher'),
  ('teacher3', 'hashed_password13', 'teacher3@example.com', 'teacher'),
  ('teacher4', 'hashed_password14', 'teacher4@example.com', 'teacher'),
  ('teacher5', 'hashed_password15', 'teacher5@example.com', 'teacher');

-- ========================================
-- Populate the Course Database with 15 courses.
-- We assign teacher_id values between 11 and 15 (as inserted above).
-- ========================================

INSERT INTO Courses (course_name, description, max_slots, teacher_id)
VALUES
  ('Course 1', 'Description for Course 1', 30, 11),
  ('Course 2', 'Description for Course 2', 25, 12),
  ('Course 3', 'Description for Course 3', 30, 13),
  ('Course 4', 'Description for Course 4', 25, 14),
  ('Course 5', 'Description for Course 5', 30, 15),
  ('Course 6', 'Description for Course 6', 30, 11),
  ('Course 7', 'Description for Course 7', 25, 12),
  ('Course 8', 'Description for Course 8', 30, 13),
  ('Course 9', 'Description for Course 9', 25, 14),
  ('Course 10', 'Description for Course 10', 30, 15),
  ('Course 11', 'Description for Course 11', 30, 11),
  ('Course 12', 'Description for Course 12', 25, 12),
  ('Course 13', 'Description for Course 13', 30, 13),
  ('Course 14', 'Description for Course 14', 25, 14),
  ('Course 15', 'Description for Course 15', 30, 15);

-- ========================================
-- Populate the Enrollment Table with 15 enrollments.
-- Only students (user_id 1 to 10) can enroll.
-- ========================================

INSERT INTO Enrollment (student_id, course_id)
VALUES
  (1, 1),   -- student1 enrolls in Course 1
  (2, 1),   -- student2 enrolls in Course 1
  (3, 2),   -- student3 enrolls in Course 2
  (4, 3),   -- student4 enrolls in Course 3
  (5, 4),   -- student5 enrolls in Course 4
  (6, 5),   -- student6 enrolls in Course 5
  (7, 6),   -- student7 enrolls in Course 6
  (8, 7),   -- student8 enrolls in Course 7
  (9, 8),   -- student9 enrolls in Course 8
  (10, 9),  -- student10 enrolls in Course 9
  (1, 10),  -- student1 enrolls in Course 10
  (2, 11),  -- student2 enrolls in Course 11
  (3, 12),  -- student3 enrolls in Course 12
  (4, 13),  -- student4 enrolls in Course 13
  (5, 14);  -- student5 enrolls in Course 14

INSERT INTO Grades (student_id, course_id, grade)
VALUES
  (1, 1, 3.8),   -- student1 in Course 1
  (2, 1, 3.5),   -- student2 in Course 1
  (3, 2, 3.0),   -- student3 in Course 2
  (4, 3, 2.8),   -- student4 in Course 3
  (5, 4, 3.2),   -- student5 in Course 4
  (6, 5, 3.9),   -- student6 in Course 5
  (7, 6, 2.5),   -- student7 in Course 6
  (8, 7, 3.0),   -- student8 in Course 7
  (9, 8, 3.7),   -- student9 in Course 8
  (10, 9, 2.9),  -- student10 in Course 9
  (1, 10, 3.4),  -- student1 in Course 10
  (2, 11, 3.1),  -- student2 in Course 11
  (3, 12, 3.6),  -- student3 in Course 12
  (4, 13, 2.7),  -- student4 in Course 13
  (5, 14, 3.3);  -- student5 in Course 14
