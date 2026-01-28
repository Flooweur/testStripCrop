# Pool Test Strip Cropper

A .NET 8 microservice that automatically detects and crops pool test strips from images. The service accepts an image containing a pool test strip (properly rotated with the strip pointing upwards), detects the strip using computer vision algorithms, and saves the cropped result.

## Table of Contents

- [Features](#features)
- [How It Works](#how-it-works)
- [Prerequisites](#prerequisites)
- [Quick Start with Docker](#quick-start-with-docker)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Project Structure](#project-structure)
- [Algorithm Details](#algorithm-details)
- [Development](#development)
- [Troubleshooting](#troubleshooting)

## Features

- **Automatic Test Strip Detection**: Uses OpenCV-based edge detection and contour analysis
- **Dockerized Deployment**: Ready-to-use Docker configuration for easy deployment
- **RESTful API**: Simple HTTP endpoint for image upload and processing
- **Configurable Output**: Cropped images are saved to a configurable output directory
- **Health Checks**: Built-in health check endpoints for monitoring
- **Comprehensive Logging**: Detailed logging for debugging and monitoring

## How It Works

The service uses a computer vision pipeline to detect and crop pool test strips:

1. **Image Loading**: The uploaded image is decoded and loaded into memory
2. **Preprocessing**: The image is converted to grayscale and blurred to reduce noise
3. **Edge Detection**: Canny edge detection identifies edges in the image
4. **Contour Detection**: The algorithm finds contours (continuous boundaries) in the edge image
5. **Rectangle Detection**: Contours are analyzed to find rectangular shapes matching test strip characteristics
6. **Cropping**: The original image is cropped to the detected test strip region
7. **Output**: The cropped image is saved to the output directory

## Prerequisites

### For Docker Deployment (Recommended)
- Docker 20.10 or later
- Docker Compose v2.0 or later (optional, for easier management)

### For Local Development
- .NET 8.0 SDK
- OpenCV native libraries (automatically handled by OpenCvSharp NuGet packages)

## Quick Start with Docker

### Option 1: Using Docker Compose (Recommended)

```bash
# Clone the repository
cd PoolTestStripCropper

# Build and start the service
docker-compose up --build

# Or run in detached mode
docker-compose up -d --build
```

### Option 2: Using Docker Directly

```bash
# Build the Docker image
docker build -t pool-test-strip-cropper .

# Run the container
docker run -d \
  --name pool-test-strip-cropper \
  -p 8080:8080 \
  -v $(pwd)/output:/app/output \
  pool-test-strip-cropper
```

### Test the Service

Once running, test the service:

```bash
# Check health
curl http://localhost:8080/health

# Check service info
curl http://localhost:8080/

# Crop a test strip image
curl -X POST \
  -F "file=@/path/to/your/test-strip-image.jpg" \
  http://localhost:8080/api/teststrip/crop
```

## API Reference

### POST /api/teststrip/crop

Uploads an image and crops the detected pool test strip.

**Request:**
- Method: `POST`
- Content-Type: `multipart/form-data`
- Body: Form field `file` containing the image

**Supported Image Formats:**
- JPEG/JPG
- PNG
- BMP
- WebP

**Maximum File Size:** 10 MB

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "outputFileName": "image_cropped_20240115_143022_456.jpg",
  "outputFilePath": "output/image_cropped_20240115_143022_456.jpg"
}
```

**Response (Processing Failed - 422 Unprocessable Entity):**
```json
{
  "success": false,
  "errorMessage": "Could not detect a test strip in the image."
}
```

**Response (Validation Failed - 400 Bad Request):**
```json
{
  "success": false,
  "errorMessage": "Invalid file type. Allowed types: image/jpeg, image/png, ..."
}
```

### GET /api/teststrip/health

Returns the health status of the service.

**Response (200 OK):**
```json
{
  "status": "Healthy",
  "service": "Pool Test Strip Cropper",
  "timestamp": "2024-01-15T14:30:22.456Z"
}
```

### GET /health

Standard health check endpoint (for container orchestration).

**Response (200 OK):**
```
Healthy
```

### GET /

Returns service information and available endpoints.

**Response (200 OK):**
```json
{
  "service": "Pool Test Strip Cropper",
  "version": "1.0.0",
  "description": "Microservice for detecting and cropping pool test strips from images",
  "endpoints": {
    "cropTestStrip": "POST /api/teststrip/crop",
    "health": "GET /api/teststrip/health",
    "healthCheck": "GET /health"
  }
}
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment (Development/Production) | `Production` |
| `ASPNETCORE_URLS` | URL(s) to listen on | `http://+:8080` |
| `OutputDirectory` | Directory for saving cropped images | `output` |

### Application Settings (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "PoolTestStripCropper": "Debug"
    }
  },
  "AllowedHosts": "*",
  "OutputDirectory": "output"
}
```

## Project Structure

```
PoolTestStripCropper/
├── Controllers/
│   └── TestStripController.cs    # API endpoint definitions
├── Models/
│   └── CropResult.cs             # Response model for crop operations
├── Services/
│   ├── ITestStripCroppingService.cs  # Service interface
│   └── TestStripCroppingService.cs   # Cropping algorithm implementation
├── Program.cs                    # Application entry point & configuration
├── appsettings.json             # Application configuration
├── Dockerfile                   # Docker build instructions
├── docker-compose.yml           # Docker Compose configuration
├── .dockerignore               # Files to exclude from Docker build
└── PoolTestStripCropper.csproj  # Project file with dependencies
```

## Algorithm Details

The test strip detection algorithm uses the following techniques:

### 1. Preprocessing
- **Grayscale Conversion**: Simplifies the image to single-channel intensity values
- **Gaussian Blur**: Reduces noise with a 5x5 kernel to prevent false edge detection

### 2. Edge Detection
- **Canny Edge Detection**: Identifies edges using gradient-based detection
  - Low threshold: 50
  - High threshold: 150
- **Morphological Closing**: Closes small gaps in detected edges

### 3. Contour Analysis
- **Contour Finding**: Extracts continuous boundaries from the edge image
- **Polygon Approximation**: Simplifies contours to basic shapes
- **Rectangle Detection**: Identifies 4-vertex convex polygons (rectangles)

### 4. Test Strip Selection
- **Aspect Ratio Filtering**: Test strips have characteristic tall, narrow shapes
  - Accepted ratios: 1.5-15 (vertical) or 0.067-0.67 (horizontal)
- **Area Filtering**: Ignores contours smaller than 1% of image area
- **Largest Rectangle Selection**: Chooses the largest qualifying rectangle

### 5. Cropping
- **Padding**: Adds 10px padding around detected region
- **Boundary Checking**: Ensures crop region stays within image bounds

## Development

### Local Development Setup

```bash
# Navigate to project directory
cd PoolTestStripCropper

# Restore dependencies
dotnet restore

# Run in development mode
dotnet run

# Or run with hot reload
dotnet watch run
```

### Building

```bash
# Build in Debug mode
dotnet build

# Build in Release mode
dotnet build -c Release

# Publish for deployment
dotnet publish -c Release -o ./publish
```

### Testing with curl

```bash
# Test with a local image
curl -X POST \
  -F "file=@test-strip.jpg" \
  http://localhost:5000/api/teststrip/crop
```

## Troubleshooting

### Common Issues

#### "Could not detect a test strip in the image"
- Ensure the test strip is clearly visible and in focus
- The test strip should be the prominent rectangular object in the image
- Try improving lighting conditions
- Make sure the test strip is oriented upward as expected

#### "Failed to decode the uploaded image"
- Verify the file is a valid image (JPEG, PNG, BMP, or WebP)
- Check if the file is corrupted
- Ensure the file size is under 10 MB

#### Docker container won't start
- Check if port 8080 is available: `lsof -i :8080`
- Verify Docker has sufficient resources
- Check container logs: `docker logs pool-test-strip-cropper`

#### OpenCV-related errors in Docker
- The Dockerfile includes all necessary OpenCV dependencies
- If issues persist, try rebuilding the image: `docker-compose build --no-cache`

### Viewing Logs

```bash
# Docker Compose
docker-compose logs -f

# Docker
docker logs -f pool-test-strip-cropper
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| OpenCvSharp4 | 4.10.0.20241108 | OpenCV wrapper for .NET |
| OpenCvSharp4.runtime.linux-x64 | 4.10.0.20240717 | Native OpenCV libraries for Linux |

## License

This project is provided as-is for educational and development purposes.
