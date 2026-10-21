using EduCore.Features.Courses;
using EduCore.Features.Enrollments;
using EduCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CoursesDb>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Courses") ?? "Host=localhost;Port=5434;Database=educore_slices;Username=lab;Password=local-only-change-me"));
builder.Services.AddScoped<CreateCourse>(); builder.Services.AddScoped<EnrollStudent>();
var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch { ArgumentException => (400, "Invalid request"), KeyNotFoundException => (404, "Course not found"), InvalidOperationException => (409, "Course capacity exceeded"), _ => (500, "Request could not be completed") };
    await Results.Problem(statusCode: status, title: title).ExecuteAsync(context);
}));
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoursesDb>().Database.EnsureCreatedAsync();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/courses", async (CreateRequest request, CreateCourse handler, CancellationToken ct) => { var course = await handler.Handle(request.Title, request.Capacity, ct); return Results.Created($"/courses/{course.Id}", new { course.Id, course.Title, course.Capacity }); });
app.MapGet("/courses/{id:guid}", async (Guid id, CoursesDb db, CancellationToken ct) => { var course = await db.Courses.Include(c => c.Enrollments).SingleOrDefaultAsync(c => c.Id == id, ct); return course is null ? Results.NotFound() : Results.Ok(new { course.Id, course.Title, course.Capacity, enrolled = course.Enrollments.Count }); });
app.MapPost("/courses/{id:guid}/enrollments", async (Guid id, EnrollRequest request, EnrollStudent handler, CancellationToken ct) => { var result = await handler.EnrollAsync(id, request.StudentId, ct); return Results.Ok(new { result.Id, result.CourseId, result.StudentId }); });
app.Run();
public record CreateRequest(string? Title, int Capacity);
public record EnrollRequest(Guid StudentId);
public partial class Program { }
