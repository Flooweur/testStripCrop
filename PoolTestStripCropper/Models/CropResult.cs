namespace PoolTestStripCropper.Models;

/// <summary>
/// Represents the result of a test strip cropping operation.
/// </summary>
public class CropResult
{
    /// <summary>
    /// Indicates whether the cropping operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The file path where the cropped image was saved (relative to output folder).
    /// Only populated when Success is true.
    /// </summary>
    public string? OutputFileName { get; set; }

    /// <summary>
    /// The full path to the cropped image file.
    /// Only populated when Success is true.
    /// </summary>
    public string? OutputFilePath { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// Only populated when Success is false.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CropResult Successful(string outputFileName, string outputFilePath)
    {
        return new CropResult
        {
            Success = true,
            OutputFileName = outputFileName,
            OutputFilePath = outputFilePath
        };
    }

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static CropResult Failed(string errorMessage)
    {
        return new CropResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
