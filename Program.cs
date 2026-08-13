using HotelGraphApi.Configuration;
using HotelGraphApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Render/containers: avoid inotify file watcher limit on startup
foreach (var source in builder.Configuration.Sources)
{
    if (source is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource jsonSource)
    {
        jsonSource.ReloadOnChange = false;
    }
}

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.Configure<CognoDbSettings>(options =>
{
    options.Uri = Environment.GetEnvironmentVariable("COGNODB_URI")
        ?? builder.Configuration["CognoDb:Uri"]
        ?? string.Empty;
    options.Username = Environment.GetEnvironmentVariable("COGNODB_USERNAME")
        ?? builder.Configuration["CognoDb:Username"]
        ?? "cognodb";
    options.Password = Environment.GetEnvironmentVariable("COGNODB_PASSWORD")
        ?? builder.Configuration["CognoDb:Password"]
        ?? string.Empty;
});

builder.Services.AddSingleton<IGraphDatabaseService, GraphDatabaseService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<GuestTraceService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return false;
            }

            var localOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "http://localhost:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5173"
            };

            if (localOrigins.Contains(origin))
            {
                return true;
            }

            // Allow Render-hosted UI without manual env setup
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && uri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var extraOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
            if (string.IsNullOrWhiteSpace(extraOrigins))
            {
                return false;
            }

            return extraOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(origin, StringComparer.OrdinalIgnoreCase);
        })
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("ReactApp");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "Hotel Guest & Revenue Trace API",
    status = "running",
    health = "/api/health",
    swagger = "/swagger",
    seed = "POST /api/seed"
}));

app.Run();
