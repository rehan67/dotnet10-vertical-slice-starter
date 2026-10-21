using System.Net;
using System.Net.Http.Json;
using EduCore.Features.Courses;
using EduCore.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace EduCore.Tests;

public sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public TestApplication() { connection.Open(); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            if (Environment.GetEnvironmentVariable("TEST_POSTGRES") is string postgres) { services.RemoveAll<DbContextOptions<CoursesDb>>(); services.RemoveAll<IDbContextOptionsConfiguration<CoursesDb>>(); services.AddDbContext<CoursesDb>(options => options.UseNpgsql(postgres)); }
            else { services.RemoveAll<DbContextOptions<CoursesDb>>(); services.RemoveAll<IDbContextOptionsConfiguration<CoursesDb>>(); services.AddDbContext<CoursesDb>(options => options.UseSqlite(connection)); }
        });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}
public sealed class DomainTests
{
    [Theory][InlineData(0)][InlineData(1001)] public void RejectsInvalidCapacity(int capacity) => Assert.Throws<ArgumentException>(() => new Course("Architecture", capacity));
    [Fact] public void DuplicateEnrollmentIsIdempotent() { var course = new Course("Architecture", 1); var student = Guid.NewGuid(); Assert.Equal(course.Enroll(student).Id, course.Enroll(student).Id); Assert.Single(course.Enrollments); }
    [Fact] public void CapacityCannotBeExceeded() { var course = new Course("Architecture", 1); course.Enroll(Guid.NewGuid()); Assert.Throws<InvalidOperationException>(() => course.Enroll(Guid.NewGuid())); }
}
public sealed class ApiTests
{
    [Fact]
    public async Task CompleteFlowPreservesCapacityAndIdempotency()
    {
        using var app = new TestApplication(); using var client = app.CreateClient();
        var created = await client.PostAsJsonAsync("/courses", new { title = "Architecture", capacity = 1 }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var course = await created.Content.ReadFromJsonAsync<CourseResult>(); var studentId = Guid.NewGuid(); var route = $"/courses/{course!.Id}/enrollments";
        var first = await client.PostAsJsonAsync(route, new { studentId }); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var repeated = await client.PostAsJsonAsync(route, new { studentId }); Assert.Equal(await first.Content.ReadAsStringAsync(), await repeated.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { studentId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { studentId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/courses/{Guid.NewGuid()}/enrollments", new { studentId })).StatusCode);
        var read = await client.GetFromJsonAsync<CourseResult>($"/courses/{course.Id}"); Assert.Equal(1, read!.Enrolled);
    }
    [Fact] public async Task InvalidRequestsReturnProblemsWithoutInternals() { using var app = new TestApplication(); using var client = app.CreateClient(); var result = await client.PostAsJsonAsync("/courses", new { title = "", capacity = 0 }); Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode); Assert.DoesNotContain("Exception", await result.Content.ReadAsStringAsync()); }
    [PostgresFact]
    public async Task ConcurrentStudentsCannotOversubscribePostgres()
    {
        using var app = new TestApplication(); using var client = app.CreateClient(); var created = await client.PostAsJsonAsync("/courses", new { title = "Concurrent", capacity = 2 }); var course = await created.Content.ReadFromJsonAsync<CourseResult>();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.PostAsJsonAsync($"/courses/{course!.Id}/enrollments", new { studentId = Guid.NewGuid() })));
        Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.OK)); Assert.Equal(8, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
    [PostgresFact]
    public async Task ConcurrentDuplicateRequestsCommitOneEnrollment()
    {
        using var app = new TestApplication(); using var client = app.CreateClient();
        var created = await client.PostAsJsonAsync("/courses", new { title = "Replay", capacity = 1 });
        var course = await created.Content.ReadFromJsonAsync<CourseResult>(); var studentId = Guid.NewGuid();
        var replies = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.PostAsJsonAsync($"/courses/{course!.Id}/enrollments", new { studentId })));
        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.OK, reply.StatusCode));
        var bodies = await Task.WhenAll(replies.Select(reply => reply.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        var read = await client.GetFromJsonAsync<CourseResult>($"/courses/{course!.Id}"); Assert.Equal(1, read!.Enrolled);
    }
    private record CourseResult(Guid Id, string Title, int Capacity, int Enrolled);
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (Environment.GetEnvironmentVariable("TEST_POSTGRES") is null) Skip = "Set TEST_POSTGRES to run real PostgreSQL concurrency tests."; }
}
