using Serilog;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using ModelContextProtocol.AspNetCore;
using UAI.ZeroVP.API.Mcp;
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
        // Serialize enums as strings for readability in curl and Swagger.
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

// Standardized error responses using ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Time abstraction registered as a singleton service across DI.
builder.Services.AddSingleton(TimeProvider.System);

// In-memory storage for load tests; persistent JSON storage otherwise.
if (isLoadTest)
{
    builder.Services.AddInMemoryTodoData(options =>
    {
        options.SeedSampleData = false;
    });
}
else
{
    builder.Services.AddJsonFileTodoData(options =>
        builder.Configuration
            .GetSection(
                UAI.ZeroVP.Data.Json.JsonFileTodoOptions.SectionName)
            .Bind(options));
}

// Application availability endpoint.
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
            Description = "Sample ToDo API for Docker learning purposes. " +
                          "Supports persistent JSON storage, shared access " +
                          "across multiple API instances, and optimistic concurrency."
        };

        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register MCP services before building the application.
// MCP requires both Development and explicit configuration.
var mcpEnabled = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Mcp:Enabled");

if (mcpEnabled)
{
    var mcpJsonOptions = new System.Text.Json.JsonSerializerOptions(
        System.Text.Json.JsonSerializerDefaults.Web)
    {
        TypeInfoResolver =
            new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    mcpJsonOptions.Converters.Add(new JsonStringEnumConverter());

    builder.Services.AddMcpServer()
        .WithHttpTransport(options =>
        {
            options.SessionMode = HttpServerSessionMode.Stateless;
        })
        .WithTools<TodoTools>(serializerOptions: mcpJsonOptions);
}

var app = builder.Build();

app.UseSerilogRequestLogging();

// Swagger can be enabled explicitly, including in containers.
var swaggerEnabled = app.Configuration.GetValue(
    "Swagger:Enabled",
    app.Environment.IsDevelopment());

// --- HTTP pipeline --------------------------------------------------------

// No HTTPS redirection: TLS termination is handled by a reverse proxy.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (swaggerEnabled)
{
    app.MapOpenApi();

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

if (mcpEnabled)
{
    app.MapMcp("/mcp");
}

// Redirect to Swagger when enabled, otherwise to the todos endpoint.
app.MapGet("/", () =>
        Results.Redirect(swaggerEnabled ? "/swagger" : "/api/todos"))
    .ExcludeFromDescription();

app.Run();

/// <summary>
/// Entry point exposed for ASP.NET Core integration testing.
/// </summary>
public partial class Program { }