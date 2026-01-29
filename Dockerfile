# ============================================================================
# Pool Test Strip Cropper - Dockerfile
# ============================================================================
# Multi-stage build for optimized image size
# Stage 1: Build the application
# Stage 2: Create the runtime image
# ============================================================================

# --------------------------------
# Stage 1: Build
# --------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
# This layer is cached unless the csproj changes
COPY ["TestStripCropper.csproj", "./"]
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build the application in Release mode
RUN dotnet build -c Release -o /app/build

# Publish the application
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --------------------------------
# Stage 2: Runtime
# --------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create output directory for cropped images
# This directory can be mounted as a volume for persistence
RUN mkdir -p /app/output

# Copy the published application from build stage
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose the port the app runs on
EXPOSE 8080

# Health check to ensure the container is healthy
# Uses wget since curl is not available in the base aspnet image
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "TestStripCropper.dll"]
