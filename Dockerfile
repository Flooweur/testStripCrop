# ==============================================================================
# Pool Test Strip Cropper - Dockerfile
# ==============================================================================
# This Dockerfile creates a production-ready container for the pool test strip
# cropping microservice.
#
# Build:   docker build -t pool-test-strip-cropper .
# Run:     docker run -p 8000:8000 -v $(pwd)/output:/app/output pool-test-strip-cropper
# ==============================================================================

# Use Python 3.11 slim image for a good balance of features and size
FROM python:3.11-slim

# Set metadata
LABEL maintainer="Pool Test Strip Cropper Service"
LABEL description="Microservice for detecting and cropping pool test strips from images"
LABEL version="1.0.0"

# Set environment variables
# PYTHONDONTWRITEBYTECODE: Prevents Python from writing .pyc files
# PYTHONUNBUFFERED: Ensures logs are output immediately (important for Docker)
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV OUTPUT_FOLDER=/app/output

# Set working directory
WORKDIR /app

# Install system dependencies required by OpenCV
# libglib2.0-0: Required by OpenCV
# libgl1-mesa-glx: OpenGL support (needed even for headless)
# libsm6, libxext6, libxrender-dev: X11 libraries (some OpenCV builds need these)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Copy requirements first (Docker layer caching optimization)
# If requirements don't change, this layer is cached
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY app/ ./app/

# Create output directory
RUN mkdir -p /app/output

# Expose the API port
EXPOSE 8000

# Health check - Docker will mark container as unhealthy if this fails
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')" || exit 1

# Run the application with uvicorn
# --host 0.0.0.0: Listen on all interfaces (required for Docker)
# --port 8000: Port to listen on
# --workers 1: Single worker (increase for production with more cores)
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
