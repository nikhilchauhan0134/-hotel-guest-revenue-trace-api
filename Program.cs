using HotelGraphApi.Configuration;
using HotelGraphApi.Services;

var builder = WebApplication.CreateBuilder(args);

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
        var origins = new List<string>
        {
            "http://localhost:5173",
            "http://localhost:5174",
            "http://127.0.0.1:5173"
        };

        var extraOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
        if (!string.IsNullOrWhiteSpace(extraOrigins))
        {
            origins.AddRange(extraOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        policy.WithOrigins(origins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
