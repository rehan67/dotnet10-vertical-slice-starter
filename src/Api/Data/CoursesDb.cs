using EduCore.Features.Courses;
using Microsoft.EntityFrameworkCore;
namespace EduCore.Data;

public sealed class CoursesDb(DbContextOptions<CoursesDb> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Course>(e => { e.ToTable("courses"); e.HasKey(c => c.Id); e.Property(c => c.Id).HasColumnName("id"); e.Property(c => c.Title).HasColumnName("title").HasMaxLength(120); e.Property(c => c.Capacity).HasColumnName("capacity"); e.HasMany(c => c.Enrollments).WithOne().HasForeignKey(c => c.CourseId); e.Navigation(c => c.Enrollments).UsePropertyAccessMode(PropertyAccessMode.Field); });
        model.Entity<Enrollment>(e => { e.ToTable("enrollments"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.CourseId, x.StudentId }).IsUnique(); });
    }
}
