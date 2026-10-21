using EduCore.Data;
namespace EduCore.Features.Courses;

public sealed class CreateCourse(CoursesDb db) { public async Task<Course> Handle(string? title, int capacity, CancellationToken ct) { var course = new Course(title, capacity); db.Add(course); await db.SaveChangesAsync(ct); return course; } }
