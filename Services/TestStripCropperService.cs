using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TestStripCropper.Services;

/// <summary>
/// Service responsible for detecting and cropping pool test strips from images.
/// 
/// Algorithm Overview:
/// 1. Convert image to grayscale for edge detection
/// 2. Apply Sobel edge detection to find edges
/// 3. Scan for vertical edges (since strip is always upward-oriented)
/// 4. Find the bounding box of the main object
/// 5. Apply padding and crop the result
/// 
/// Assumptions:
/// - Test strip is always rotated to point upward
/// - Test strip has distinct edges that contrast with background
/// - Test strip is the primary subject in the image
/// </summary>
public class TestStripCropperService : ITestStripCropperService
{
    // Minimum percentage of image width that the strip should occupy
    private const float MinStripWidthRatio = 0.05f;
    
    // Padding percentage to add around the detected strip
    private const float PaddingRatio = 0.02f;
    
    // Edge detection threshold (0-255)
    private const int EdgeThreshold = 30;
    
    // Minimum vertical continuity required to consider a column as part of strip
    private const float MinVerticalContinuity = 0.1f;

    /// <summary>
    /// Detects and crops the test strip from the input image.
    /// </summary>
    /// <param name="inputStream">Input image stream</param>
    /// <returns>Cropped image as byte array</returns>
    public async Task<byte[]> CropTestStripAsync(Stream inputStream)
    {
        // Load the image
        using var image = await Image.LoadAsync<Rgba32>(inputStream);
        
        // Step 1: Create a grayscale version for edge detection
        using var grayscale = image.Clone();
        grayscale.Mutate(x => x.Grayscale());
        
        // Step 2: Detect edges using Sobel-like operator
        var edges = DetectEdges(grayscale);
        
        // Step 3: Find the bounding box of the test strip
        var boundingBox = FindTestStripBoundingBox(edges, image.Width, image.Height);
        
        // Step 4: Apply padding to the bounding box
        var paddedBox = ApplyPadding(boundingBox, image.Width, image.Height);
        
        // Step 5: Crop the image
        image.Mutate(x => x.Crop(paddedBox));
        
        // Step 6: Convert to byte array
        using var outputStream = new MemoryStream();
        await image.SaveAsPngAsync(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>
    /// Performs edge detection using a Sobel-like operator.
    /// Returns a 2D array of edge magnitudes (0-255).
    /// </summary>
    private byte[,] DetectEdges(Image<Rgba32> grayscale)
    {
        int width = grayscale.Width;
        int height = grayscale.Height;
        var edges = new byte[height, width];
        
        // Sobel kernels for horizontal and vertical edge detection
        // Gx kernel for horizontal edges (vertical lines)
        int[,] gx = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
        // Gy kernel for vertical edges (horizontal lines)  
        int[,] gy = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

        // Process the frame to access pixels efficiently
        grayscale.ProcessPixelRows(accessor =>
        {
            for (int y = 1; y < height - 1; y++)
            {
                Span<Rgba32> rowAbove = accessor.GetRowSpan(y - 1);
                Span<Rgba32> rowCurrent = accessor.GetRowSpan(y);
                Span<Rgba32> rowBelow = accessor.GetRowSpan(y + 1);

                for (int x = 1; x < width - 1; x++)
                {
                    // Get the 3x3 neighborhood pixel values (using Red channel as grayscale)
                    int[,] neighborhood = {
                        { rowAbove[x - 1].R, rowAbove[x].R, rowAbove[x + 1].R },
                        { rowCurrent[x - 1].R, rowCurrent[x].R, rowCurrent[x + 1].R },
                        { rowBelow[x - 1].R, rowBelow[x].R, rowBelow[x + 1].R }
                    };

                    // Apply Sobel kernels
                    int sumX = 0, sumY = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            sumX += gx[i, j] * neighborhood[i, j];
                            sumY += gy[i, j] * neighborhood[i, j];
                        }
                    }

                    // Calculate edge magnitude
                    int magnitude = (int)Math.Sqrt(sumX * sumX + sumY * sumY);
                    edges[y, x] = (byte)Math.Min(255, magnitude);
                }
            }
        });

        return edges;
    }

    /// <summary>
    /// Finds the bounding box of the test strip based on edge detection results.
    /// Uses column analysis to find vertical regions with high edge density.
    /// </summary>
    private Rectangle FindTestStripBoundingBox(byte[,] edges, int width, int height)
    {
        // Analyze columns to find regions with consistent vertical edges
        var columnEdgeDensity = new float[width];
        
        for (int x = 0; x < width; x++)
        {
            int edgeCount = 0;
            for (int y = 0; y < height; y++)
            {
                if (edges[y, x] > EdgeThreshold)
                {
                    edgeCount++;
                }
            }
            columnEdgeDensity[x] = (float)edgeCount / height;
        }

        // Find the left and right boundaries of the strip
        // Look for regions where edge density is above minimum threshold
        int leftBound = 0;
        int rightBound = width - 1;
        
        // Find left boundary - first column with significant edges
        for (int x = 0; x < width; x++)
        {
            if (columnEdgeDensity[x] > MinVerticalContinuity)
            {
                leftBound = x;
                break;
            }
        }

        // Find right boundary - last column with significant edges
        for (int x = width - 1; x >= 0; x--)
        {
            if (columnEdgeDensity[x] > MinVerticalContinuity)
            {
                rightBound = x;
                break;
            }
        }

        // Ensure minimum strip width
        int minWidth = (int)(width * MinStripWidthRatio);
        if (rightBound - leftBound < minWidth)
        {
            // If detection failed, use center portion of image
            int centerX = width / 2;
            leftBound = Math.Max(0, centerX - width / 4);
            rightBound = Math.Min(width - 1, centerX + width / 4);
        }

        // Analyze rows within the detected columns to find vertical bounds
        var rowEdgeDensity = new float[height];
        for (int y = 0; y < height; y++)
        {
            int edgeCount = 0;
            for (int x = leftBound; x <= rightBound; x++)
            {
                if (edges[y, x] > EdgeThreshold)
                {
                    edgeCount++;
                }
            }
            rowEdgeDensity[y] = (float)edgeCount / (rightBound - leftBound + 1);
        }

        // Find top and bottom boundaries
        int topBound = 0;
        int bottomBound = height - 1;

        for (int y = 0; y < height; y++)
        {
            if (rowEdgeDensity[y] > MinVerticalContinuity)
            {
                topBound = y;
                break;
            }
        }

        for (int y = height - 1; y >= 0; y--)
        {
            if (rowEdgeDensity[y] > MinVerticalContinuity)
            {
                bottomBound = y;
                break;
            }
        }

        return new Rectangle(
            leftBound,
            topBound,
            rightBound - leftBound + 1,
            bottomBound - topBound + 1
        );
    }

    /// <summary>
    /// Applies padding to the bounding box while ensuring it stays within image bounds.
    /// </summary>
    private Rectangle ApplyPadding(Rectangle box, int imageWidth, int imageHeight)
    {
        int paddingX = (int)(box.Width * PaddingRatio);
        int paddingY = (int)(box.Height * PaddingRatio);

        int newX = Math.Max(0, box.X - paddingX);
        int newY = Math.Max(0, box.Y - paddingY);
        int newWidth = Math.Min(imageWidth - newX, box.Width + 2 * paddingX);
        int newHeight = Math.Min(imageHeight - newY, box.Height + 2 * paddingY);

        return new Rectangle(newX, newY, newWidth, newHeight);
    }
}
