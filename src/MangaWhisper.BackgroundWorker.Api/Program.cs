using Microsoft.EntityFrameworkCore;
using Serilog;
using MangaWhisper.Infrastructure.Data;
using MangaWhisper.Domain.Factories;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Repositories;
using MangaWhisper.Infrastructure.Services;
using MangaWhisper.Infrastructure.Factories;
using MangaWhisper.Application.Services;

// Load environment variables from .env file
DotNetEnv.Env.Load();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/background-worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting MangaWhisper Background Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog();

    // Get connection string from environment variable
    var connectionString = Environment.GetEnvironmentVariable("DefaultConnection")
                          ?? throw new InvalidOperationException("Database connection string 'DefaultConnection' not found in environment variables.");

    // Entity Framework DbContext
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Query DbContext for read operations (Dapper)
    builder.Services.AddScoped<QueryDbContext>(provider =>
        new QueryDbContext(connectionString));

    // HttpClient
    builder.Services.AddHttpClient();

    // Repositories
    builder.Services.AddScoped<IMangaCheckerRepository, MangaCheckerRepository>();
    builder.Services.AddScoped<IChapterRepository, ChapterRepository>();
    builder.Services.AddScoped<IMangaRepository, MangaRepository>();

    // Services
    builder.Services.AddScoped<IChapterCheckingService, ChapterCheckingService>();

    // Factories
    builder.Services.AddScoped<IChapterCheckerFactory, ChapterCheckerFactory>();

    // Background service
    builder.Services.AddHostedService<ChapterCheckingBackgroundService>();

    var host = builder.Build();

    Log.Information("MangaWhisper Background Worker configured successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
