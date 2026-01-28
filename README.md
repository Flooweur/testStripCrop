# Pool Test Strip Cropper

A Python microservice that automatically detects and crops pool test strips from photographs. The service runs in Docker and exposes a REST API for easy integration.

## Table of Contents

- [Features](#features)
- [How It Works](#how-it-works)
- [Quick Start](#quick-start)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Project Structure](#project-structure)
- [Development](#development)
- [Troubleshooting](#troubleshooting)

## Features

- **Automatic Detection**: Uses computer vision to detect the test strip in the image
- **Background Agnostic**: Works with various backgrounds (not just neutral ones)
- **REST API**: Simple HTTP endpoints for integration
- **Docker Ready**: Easy deployment with Docker and Docker Compose
- **Auto Documentation**: Built-in Swagger UI at `/docs`

## How It Works

### Algorithm Overview

The cropping algorithm uses **contour detection** via OpenCV to locate the test strip:

1. **Grayscale Conversion**: The image is converted to grayscale for edge detection
2. **Noise Reduction**: Gaussian blur is applied to reduce noise while preserving edges
3. **Edge Detection**: Canny edge detection finds the boundaries of objects
4. **Contour Finding**: OpenCV finds closed shapes (contours) in the edge image
5. **Filtering**: Contours are filtered by:
   - **Area**: Must be between 1% and 95% of the image
   - **Aspect Ratio**: Must be tall and narrow (1.5x to 15x taller than wide)
   - **Rectangularity**: Preference for rectangular shapes
6. **Cropping**: The best matching contour is used to crop the image with a small margin

### Why Contour Detection?

- **No Training Required**: Unlike machine learning, no labeled dataset needed
- **Robust to Backgrounds**: Works with various non-neutral backgrounds
- **Lightweight**: Fast execution without heavy dependencies
- **Predictable**: Deterministic results based on clear rules

## Quick Start

### Using Docker (Recommended)

```bash
# Clone the repository
git clone <repository-url>
cd pool-test-strip-cropper

# Build and run with Docker Compose
docker-compose up --build

# The service is now running at http://localhost:8000
```

### Using Docker directly

```bash
# Build the image
docker build -t pool-test-strip-cropper .

# Run the container
docker run -p 8000:8000 -v $(pwd)/output:/app/output pool-test-strip-cropper
```

### Local Development (without Docker)

```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run the service
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

## API Reference

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/` | API information |
| `GET` | `/health` | Health check |
| `GET` | `/docs` | Swagger UI documentation |
| `POST` | `/crop` | Upload image, receive cropped image |
| `POST` | `/crop/save` | Upload image, save to server |

### POST /crop

Upload an image and receive the cropped image as the response.

**Request:**
- Content-Type: `multipart/form-data`
- Body: `file` - Image file (JPEG, PNG, WebP, or BMP)

**Response:**
- Content-Type: `image/png`
- Body: Cropped image

**Example with curl:**

```bash
curl -X POST \
  -F "file=@my_test_strip_photo.jpg" \
  http://localhost:8000/crop \
  --output cropped_result.png
```

**Example with Python:**

```python
import requests

with open("my_test_strip_photo.jpg", "rb") as f:
    response = requests.post(
        "http://localhost:8000/crop",
        files={"file": f}
    )

# Save the cropped image
with open("cropped_result.png", "wb") as f:
    f.write(response.content)
```

### POST /crop/save

Upload an image, crop it, and save the result to the server's output folder.

**Request:**
- Content-Type: `multipart/form-data`
- Body: 
  - `file` - Image file (JPEG, PNG, WebP, or BMP)
  - `filename` (optional) - Custom filename for the saved image

**Response:**
- Content-Type: `application/json`

```json
{
  "success": true,
  "saved_to": "/app/output/cropped_20240101_120000_abc12345.png",
  "filename": "cropped_20240101_120000_abc12345.png",
  "detection": {
    "success": true,
    "original_size": {"width": 1920, "height": 1080},
    "cropped_size": {"width": 200, "height": 800},
    "bbox": {"x": 860, "y": 140, "width": 200, "height": 800},
    "detection_method": "contour_detection",
    "message": "Test strip successfully detected and cropped"
  }
}
```

**Example with curl:**

```bash
curl -X POST \
  -F "file=@my_test_strip_photo.jpg" \
  http://localhost:8000/crop/save
```

### GET /health

Health check endpoint for container orchestration (Kubernetes, Docker Swarm, etc.).

**Response:**

```json
{
  "status": "healthy",
  "timestamp": "2024-01-01T12:00:00.000000"
}
```

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OUTPUT_FOLDER` | `/app/output` | Directory for saved cropped images |

### Algorithm Parameters

The detection algorithm can be tuned by modifying constants in `app/cropper.py`:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `MIN_AREA_RATIO` | `0.01` | Minimum contour area (% of image) |
| `MAX_AREA_RATIO` | `0.95` | Maximum contour area (% of image) |
| `MIN_ASPECT_RATIO` | `1.5` | Minimum height/width ratio |
| `MAX_ASPECT_RATIO` | `15.0` | Maximum height/width ratio |
| `CROP_MARGIN_PERCENT` | `0.05` | Margin around detected strip (%) |

## Project Structure

```
pool-test-strip-cropper/
├── app/
│   ├── __init__.py          # Package marker
│   ├── main.py              # FastAPI application & endpoints
│   └── cropper.py           # Image processing & cropping logic
├── output/                   # Cropped images saved here
├── Dockerfile               # Docker image definition
├── docker-compose.yml       # Docker Compose configuration
├── .dockerignore            # Files to exclude from Docker build
├── requirements.txt         # Python dependencies
└── README.md                # This file
```

### Key Files Explained

- **`app/main.py`**: FastAPI application with REST endpoints. Handles file uploads, validation, and response formatting.

- **`app/cropper.py`**: Core image processing logic. Contains the contour detection algorithm and image manipulation utilities.

- **`Dockerfile`**: Multi-stage Docker build optimized for small image size and fast builds.

- **`docker-compose.yml`**: Easy one-command deployment with volume mounts and health checks.

## Development

### Prerequisites

- Python 3.11+
- pip

### Setup

```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Run with auto-reload
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

### Testing the API

```bash
# Test health endpoint
curl http://localhost:8000/health

# Test crop endpoint with a sample image
curl -X POST -F "file=@sample.jpg" http://localhost:8000/crop --output result.png
```

### Interactive API Documentation

Visit http://localhost:8000/docs for the interactive Swagger UI where you can test all endpoints directly in your browser.

## Troubleshooting

### "No suitable test strip contour found"

The algorithm couldn't detect a test strip. This can happen if:

1. **Test strip is too small**: Ensure the strip occupies at least 1% of the image
2. **Test strip is too large**: The strip should be less than 95% of the image
3. **Wrong aspect ratio**: The strip should be significantly taller than wide
4. **Poor contrast**: Ensure good lighting and contrast between strip and background
5. **Horizontal orientation**: The algorithm expects the strip to be vertical (pointing upward)

**Solutions:**
- Adjust `MIN_AREA_RATIO` and `MAX_AREA_RATIO` in `cropper.py`
- Ensure proper lighting when taking photos
- Make sure the test strip is clearly visible against the background

### "Could not decode image"

The uploaded file couldn't be read as an image:

- Ensure the file is a valid image (JPEG, PNG, WebP, or BMP)
- Check that the file isn't corrupted
- Verify the file extension matches the actual format

### Docker container not starting

Check the logs:

```bash
docker-compose logs -f
```

Common issues:
- Port 8000 already in use: Change the port mapping in `docker-compose.yml`
- Insufficient memory: Increase Docker memory limits

## License

MIT License - See LICENSE file for details.
