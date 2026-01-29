namespace TestStripCropper.Models;

/// <summary>
/// Response model returned after successfully cropping a test strip image.
/// </summary>
public class CropResponse
{
    /// <summary>
    /// Indicates whether the cropping operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message providing details about the operation result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The filename of the saved cropped image (if successful).
    /// </summary>
    public string? OutputFileName { get; set; }

    /// <summary>
    /// The full path to the saved cropped image (if successful).
    /// </summary>
    public string? OutputPath { get; set; }
}

/// <summary>
/// Response model for error scenarios.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Optional details for debugging (only in development).
    /// </summary>
    public string? Details { get; set; }
}
