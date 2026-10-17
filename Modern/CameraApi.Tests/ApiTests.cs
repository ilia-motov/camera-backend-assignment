using System.Net;
using System.Net.Http.Json;
using CameraApi.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CameraApi.Tests;

public sealed class ApiTests
{
    [Fact]
    public async Task InvalidCoordinatesAndLimitReturn400WithoutDatabaseRead()
    {
        var repository = new StubRepository([]);
        using var factory = new TestApiFactory(repository);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/cameras/nearest",
            new { latitude = 91, longitude = 181, limit = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Latitude", problem.Errors.Keys);
        Assert.Contains("Longitude", problem.Errors.Keys);
        Assert.Contains("Limit", problem.Errors.Keys);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task AbortedHttpRequestCancelsRepositoryOperation()
    {
        var repository = new BlockingRepository();
        using var factory = new TestApiFactory(repository);
        using var client = factory.CreateClient();
        using var cts = new CancellationTokenSource();
        var request = client.PostAsJsonAsync("/api/cameras/nearest",
            new { latitude = 0, longitude = 0, limit = 1 }, cts.Token);
        await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);
        await repository.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class BlockingRepository : ICameraRepository
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<Camera>> GetActiveAsync(CancellationToken cancellationToken)
        {
            Started.SetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled.SetResult(); throw; }
            return [];
        }
    }
}

internal sealed class TestApiFactory(ICameraRepository repository) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICameraRepository>();
            services.AddSingleton(repository);
        });
    }
}
