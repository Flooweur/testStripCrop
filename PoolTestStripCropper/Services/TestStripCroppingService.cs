using OpenCvSharp;
using PoolTestStripCropper.Models;

namespace PoolTestStripCropper.Services;

/// <summary>
/// Service responsible for detecting and cropping pool test strips from images.
/// 
/// Algorithm Overview:
/// 1. Convert image to grayscale for edge detection
/// 2. Apply Gaussian blur to reduce noise
/// 3. Use Canny edge detection to find edges
/// 4. Find contours in the edge-detected image
/// 5. Filter contours to find rectangular shapes (test strips are rectangular)
/// 6. Select the largest rectangular contour as the test strip
/// 7. Crop the original image to the bounding rectangle
/// 8. Save the cropped result
/// 
/// This approach works well for test strips because:
/// - Test strips have distinct rectangular shapes
/// - Edge detection captures the strip boundaries regardless of background color
/// - The test strip is typically the largest rectangular object in a properly framed photo
/// </summary>
public class TestStripCroppingService : ITestStripCroppingService
{
    private readonly ILogger<TestStripCroppingService> _logger;
    private readonly string _outputDirectory;

    // Configuration constants for the cropping algorithm
    // These values are tuned for typical pool test strip images
    
    /// <summary>
    /// Gaussian blur kernel size. Larger values = more blur = less noise but less detail.
    /// 5x5 is a good balance for most images.
    /// </summary>
    private const int GaussianBlurKernelSize = 5;

    /// <summary>
    /// Lower threshold for Canny edge detection.
    /// Edges with gradient below this are discarded.
    /// </summary>
    private const double CannyLowThreshold = 50;

    /// <summary>
    /// Upper threshold for Canny edge detection.
    /// Edges with gradient above this are definitely edges.
    /// </summary>
    private const double CannyHighThreshold = 150;

    /// <summary>
    /// Epsilon factor for polygon approximation (percentage of contour perimeter).
    /// Lower values = more vertices = closer to original contour.
    /// 0.02 (2%) works well for detecting rectangles.
    /// </summary>
    private const double PolygonApproximationEpsilon = 0.02;

    /// <summary>
    /// Minimum area ratio (relative to image size) for a contour to be considered.
    /// Helps filter out small noise contours.
    /// </summary>
    private const double MinAreaRatio = 0.01;

    /// <summary>
    /// Padding (in pixels) to add around the detected test strip.
    /// Ensures we don't crop too tightly.
    /// </summary>
    private const int CropPadding = 10;

    public TestStripCroppingService(ILogger<TestStripCroppingService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        // Get output directory from configuration, default to "output" folder
        _outputDirectory = configuration.GetValue<string>("OutputDirectory") ?? "output";
        
        // Ensure output directory exists
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
            _logger.LogInformation("Created output directory: {OutputDirectory}", _outputDirectory);
        }
    }

    /// <inheritdoc />
    public async Task<CropResult> CropTestStripAsync(byte[] imageBytes, string originalFileName)
    {
        try
        {
            _logger.LogInformation("Starting test strip detection for file: {FileName}", originalFileName);

            // Step 1: Decode the image from bytes
            using var originalImage = Mat.FromImageData(imageBytes);
            if (originalImage.Empty())
            {
                _logger.LogError("Failed to decode image: {FileName}", originalFileName);
                return CropResult.Failed("Failed to decode the uploaded image. Please ensure it's a valid image file.");
            }

            _logger.LogDebug("Image loaded successfully. Size: {Width}x{Height}", originalImage.Width, originalImage.Height);

            // Step 2: Detect the test strip region
            var testStripRegion = DetectTestStrip(originalImage);
            if (testStripRegion == null)
            {
                _logger.LogWarning("No test strip detected in image: {FileName}", originalFileName);
                return CropResult.Failed("Could not detect a test strip in the image. Please ensure the test strip is clearly visible.");
            }

            _logger.LogInformation("Test strip detected at region: X={X}, Y={Y}, Width={Width}, Height={Height}",
                testStripRegion.Value.X, testStripRegion.Value.Y, 
                testStripRegion.Value.Width, testStripRegion.Value.Height);

            // Step 3: Crop the image to the detected region
            using var croppedImage = CropToRegion(originalImage, testStripRegion.Value);

            // Step 4: Generate output file name and save
            var outputFileName = GenerateOutputFileName(originalFileName);
            var outputFilePath = Path.Combine(_outputDirectory, outputFileName);

            // Save the cropped image
            await Task.Run(() => croppedImage.SaveImage(outputFilePath));

            _logger.LogInformation("Cropped image saved to: {OutputPath}", outputFilePath);

            return CropResult.Successful(outputFileName, outputFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing image: {FileName}", originalFileName);
            return CropResult.Failed($"An error occurred while processing the image: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects the test strip region in the image using edge detection and contour analysis.
    /// </summary>
    /// <param name="image">The input image (BGR format).</param>
    /// <returns>The bounding rectangle of the detected test strip, or null if not found.</returns>
    private Rect? DetectTestStrip(Mat image)
    {
        // Convert to grayscale for edge detection
        // Grayscale simplifies the image and makes edge detection more robust
        using var grayImage = new Mat();
        Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);

        // Apply Gaussian blur to reduce noise
        // This helps prevent false edges from image noise
        using var blurredImage = new Mat();
        Cv2.GaussianBlur(grayImage, blurredImage, new Size(GaussianBlurKernelSize, GaussianBlurKernelSize), 0);

        // Apply Canny edge detection
        // This finds edges (areas of rapid intensity change) in the image
        using var edges = new Mat();
        Cv2.Canny(blurredImage, edges, CannyLowThreshold, CannyHighThreshold);

        // Apply morphological operations to close gaps in edges
        // This helps connect broken edge segments
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var closedEdges = new Mat();
        Cv2.MorphologyEx(edges, closedEdges, MorphTypes.Close, kernel, iterations: 2);

        // Find contours in the edge image
        // Contours are curves joining continuous points with the same intensity
        Cv2.FindContours(closedEdges, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        _logger.LogDebug("Found {ContourCount} contours in image", contours.Length);

        // Calculate minimum area threshold based on image size
        double imageArea = image.Width * image.Height;
        double minArea = imageArea * MinAreaRatio;

        // Find the best rectangular contour
        // We're looking for the largest contour that approximates to a rectangle (4 vertices)
        Rect? bestRect = null;
        double bestArea = 0;

        foreach (var contour in contours)
        {
            // Calculate contour area
            double area = Cv2.ContourArea(contour);
            
            // Skip contours that are too small
            if (area < minArea)
                continue;

            // Approximate the contour to a polygon
            // This simplifies the contour shape
            double epsilon = PolygonApproximationEpsilon * Cv2.ArcLength(contour, true);
            var approxCurve = Cv2.ApproxPolyDP(contour, epsilon, true);

            // Check if the approximated contour has 4 vertices (rectangle)
            // Also check if it's convex (a proper rectangle should be convex)
            if (approxCurve.Length == 4 && Cv2.IsContourConvex(approxCurve))
            {
                // Check aspect ratio - test strips are typically tall and narrow
                var rect = Cv2.BoundingRect(approxCurve);
                double aspectRatio = (double)rect.Height / rect.Width;

                // Test strips typically have height > width (vertical orientation)
                // Accept aspect ratios between 1.5 and 10 (tall rectangles)
                // or between 0.1 and 0.67 (in case the strip is horizontal, we'll still detect it)
                bool isValidAspectRatio = (aspectRatio >= 1.5 && aspectRatio <= 15) || 
                                          (aspectRatio >= 0.067 && aspectRatio <= 0.67);

                if (isValidAspectRatio && area > bestArea)
                {
                    bestArea = area;
                    bestRect = rect;
                }
            }
        }

        // If no rectangle found with strict criteria, try a more lenient approach
        // Just find the largest contour with reasonable aspect ratio
        if (bestRect == null)
        {
            _logger.LogDebug("No strict rectangle found, trying lenient detection");
            
            foreach (var contour in contours.OrderByDescending(c => Cv2.ContourArea(c)))
            {
                double area = Cv2.ContourArea(contour);
                if (area < minArea)
                    continue;

                var rect = Cv2.BoundingRect(contour);
                double aspectRatio = (double)rect.Height / rect.Width;

                // More lenient aspect ratio check
                bool isReasonableShape = (aspectRatio >= 1.2 && aspectRatio <= 20) ||
                                         (aspectRatio >= 0.05 && aspectRatio <= 0.83);

                if (isReasonableShape)
                {
                    bestRect = rect;
                    break; // Take the largest one
                }
            }
        }

        return bestRect;
    }

    /// <summary>
    /// Crops the image to the specified region with padding.
    /// </summary>
    /// <param name="image">The original image.</param>
    /// <param name="region">The region to crop to.</param>
    /// <returns>The cropped image.</returns>
    private Mat CropToRegion(Mat image, Rect region)
    {
        // Add padding around the detected region
        // This ensures we don't crop too tightly and lose edge information
        int x = Math.Max(0, region.X - CropPadding);
        int y = Math.Max(0, region.Y - CropPadding);
        int width = Math.Min(image.Width - x, region.Width + (2 * CropPadding));
        int height = Math.Min(image.Height - y, region.Height + (2 * CropPadding));

        var paddedRegion = new Rect(x, y, width, height);

        // Create a new Mat containing only the cropped region
        return new Mat(image, paddedRegion);
    }

    /// <summary>
    /// Generates a unique output file name based on the original file name.
    /// </summary>
    /// <param name="originalFileName">The original file name.</param>
    /// <returns>A unique output file name.</returns>
    private string GenerateOutputFileName(string originalFileName)
    {
        // Get the file extension
        string extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".jpg"; // Default to jpg if no extension
        }

        // Get the base name without extension
        string baseName = Path.GetFileNameWithoutExtension(originalFileName);

        // Generate a timestamp-based unique name
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

        return $"{baseName}_cropped_{timestamp}{extension}";
    }
}
