using DriverConnectApp.API;
using DriverConnectApp.API.Services;
using DriverConnectApp.Infrastructure.Identity;
using DriverConnectApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ADD THIS: HttpClient factory registration
builder.Services.AddHttpClient();

// ADD THIS: HttpContextAccessor for accessing current user in services
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IMultiTenantWhatsAppService, MultiTenantWhatsAppService>();

// Database Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// CORS Configuration - Enhanced for media files
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:5001", "http://localhost:5000", "http://157.180.87.7:5001",
                "https://157.180.87.7:5001",
                "https://onestopvan.work.gd",
                "http://onestopvan.work.gd")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Range");
    });
});

// Add Logging
builder.Services.AddLogging();

// Configure Kestrel for large file uploads
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524288000; // 500MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS must be before other middleware
app.UseCors("AllowFrontend");
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Configure static files with proper CORS headers and caching
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Console.WriteLine($"Created uploads directory: {uploadsPath}");
}

// Enhanced static files middleware for uploads
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        // Add CORS headers for all static files
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Range, Accept-Ranges");
        ctx.Context.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Length, Content-Range, Accept-Ranges");
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600"); // Cache for 1 hour

        // Enable range requests for video/audio files
        var fileExtension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        if (new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv", ".mp3", ".wav", ".ogg", ".m4a", ".aac" }.Contains(fileExtension))
        {
            ctx.Context.Response.Headers.Append("Accept-Ranges", "bytes");
        }
    }
});

// Serve wwwroot static files
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Serve SPA
app.MapFallbackToFile("index.html");

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
// Initialize database and seed data - SIMPLIFIED
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        context.Database.Migrate();
        Console.WriteLine("✅ Database migrated successfully");

        await SeedData.Initialize(services);
        Console.WriteLine("✅ Database seeded successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ An error occurred while migrating or seeding the database: {ex.Message}");
    }
}

app.Run();