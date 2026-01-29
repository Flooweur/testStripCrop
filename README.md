# Pool Test Strip Cropper Microservice

A .NET 8 microservice that automatically detects and crops pool test strips from photos. The service uses edge detection algorithms to identify the test strip boundaries and saves the cropped result.

## Features

- **Automatic Test Strip Detection**: Uses Sobel edge detection to find test strip boundaries
- **REST API**: Simple HTTP API for uploading and processing images
- **Docker Ready**: Fully containerized for easy deployment
- **Swagger UI**: Interactive API documentation at the root URL
- **Output Management**: Cropped images are saved with timestamps for easy tracking

## Prerequisites

- Docker and Docker Compose (recommended)
- OR .NET 8 SDK (for local development)

## Quick Start with Docker

### 1. Build and Run

```bash
# Navigate to the project directory
cd TestStripCropper

# Build and start the container
docker-compose up -d --build

# Check if the container is running
docker-compose ps

# View logs
docker-compose logs -f
```

### 2. Access the Service

- **Swagger UI**: http://localhost:8080
- **Health Check**: http://localhost:8080/health

### 3. Test the API

```bash
# Upload an image for cropping
curl -X POST "http://localhost:8080/api/crop" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@/path/to/your/test-strip-photo.jpg"

# List all cropped images
curl "http://localhost:8080/api/outputs"

# Download a specific cropped image
curl "http://localhost:8080/api/outputs/your-image_cropped_20240129_123456_789.png" --output cropped.png
```

## API Endpoints

### POST /api/crop

Upload an image containing a pool test strip. The service will detect the strip, crop the image, and save the result.

**Request:**
- Method: `POST`
- Content-Type: `multipart/form-data`
- Body: `file` - The image file (JPEG, PNG, BMP, or GIF)

**Response:**
```json
{
  "success": true,
  "message": "Test strip cropped successfully",
  "outputFileName": "image_cropped_20240129_123456_789.png",
  "outputPath": "/app/output/image_cropped_20240129_123456_789.png"
}
```

### GET /api/outputs

List all cropped images in the output folder.

**Response:**
```json
{
  "count": 2,
  "files": [
    {
      "fileName": "test_cropped_20240129_123456_789.png",
      "fullPath": "/app/output/test_cropped_20240129_123456_789.png",
      "size": 45678,
      "createdAt": "2024-01-29T12:34:56Z"
    }
  ]
}
```

### GET /api/outputs/{fileName}

Download a specific cropped image.

### GET /health

Health check endpoint for container orchestration.

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-29T12:34:56Z"
}
```

## Algorithm Explanation

The cropping algorithm uses **Sobel edge detection** to identify the test strip boundaries:

1. **Grayscale Conversion**: Convert the input image to grayscale for edge analysis
2. **Sobel Edge Detection**: Apply Sobel operators to detect horizontal and vertical edges
3. **Column Analysis**: Analyze each column to find regions with high edge density (vertical edges indicate strip boundaries)
4. **Row Analysis**: Within the detected columns, analyze rows to find top/bottom boundaries
5. **Padding**: Add a small padding around the detected region
6. **Cropping**: Extract the detected region from the original image

### Key Parameters (configurable in `TestStripCropperService.cs`)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `MinStripWidthRatio` | 0.05 | Minimum strip width as percentage of image width |
| `PaddingRatio` | 0.02 | Padding to add around detected strip |
| `EdgeThreshold` | 30 | Minimum edge magnitude (0-255) to consider |
| `MinVerticalContinuity` | 0.1 | Minimum edge density for column/row detection |

## Project Structure

```
TestStripCropper/
├── Models/
│   └── CropResponse.cs       # API response models
├── Services/
│   ├── ITestStripCropperService.cs  # Service interface
│   └── TestStripCropperService.cs   # Cropping algorithm implementation
├── output/                   # Cropped images directory
├── Program.cs               # Application entry point & API endpoints
├── appsettings.json         # Application configuration
├── Dockerfile               # Docker image definition
├── docker-compose.yml       # Docker Compose configuration
├── .dockerignore            # Docker build exclusions
└── README.md                # This file
```

## Local Development

### Build and Run Locally

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run

# The API will be available at http://localhost:8080
```

### Configuration

Edit `appsettings.json` to modify:
- Logging levels
- Server port (default: 8080)
- Other ASP.NET Core settings

## Docker Commands Reference

```bash
# Build the image
docker build -t test-strip-cropper .

# Run the container
docker run -d -p 8080:8080 -v $(pwd)/output:/app/output --name test-strip-cropper test-strip-cropper

# Stop the container
docker stop test-strip-cropper

# Remove the container
docker rm test-strip-cropper

# View logs
docker logs test-strip-cropper
```

## Assumptions & Limitations

1. **Test Strip Orientation**: The photo must have the test strip oriented vertically (pointing upward)
2. **Single Strip**: The algorithm assumes one test strip per image
3. **Contrast**: The test strip should have visible edges that contrast with the background
4. **Image Quality**: Better quality images produce better detection results

## Troubleshooting

### Poor Cropping Results

If the algorithm produces poor results:
1. Ensure the test strip is clearly visible and oriented vertically
2. Try adjusting the `EdgeThreshold` parameter (lower for low-contrast images)
3. Ensure adequate lighting in the photo
4. Check that the test strip has clear, distinct edges

### Container Won't Start

```bash
# Check container logs
docker-compose logs

# Verify port 8080 is available
netstat -tulpn | grep 8080
```

## License

This project is provided as-is for educational and practical purposes.
