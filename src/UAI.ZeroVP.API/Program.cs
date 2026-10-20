using Serilog;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using UAI.ZeroVP.Data.DependencyInjection;
using UAI.ZeroVP.Data.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Performance tests use a local temporary file.
// Normal application runs store logs in the local user profile.
// The path can be overridden via Serilog:FilePath configuration.
var isLoadTest = builder.Environment.IsEnvironment("LoadTest");

var logFilePath = isLoadTest
    ? Path.Combine(Path.GetTempPath(), "zerovp-loadtest-.txt")
    : builder.Configuration["Serilog:FilePath"]
        ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "UAI.ZeroVP",
            "logs",
            "log-.txt");

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override(
        "Microsoft",
        Serilog.Events.LogEventLevel.Warning)
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day);

// Keep console logging for local development.
// Avoids additional overhead during load tests and in production.
if (builder.Environment.IsDevelopment())
{
    loggerConfiguration.WriteTo.Console();
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();


// --- Services -------------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums are serialized as strings ("High") instead of numbers for better readability in curl and Swagger.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Standardized error responses using ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Time abstraction registered as a singleton service across DI.
builder.Services.AddSingleton(TimeProvider.System);

// Data layer: in-memory storage. Configuration is bound from the "Todo" section,
// can be overridden in container via Todo__SeedSampleData environment variable.
builder.Services.AddInMemoryTodoData(options =>
    builder.Configuration.GetSection(InMemoryTodoOptions.SectionName).Bind(options));

// Health checks service for Docker container monitoring.
builder.Services.AddHealthChecks();

// OpenAPI document generator (.NET 10 Microsoft.AspNetCore.OpenApi).
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "UAI.ZeroVP – ToDo API",
            Version = "v1",
            Description = "Sample API for Docker learning purposes. Data is stored purely "
                        + "in process memory and is lost once the container stops."
        };

        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSerilogRequestLogging();

// Swagger UI is enabled by default in Development; in container (Production) it can be
// enabled via Swagger__Enabled=true environment variable.
var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());

// --- HTTP pipeline --------------------------------------------------------

// No HTTPS redirection: container exposes HTTP only; TLS termination is handled by reverse proxy.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (swaggerEnabled)
{
    // Machine-readable OpenAPI JSON document.
    app.MapOpenApi();

    // Interactive Swagger UI documentation.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "UAI.ZeroVP – ToDo API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "UAI.ZeroVP – ToDo API";
    });
}

app.MapHealthChecks("/health");
app.MapControllers();

// Root route redirects to Swagger documentation if enabled, otherwise to todos endpoint.
app.MapGet("/", () => Results.Redirect(swaggerEnabled ? "/swagger" : "/api/todos"))
   .ExcludeFromDescription();

app.Run();

/// <summary>
/// Entry point exposed for ASP.NET Core integration testing.
/// </summary>
public partial class Program { }
