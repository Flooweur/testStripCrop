using PoolTestStripCropper.Models;

namespace PoolTestStripCropper.Services;

/// <summary>
/// Interface for the test strip cropping service.
/// Defines the contract for detecting and cropping pool test strips from images.
/// </summary>
public interface ITestStripCroppingService
{
    /// <summary>
    /// Processes an image to detect and crop the pool test strip.
    /// </summary>
    /// <param name="imageBytes">The raw bytes of the input image.</param>
    /// <param name="originalFileName">The original file name (used for generating output file name).</param>
    /// <returns>A result containing the path to the cropped image or error information.</returns>
    Task<CropResult> CropTestStripAsync(byte[] imageBytes, string originalFileName);
}
