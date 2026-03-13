using LMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            try
            {
                await context.Database.EnsureCreatedAsync();
            }
            catch
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE Courses ADD COLUMN Price REAL NOT NULL DEFAULT 0;
                    ");
                }
                catch
                {
                }
            }

            string[] roles = { "Admin", "Instructor", "Student" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            if (await userManager.FindByEmailAsync("admin@lms.com") == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@lms.com",
                    Email = "admin@lms.com",
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            ApplicationUser? instructor = await userManager.FindByEmailAsync("instructor@lms.com");
            if (instructor == null)
            {
                instructor = new ApplicationUser
                {
                    UserName = "instructor@lms.com",
                    Email = "instructor@lms.com",
                    FirstName = "John",
                    LastName = "Doe",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(instructor, "Instructor@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(instructor, "Instructor");
            }
            else
            {
                if (!await userManager.IsInRoleAsync(instructor, "Instructor"))
                    await userManager.AddToRoleAsync(instructor, "Instructor");
            }

            ApplicationUser? student = await userManager.FindByEmailAsync("student@lms.com");
            if (student == null)
            {
                student = new ApplicationUser
                {
                    UserName = "student@lms.com",
                    Email = "student@lms.com",
                    FirstName = "Jane",
                    LastName = "Smith",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(student, "Student@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(student, "Student");
            }

            if (!await context.Courses.AnyAsync())
            {
                var course1 = new Course
                {
                    Title = "Introduction to C# Programming",
                    Description = "Learn the basics of C# programming language from scratch. This course covers variables, control structures, OOP concepts, and more.",
                    Category = "Programming",
                    Status = CourseStatus.Published,
                    InstructorId = instructor!.Id
                };

                var course2 = new Course
                {
                    Title = "Web Development with ASP.NET Core",
                    Description = "Build modern web applications using ASP.NET Core MVC. Topics include routing, controllers, views, Entity Framework, and deployment.",
                    Category = "Web Development",
                    Status = CourseStatus.Published,
                    InstructorId = instructor!.Id
                };

                context.Courses.AddRange(course1, course2);
                await context.SaveChangesAsync();

                var lessons = new List<Lesson>
                {
                    new Lesson { Title = "Setting up the Development Environment", Content = "Install Visual Studio and .NET SDK. Learn how to create your first C# project.", Order = 1, DurationMinutes = 20, CourseId = course1.Id, Type = LessonType.Text },
                    new Lesson { Title = "Variables and Data Types", Content = "Learn about int, string, bool, double and other data types in C#.", Order = 2, DurationMinutes = 30, CourseId = course1.Id, Type = LessonType.Text },
                    new Lesson { Title = "Control Flow", Content = "Master if-else statements, switch cases, loops (for, while, foreach).", Order = 3, DurationMinutes = 35, CourseId = course1.Id, Type = LessonType.Text },
                    new Lesson { Title = "Methods and Functions", Content = "Understand how to define and call methods, parameters, return types.", Order = 4, DurationMinutes = 40, CourseId = course1.Id, Type = LessonType.Text },
                    new Lesson { Title = "Object-Oriented Programming", Content = "Classes, objects, inheritance, polymorphism, encapsulation.", Order = 5, DurationMinutes = 60, CourseId = course1.Id, Type = LessonType.Text },
                };

                var lessons2 = new List<Lesson>
                {
                    new Lesson { Title = "ASP.NET Core Overview", Content = "Introduction to the ASP.NET Core framework and its architecture.", Order = 1, DurationMinutes = 25, CourseId = course2.Id, Type = LessonType.Text },
                    new Lesson { Title = "Controllers and Actions", Content = "Learn how controllers handle HTTP requests and return responses.", Order = 2, DurationMinutes = 35, CourseId = course2.Id, Type = LessonType.Text },
                    new Lesson { Title = "Views and Razor Syntax", Content = "Build dynamic HTML pages using Razor templating engine.", Order = 3, DurationMinutes = 45, CourseId = course2.Id, Type = LessonType.Text },
                };

                context.Lessons.AddRange(lessons);
                context.Lessons.AddRange(lessons2);
                await context.SaveChangesAsync();

                var quiz = new Quiz
                {
                    Title = "C# Basics Quiz",
                    Description = "Test your knowledge of C# fundamentals",
                    PassingScore = 60,
                    TimeLimitMinutes = 15,
                    CourseId = course1.Id
                };
                context.Quizzes.Add(quiz);
                await context.SaveChangesAsync();

                var questions = new List<Question>
                {
                    new Question
                    {
                        Text = "What is the correct way to declare an integer variable in C#?",
                        Points = 1,
                        Order = 1,
                        QuizId = quiz.Id,
                        Answers = new List<Answer>
                        {
                            new Answer { Text = "int x = 5;", IsCorrect = true },
                            new Answer { Text = "integer x = 5;", IsCorrect = false },
                            new Answer { Text = "var x: int = 5;", IsCorrect = false },
                            new Answer { Text = "x = 5 as int;", IsCorrect = false }
                        }
                    },
                    new Question
                    {
                        Text = "Which keyword is used to define a class in C#?",
                        Points = 1,
                        Order = 2,
                        QuizId = quiz.Id,
                        Answers = new List<Answer>
                        {
                            new Answer { Text = "class", IsCorrect = true },
                            new Answer { Text = "struct", IsCorrect = false },
                            new Answer { Text = "object", IsCorrect = false },
                            new Answer { Text = "define", IsCorrect = false }
                        }
                    },
                    new Question
                    {
                        Text = "What does OOP stand for?",
                        Points = 1,
                        Order = 3,
                        QuizId = quiz.Id,
                        Answers = new List<Answer>
                        {
                            new Answer { Text = "Object-Oriented Programming", IsCorrect = true },
                            new Answer { Text = "Object-Oriented Processes", IsCorrect = false },
                            new Answer { Text = "Ordered Object Programming", IsCorrect = false },
                            new Answer { Text = "Official Open Platform", IsCorrect = false }
                        }
                    }
                };
                context.Questions.AddRange(questions);
                await context.SaveChangesAsync();

                var enrollment = new Enrollment
                {
                    UserId = student!.Id,
                    CourseId = course1.Id
                };
                context.Enrollments.Add(enrollment);
                await context.SaveChangesAsync();
            }
        }
    }
}
