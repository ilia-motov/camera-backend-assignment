using System.Text.Json.Serialization;
using CameraApi.Application;
using CameraApi.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IDistanceCalculator, HaversineDistanceCalculator>();
builder.Services.AddScoped<NearestCameraService>();
builder.Services.AddScoped<ICameraRepository, PostgresCameraRepository>();
builder.Services.AddDbContext<CameraDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Cameras")
        ?? throw new InvalidOperationException("ConnectionStrings:Cameras is required.");
    options.UseNpgsql(connectionString, postgres => postgres.CommandTimeout(10));
});

var app = builder.Build();
app.UseExceptionHandler();
app.MapControllers();
app.Run();

// Makes the entry point accessible to WebApplicationFactory integration tests.
public partial class Program;
