using TestStripCropper.Models;
using TestStripCropper.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Service Configuration
// ============================================================================

// Register the test strip cropper service for dependency injection
builder.Services.AddScoped<ITestStripCropperService, TestStripCropperService>();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Pool Test Strip Cropper API", 
        Version = "v1",
        Description = "Microservice for detecting and cropping pool test strips from photos"
    });
});

var app = builder.Build();

// ============================================================================
// Middleware Configuration
// ============================================================================

// Enable Swagger in all environments for easy testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pool Test Strip Cropper API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at root
});

// ============================================================================
// Ensure output directory exists
// ============================================================================
var outputPath = Path.Combine(app.Environment.ContentRootPath, "output");
if (!Directory.Exists(outputPath))
{
    Directory.CreateDirectory(outputPath);
}

// ============================================================================
// API Endpoints
// ============================================================================

/// <summary>
/// Health check endpoint to verify the service is running.
/// </summary>
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi()
    .WithDescription("Returns the health status of the service");

/// <summary>
/// Main endpoint for cropping pool test strip images.
/// Accepts an image file, detects the test strip, crops it, and saves the result.
/// </summary>
app.MapPost("/api/crop", async (
    IFormFile file,
    ITestStripCropperService cropperService,
    IWebHostEnvironment env,
    ILogger<Program> logger) =>
{
    // Validate input
    if (file == null || file.Length == 0)
    {
        logger.LogWarning("Crop request received with no file");
        return Results.BadRequest(new ErrorResponse 
        { 
            Error = "No file was uploaded" 
        });
    }

    // Validate file type (basic check)
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
    {
        logger.LogWarning("Invalid file type uploaded: {Extension}", extension);
        return Results.BadRequest(new ErrorResponse 
        { 
            Error = $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}" 
        });
    }

    try
    {
        logger.LogInformation("Processing image: {FileName}, Size: {Size} bytes", 
            file.FileName, file.Length);

        // Process the image
        using var inputStream = file.OpenReadStream();
        var croppedImageBytes = await cropperService.CropTestStripAsync(inputStream);

        // Generate output filename with timestamp for uniqueness
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var originalName = Path.GetFileNameWithoutExtension(file.FileName);
        var outputFileName = $"{originalName}_cropped_{timestamp}.png";
        var fullOutputPath = Path.Combine(outputPath, outputFileName);

        // Save the cropped image
        await File.WriteAllBytesAsync(fullOutputPath, croppedImageBytes);

        logger.LogInformation("Image cropped successfully. Output: {OutputPath}", fullOutputPath);

        return Results.Ok(new CropResponse
        {
            Success = true,
            Message = "Test strip cropped successfully",
            OutputFileName = outputFileName,
            OutputPath = fullOutputPath
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing image: {FileName}", file.FileName);
        
        return Results.Problem(
            detail: app.Environment.IsDevelopment() ? ex.Message : "An error occurred while processing the image",
            statusCode: 500,
            title: "Image Processing Error"
        );
    }
})
.WithName("CropTestStrip")
.WithOpenApi()
.WithDescription("Upload a pool test strip photo to crop and isolate the test strip")
.DisableAntiforgery(); // Required for file upload in minimal APIs

/// <summary>
/// Endpoint to list all cropped images in the output folder.
/// </summary>
app.MapGet("/api/outputs", (IWebHostEnvironment env) =>
{
    var files = Directory.GetFiles(outputPath)
        .Where(f => !Path.GetFileName(f).StartsWith(".")) // Exclude hidden files
        .Select(f => new 
        {
            FileName = Path.GetFileName(f),
            FullPath = f,
            Size = new FileInfo(f).Length,
            CreatedAt = File.GetCreationTimeUtc(f)
        })
        .OrderByDescending(f => f.CreatedAt)
        .ToList();

    return Results.Ok(new { Count = files.Count, Files = files });
})
.WithName("ListOutputs")
.WithOpenApi()
.WithDescription("List all cropped images in the output folder");

/// <summary>
/// Endpoint to download a specific cropped image.
/// </summary>
app.MapGet("/api/outputs/{fileName}", (string fileName, IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(outputPath, fileName);
    
    if (!File.Exists(filePath))
    {
        return Results.NotFound(new ErrorResponse { Error = $"File not found: {fileName}" });
    }

    return Results.File(filePath, "image/png", fileName);
})
.WithName("DownloadOutput")
.WithOpenApi()
.WithDescription("Download a specific cropped image from the output folder");

app.Run();
