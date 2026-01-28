"""
Pool Test Strip Cropper - FastAPI Application
==============================================

A microservice that accepts pool test strip photos and returns
cropped images containing only the test strip.

Endpoints:
---------
- POST /crop          : Upload an image and get back the cropped image
- POST /crop/save     : Upload an image, crop it, and save to output folder
- GET  /health        : Health check endpoint
- GET  /              : API information

Usage Examples:
--------------
Using curl:
    # Get cropped image as response
    curl -X POST -F "file=@test_strip.jpg" http://localhost:8000/crop --output cropped.png
    
    # Save to server and get metadata
    curl -X POST -F "file=@test_strip.jpg" http://localhost:8000/crop/save

Using Python requests:
    import requests
    
    with open("test_strip.jpg", "rb") as f:
        response = requests.post("http://localhost:8000/crop", files={"file": f})
    
    with open("cropped.png", "wb") as f:
        f.write(response.content)
"""

import os
import uuid
from datetime import datetime
from pathlib import Path
from typing import Optional

from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import Response, JSONResponse

from app.cropper import crop_test_strip, load_image, save_image, encode_image_to_bytes


# =============================================================================
# CONFIGURATION
# =============================================================================

# Output folder for saved cropped images
OUTPUT_FOLDER = Path(os.getenv("OUTPUT_FOLDER", "/workspace/output"))

# Maximum file size (10 MB)
MAX_FILE_SIZE = 10 * 1024 * 1024

# Allowed image MIME types
ALLOWED_TYPES = {
    "image/jpeg",
    "image/png", 
    "image/webp",
    "image/bmp"
}


# =============================================================================
# FASTAPI APPLICATION
# =============================================================================

app = FastAPI(
    title="Pool Test Strip Cropper",
    description="Microservice for detecting and cropping pool test strips from images",
    version="1.0.0",
    docs_url="/docs",      # Swagger UI
    redoc_url="/redoc",    # ReDoc documentation
)


# =============================================================================
# STARTUP EVENT
# =============================================================================

@app.on_event("startup")
async def startup_event():
    """
    Runs when the application starts.
    Ensures the output directory exists.
    """
    OUTPUT_FOLDER.mkdir(parents=True, exist_ok=True)
    print(f"Output folder ready: {OUTPUT_FOLDER}")


# =============================================================================
# ENDPOINTS
# =============================================================================

@app.get("/", tags=["Info"])
async def root():
    """
    Root endpoint - provides API information.
    """
    return {
        "service": "Pool Test Strip Cropper",
        "version": "1.0.0",
        "description": "Upload pool test strip photos to crop them automatically",
        "endpoints": {
            "POST /crop": "Upload image, receive cropped image",
            "POST /crop/save": "Upload image, save cropped image to server",
            "GET /health": "Health check",
            "GET /docs": "API documentation (Swagger UI)"
        }
    }


@app.get("/health", tags=["Info"])
async def health_check():
    """
    Health check endpoint for container orchestration.
    Returns 200 OK if the service is running.
    """
    return {"status": "healthy", "timestamp": datetime.utcnow().isoformat()}


@app.post("/crop", tags=["Cropping"])
async def crop_image(
    file: UploadFile = File(..., description="Image file containing the pool test strip")
):
    """
    Crop a pool test strip from an uploaded image.
    
    **Parameters:**
    - `file`: Image file (JPEG, PNG, WebP, or BMP)
    
    **Returns:**
    - Cropped image as PNG file
    
    **Example:**
    ```bash
    curl -X POST -F "file=@test_strip.jpg" http://localhost:8000/crop --output cropped.png
    ```
    """
    # Validate file type
    if file.content_type not in ALLOWED_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid file type: {file.content_type}. Allowed: {', '.join(ALLOWED_TYPES)}"
        )
    
    # Read the file
    try:
        contents = await file.read()
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Error reading file: {str(e)}")
    
    # Check file size
    if len(contents) > MAX_FILE_SIZE:
        raise HTTPException(
            status_code=400,
            detail=f"File too large. Maximum size: {MAX_FILE_SIZE // 1024 // 1024} MB"
        )
    
    # Load the image
    try:
        image = load_image(contents)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    
    # Crop the test strip
    try:
        cropped_image, metadata = crop_test_strip(image)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error processing image: {str(e)}")
    
    # Encode the cropped image to PNG bytes
    image_bytes = encode_image_to_bytes(cropped_image, ".png")
    
    if image_bytes is None:
        raise HTTPException(status_code=500, detail="Error encoding cropped image")
    
    # Return the cropped image as a response
    return Response(
        content=image_bytes,
        media_type="image/png",
        headers={
            "X-Crop-Success": str(metadata["success"]),
            "X-Crop-Message": metadata["message"]
        }
    )


@app.post("/crop/save", tags=["Cropping"])
async def crop_and_save(
    file: UploadFile = File(..., description="Image file containing the pool test strip"),
    filename: Optional[str] = None
):
    """
    Crop a pool test strip and save it to the output folder.
    
    **Parameters:**
    - `file`: Image file (JPEG, PNG, WebP, or BMP)
    - `filename` (optional): Custom filename for the saved image (without extension)
    
    **Returns:**
    - JSON with the saved file path and detection metadata
    
    **Example:**
    ```bash
    curl -X POST -F "file=@test_strip.jpg" http://localhost:8000/crop/save
    ```
    """
    # Validate file type
    if file.content_type not in ALLOWED_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid file type: {file.content_type}. Allowed: {', '.join(ALLOWED_TYPES)}"
        )
    
    # Read the file
    try:
        contents = await file.read()
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Error reading file: {str(e)}")
    
    # Check file size
    if len(contents) > MAX_FILE_SIZE:
        raise HTTPException(
            status_code=400,
            detail=f"File too large. Maximum size: {MAX_FILE_SIZE // 1024 // 1024} MB"
        )
    
    # Load the image
    try:
        image = load_image(contents)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    
    # Crop the test strip
    try:
        cropped_image, metadata = crop_test_strip(image)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error processing image: {str(e)}")
    
    # Generate filename if not provided
    if filename:
        # Sanitize filename - remove path separators and dangerous characters
        safe_filename = "".join(c for c in filename if c.isalnum() or c in "-_")
    else:
        # Generate unique filename with timestamp
        timestamp = datetime.utcnow().strftime("%Y%m%d_%H%M%S")
        unique_id = str(uuid.uuid4())[:8]
        safe_filename = f"cropped_{timestamp}_{unique_id}"
    
    # Full output path
    output_path = OUTPUT_FOLDER / f"{safe_filename}.png"
    
    # Save the cropped image
    success = save_image(cropped_image, str(output_path))
    
    if not success:
        raise HTTPException(status_code=500, detail="Error saving cropped image")
    
    # Return metadata
    return JSONResponse({
        "success": True,
        "saved_to": str(output_path),
        "filename": f"{safe_filename}.png",
        "detection": metadata
    })


# =============================================================================
# RUN DIRECTLY (for development)
# =============================================================================

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
