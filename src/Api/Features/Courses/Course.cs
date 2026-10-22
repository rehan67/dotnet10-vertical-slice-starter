namespace EduCore.Features.Courses;

public sealed class Enrollment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CourseId { get; private set; }
    public Guid StudentId { get; private set; }

    private Enrollment() { }

    internal Enrollment(Guid courseId, Guid studentId)
    {
        CourseId = courseId;
        StudentId = studentId;
    }
}

public sealed class Course
{
    private readonly List<Enrollment> _enrollments = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = "";
    public int Capacity { get; private set; }
    public IReadOnlyCollection<Enrollment> Enrollments => _enrollments.AsReadOnly();

    private Course() { }

    public Course(string? title, int capacity)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120)
            throw new ArgumentException("Title must contain 1–120 characters.");
        if (capacity is < 1 or > 1000)
            throw new ArgumentException("Capacity must be between 1 and 1000.");
        Title = title.Trim();
        Capacity = capacity;
    }

    public Enrollment Enroll(Guid studentId)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("A student ID is required.");
        var existing = _enrollments.SingleOrDefault(e => e.StudentId == studentId);
        if (existing is not null)
            return existing;
        if (_enrollments.Count >= Capacity)
            throw new InvalidOperationException("The course is full.");
        var enrollment = new Enrollment(Id, studentId);
        _enrollments.Add(enrollment);
        return enrollment;
    }
}
