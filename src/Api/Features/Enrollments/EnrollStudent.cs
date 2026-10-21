using EduCore.Data;
using EduCore.Features.Courses;
using Microsoft.EntityFrameworkCore;
namespace EduCore.Features.Enrollments;

public sealed class EnrollStudent(CoursesDb db)
{
    public async Task<Enrollment> EnrollAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        // PostgreSQL row lock serialises enrolments across application replicas.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var query = db.Database.IsNpgsql() ? db.Courses.FromSqlInterpolated($"SELECT * FROM courses WHERE id = {courseId} FOR UPDATE") : db.Courses.Where(c => c.Id == courseId);
        var course = await query.SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Course not found.");
        await db.Entry(course).Collection(c => c.Enrollments).LoadAsync(ct);
        var enrollment = course.Enroll(studentId);
        if (db.Entry(enrollment).State == EntityState.Detached) db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return enrollment;
    }
}
