# =============================================================================
# Pool Test Strip Cropper - Dockerfile
# =============================================================================
# This Dockerfile builds and runs the Pool Test Strip Cropper microservice.
# It uses a multi-stage build to minimize the final image size.
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Build Stage
# -----------------------------------------------------------------------------
# Uses the .NET SDK to restore packages and build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file first to leverage Docker layer caching
# This allows Docker to skip package restore if only source files changed
COPY ["PoolTestStripCropper.csproj", "./"]
RUN dotnet restore "PoolTestStripCropper.csproj"

# Copy the rest of the source code
COPY . .

# Build the application in Release mode
RUN dotnet build "PoolTestStripCropper.csproj" -c Release -o /app/build

# -----------------------------------------------------------------------------
# Stage 2: Publish Stage
# -----------------------------------------------------------------------------
# Creates an optimized, self-contained deployment
FROM build AS publish
RUN dotnet publish "PoolTestStripCropper.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 3: Runtime Stage
# -----------------------------------------------------------------------------
# Uses the minimal ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install OpenCV dependencies required by OpenCvSharp
# These are the native libraries that OpenCvSharp needs to function
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Core image processing libraries
    libgdiplus \
    libc6-dev \
    # OpenCV dependencies
    libopencv-dev \
    # Image format support
    libjpeg62-turbo \
    libpng16-16 \
    libtiff6 \
    libwebp7 \
    # Additional required libraries
    libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Create the output directory for cropped images
# This directory will store all processed images
RUN mkdir -p /app/output

# Copy the published application from the publish stage
COPY --from=publish /app/publish .

# Set environment variables
# ASPNETCORE_URLS: Configure the application to listen on port 8080
# DOTNET_RUNNING_IN_CONTAINER: Indicates the app is running in a container
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port 8080 for the web API
EXPOSE 8080

# Health check to verify the container is running properly
# Checks the health endpoint every 30 seconds
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "PoolTestStripCropper.dll"]
