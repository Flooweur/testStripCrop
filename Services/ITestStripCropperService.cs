namespace TestStripCropper.Services;

/// <summary>
/// Interface for the test strip cropping service.
/// Allows for dependency injection and easier testing.
/// </summary>
public interface ITestStripCropperService
{
    /// <summary>
    /// Processes an image containing a pool test strip and crops it to isolate the strip.
    /// </summary>
    /// <param name="inputStream">Stream containing the input image</param>
    /// <returns>Byte array containing the cropped image in PNG format</returns>
    Task<byte[]> CropTestStripAsync(Stream inputStream);
}
