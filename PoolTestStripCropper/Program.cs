using PoolTestStripCropper.Services;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// Service Configuration
// =============================================================================

// Add controllers for handling API endpoints
builder.Services.AddControllers();

// Register the test strip cropping service with dependency injection
// Using scoped lifetime so each request gets a fresh instance
builder.Services.AddScoped<ITestStripCroppingService, TestStripCroppingService>();

// Configure request size limits for file uploads
// This allows larger files to be uploaded (up to 50MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

// Add health checks support
builder.Services.AddHealthChecks();

// =============================================================================
// Application Configuration
// =============================================================================

var app = builder.Build();

// Configure the HTTP request pipeline

// In development, show detailed error pages
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Enable HTTPS redirection (can be disabled in Docker if running behind a reverse proxy)
// Uncomment the line below if you want HTTPS redirection enabled
// app.UseHttpsRedirection();

// Map controller endpoints
app.MapControllers();

// Map health check endpoint at /health
app.MapHealthChecks("/health");

// Add a simple root endpoint for service information
app.MapGet("/", () => Results.Ok(new
{
    Service = "Pool Test Strip Cropper",
    Version = "1.0.0",
    Description = "Microservice for detecting and cropping pool test strips from images",
    Endpoints = new
    {
        CropTestStrip = "POST /api/teststrip/crop",
        Health = "GET /api/teststrip/health",
        HealthCheck = "GET /health"
    }
}));

// =============================================================================
// Start the Application
// =============================================================================

app.Run();
