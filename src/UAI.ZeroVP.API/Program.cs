using Serilog;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using UAI.ZeroVP.Data.DependencyInjection;
using UAI.ZeroVP.Data.InMemory;


var builder = WebApplication.CreateBuilder(args);

// Výkonnostní testy používají lokální dočasný soubor.
// Běžná aplikace ukládá logy do lokálního uživatelského profilu.
// Cestu lze přepsat konfigurací Serilog:FilePath.
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

// Konzolové logování ponecháváme pro lokální vývoj.
// Při zátěžových testech a v produkci nevytváří další režii.
if (builder.Environment.IsDevelopment())
{
    loggerConfiguration.WriteTo.Console();
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();


// --- Služby ---------------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Výčty posílá API jako řetězce ("High"), ne jako čísla – čitelnější v curl i ve Swaggeru.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Jednotné chybové odpovědi ve formátu ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Zdroj času jako služba – kontrolery i repozitář si ho berou z DI.
builder.Services.AddSingleton(TimeProvider.System);

// Datová vrstva: paměťové úložiště. Nastavení se čte ze sekce "Todo",
// v kontejneru ho lze přepsat proměnnou prostředí Todo__SeedSampleData.
builder.Services.AddInMemoryTodoData(options =>
    builder.Configuration.GetSection(InMemoryTodoOptions.SectionName).Bind(options));

// Kontrola zdraví aplikace – později ji využije HEALTHCHECK v Dockerfile.
builder.Services.AddHealthChecks();

// Popis API ve formátu OpenAPI. Dokument generuje vestavěná knihovna .NET 10
// (Microsoft.AspNetCore.OpenApi) a vystavuje ho na /openapi/v1.json.
// Swagger UI je pak jen prohlížeč tohoto dokumentu – žádný druhý generátor.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "UAI.ZeroVP – ToDo API",
            Version = "v1",
            Description = "Ukázkové API pro výuku Dockeru. Data jsou uložena pouze "
                        + "v paměti procesu a po zastavení kontejneru se ztratí."
        };

        return Task.CompletedTask;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UAI.ZeroVP API v1");
    // Nastaví Swagger přímo na úvodní URL adresu aplikace (např. http://localhost:5258)
    c.RoutePrefix = string.Empty; 
});

// Swagger UI je ve vývoji zapnutý; v kontejneru (prostředí Production) se dá
// zapnout proměnnou prostředí Swagger__Enabled=true.
var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());

// --- HTTP pipeline --------------------------------------------------------

// Bez přesměrování na HTTPS: v kontejneru se vystavuje pouze HTTP port
// a TLS řeší až reverzní proxy před ním.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (swaggerEnabled)
{
    // Strojově čitelný popis API.
    app.MapOpenApi();

    // Interaktivní dokumentace nad tímto popisem – http://localhost:5258/swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "UAI.ZeroVP – ToDo API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "UAI.ZeroVP – ToDo API";
    });
}

app.MapHealthChecks("/health");
app.MapControllers();

// Kořen vede na Swagger, ať má "docker run" hned co ukázat.
// Když je Swagger vypnutý, přesměruje rovnou na seznam úkolů.
app.MapGet("/", () => Results.Redirect(swaggerEnabled ? "/swagger" : "/api/todos"))
   .ExcludeFromDescription();

app.Run();

/// <summary>
/// Entry point exposed for ASP.NET Core integration testing.
/// </summary>
public partial class Program { }
