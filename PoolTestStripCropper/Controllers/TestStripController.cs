using Microsoft.AspNetCore.Mvc;
using PoolTestStripCropper.Models;
using PoolTestStripCropper.Services;

namespace PoolTestStripCropper.Controllers;

/// <summary>
/// API Controller for pool test strip image processing.
/// Provides endpoints for uploading images and receiving cropped test strip results.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestStripController : ControllerBase
{
    private readonly ITestStripCroppingService _croppingService;
    private readonly ILogger<TestStripController> _logger;

    // Maximum allowed file size (10 MB)
    private const long MaxFileSize = 10 * 1024 * 1024;

    // Allowed image content types
    private static readonly string[] AllowedContentTypes = new[]
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/bmp",
        "image/webp"
    };

    public TestStripController(
        ITestStripCroppingService croppingService,
        ILogger<TestStripController> logger)
    {
        _croppingService = croppingService;
        _logger = logger;
    }

    /// <summary>
    /// Processes an uploaded image to detect and crop the pool test strip.
    /// </summary>
    /// <param name="file">The image file containing a pool test strip.</param>
    /// <returns>
    /// On success: 200 OK with the cropping result details.
    /// On validation failure: 400 Bad Request with error details.
    /// On processing failure: 422 Unprocessable Entity with error details.
    /// </returns>
    /// <remarks>
    /// Sample request:
    ///     POST /api/teststrip/crop
    ///     Content-Type: multipart/form-data
    ///     Body: file=[image file]
    /// 
    /// Sample response (success):
    /// {
    ///     "success": true,
    ///     "outputFileName": "teststrip_cropped_20240115_123456_789.jpg",
    ///     "outputFilePath": "output/teststrip_cropped_20240115_123456_789.jpg"
    /// }
    /// </remarks>
    [HttpPost("crop")]
    [ProducesResponseType(typeof(CropResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CropResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(CropResult), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CropTestStrip(IFormFile file)
    {
        _logger.LogInformation("Received crop request for file: {FileName}, Size: {Size} bytes", 
            file?.FileName, file?.Length);

        // Validate the uploaded file
        var validationResult = ValidateFile(file);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Read the file into a byte array
        byte[] imageBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file!.CopyToAsync(memoryStream);
            imageBytes = memoryStream.ToArray();
        }

        // Process the image
        var result = await _croppingService.CropTestStripAsync(imageBytes, file.FileName);

        if (result.Success)
        {
            _logger.LogInformation("Successfully cropped test strip. Output: {OutputFile}", result.OutputFileName);
            return Ok(result);
        }
        else
        {
            _logger.LogWarning("Failed to crop test strip: {Error}", result.ErrorMessage);
            return UnprocessableEntity(result);
        }
    }

    /// <summary>
    /// Health check endpoint to verify the service is running.
    /// </summary>
    /// <returns>200 OK with service status information.</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "Pool Test Strip Cropper",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Validates the uploaded file for size and content type.
    /// </summary>
    /// <param name="file">The file to validate.</param>
    /// <returns>An IActionResult if validation fails, null if validation passes.</returns>
    private IActionResult? ValidateFile(IFormFile? file)
    {
        // Check if file was provided
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file provided in request");
            return BadRequest(CropResult.Failed("No file was uploaded. Please provide an image file."));
        }

        // Check file size
        if (file.Length > MaxFileSize)
        {
            _logger.LogWarning("File too large: {Size} bytes (max: {MaxSize})", file.Length, MaxFileSize);
            return BadRequest(CropResult.Failed($"File size exceeds the maximum allowed size of {MaxFileSize / (1024 * 1024)} MB."));
        }

        // Check content type
        if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            _logger.LogWarning("Invalid content type: {ContentType}", file.ContentType);
            return BadRequest(CropResult.Failed(
                $"Invalid file type '{file.ContentType}'. Allowed types: {string.Join(", ", AllowedContentTypes)}"));
        }

        return null; // Validation passed
    }
}
