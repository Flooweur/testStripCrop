"""
Pool Test Strip Cropper Module
==============================

This module contains the image processing logic to detect and crop
pool test strips from photographs.

Algorithm Overview:
------------------
1. Convert image to grayscale for edge detection
2. Apply Gaussian blur to reduce noise
3. Use Canny edge detection to find edges
4. Find contours (closed shapes) in the edge image
5. Filter contours to find rectangular shapes (test strips are rectangular)
6. Select the best candidate based on area and aspect ratio
7. Crop the image to the detected region with a small margin

Why Contour Detection?
---------------------
- Pool test strips are rectangular objects that contrast with backgrounds
- Contour detection works well with varying/non-neutral backgrounds
- It's robust to different lighting conditions
- No training data required (unlike ML approaches)
"""

import cv2
import numpy as np
from typing import Tuple, Optional


# =============================================================================
# CONFIGURATION - Tune these values if needed for your specific test strips
# =============================================================================

# Minimum area of detected contour (as percentage of image area)
# Prevents detecting tiny noise artifacts
MIN_AREA_RATIO = 0.01  # 1% of image

# Maximum area of detected contour (as percentage of image area)
# Prevents detecting the entire image border
MAX_AREA_RATIO = 0.95  # 95% of image

# Expected aspect ratio range for test strips (height/width)
# Most pool test strips are tall and narrow
MIN_ASPECT_RATIO = 1.5   # At least 1.5x taller than wide
MAX_ASPECT_RATIO = 15.0  # At most 15x taller than wide

# Margin to add around the cropped region (as percentage of the detected region)
# This ensures we don't cut off edges of the test strip
CROP_MARGIN_PERCENT = 0.05  # 5% margin on each side


# =============================================================================
# MAIN CROPPING FUNCTION
# =============================================================================

def crop_test_strip(image: np.ndarray) -> Tuple[np.ndarray, dict]:
    """
    Detect and crop a pool test strip from an image.
    
    Args:
        image: Input image as a numpy array (BGR format from OpenCV)
    
    Returns:
        Tuple containing:
        - Cropped image as numpy array
        - Metadata dict with detection info (success, bbox, confidence notes)
    
    Raises:
        ValueError: If image is None or invalid
    """
    # Validate input
    if image is None or image.size == 0:
        raise ValueError("Invalid image: image is None or empty")
    
    # Get image dimensions
    height, width = image.shape[:2]
    image_area = height * width
    
    # Store metadata about the detection process
    metadata = {
        "success": False,
        "original_size": {"width": width, "height": height},
        "detection_method": "contour_detection",
        "message": ""
    }
    
    # ---------------------------------------------------------------------
    # Step 1: Preprocess the image for edge detection
    # ---------------------------------------------------------------------
    
    # Convert to grayscale - edge detection works on single channel
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    
    # Apply Gaussian blur to reduce noise while preserving edges
    # Kernel size (5,5) is a good balance between noise reduction and detail
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    
    # ---------------------------------------------------------------------
    # Step 2: Edge detection using Canny algorithm
    # ---------------------------------------------------------------------
    
    # Canny edge detection finds edges based on gradient intensity
    # - Lower threshold (50): Edges below this are discarded
    # - Upper threshold (150): Edges above this are definitely edges
    # - Edges between thresholds are kept if connected to strong edges
    edges = cv2.Canny(blurred, 50, 150)
    
    # Dilate edges to close small gaps in contours
    # This helps when the test strip edge isn't perfectly continuous
    kernel = np.ones((3, 3), np.uint8)
    edges = cv2.dilate(edges, kernel, iterations=2)
    
    # ---------------------------------------------------------------------
    # Step 3: Find contours in the edge image
    # ---------------------------------------------------------------------
    
    # Find all contours in the edge image
    # RETR_EXTERNAL: Only get outermost contours (ignore nested ones)
    # CHAIN_APPROX_SIMPLE: Compress contours to save memory
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    if not contours:
        metadata["message"] = "No contours found in image"
        return image, metadata
    
    # ---------------------------------------------------------------------
    # Step 4: Find the best rectangular contour (the test strip)
    # ---------------------------------------------------------------------
    
    best_contour = None
    best_score = 0
    
    for contour in contours:
        # Get the minimum area bounding rectangle
        # This handles rotated rectangles, but since the image is already
        # oriented correctly, we use the upright bounding box
        x, y, w, h = cv2.boundingRect(contour)
        contour_area = w * h
        
        # Filter by area - skip if too small or too large
        area_ratio = contour_area / image_area
        if area_ratio < MIN_AREA_RATIO or area_ratio > MAX_AREA_RATIO:
            continue
        
        # Calculate aspect ratio (height/width since strips are vertical)
        aspect_ratio = h / w if w > 0 else 0
        
        # Filter by aspect ratio - test strips are tall and narrow
        if aspect_ratio < MIN_ASPECT_RATIO or aspect_ratio > MAX_ASPECT_RATIO:
            continue
        
        # Score based on how "rectangular" the contour is
        # Actual contour area vs bounding box area
        actual_area = cv2.contourArea(contour)
        rectangularity = actual_area / contour_area if contour_area > 0 else 0
        
        # Combined score: prefer larger, more rectangular shapes
        # Area ratio gives preference to larger contours
        # Rectangularity ensures it's actually rectangular
        score = area_ratio * rectangularity
        
        if score > best_score:
            best_score = score
            best_contour = (x, y, w, h)
    
    # ---------------------------------------------------------------------
    # Step 5: Crop the image to the detected region
    # ---------------------------------------------------------------------
    
    if best_contour is None:
        metadata["message"] = "No suitable test strip contour found"
        return image, metadata
    
    x, y, w, h = best_contour
    
    # Add margin around the detected region
    margin_x = int(w * CROP_MARGIN_PERCENT)
    margin_y = int(h * CROP_MARGIN_PERCENT)
    
    # Calculate crop coordinates with margin (clamped to image bounds)
    x1 = max(0, x - margin_x)
    y1 = max(0, y - margin_y)
    x2 = min(width, x + w + margin_x)
    y2 = min(height, y + h + margin_y)
    
    # Perform the crop
    cropped = image[y1:y2, x1:x2]
    
    # Update metadata with success info
    metadata["success"] = True
    metadata["bbox"] = {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1}
    metadata["cropped_size"] = {"width": x2 - x1, "height": y2 - y1}
    metadata["message"] = "Test strip successfully detected and cropped"
    
    return cropped, metadata


# =============================================================================
# UTILITY FUNCTIONS
# =============================================================================

def load_image(file_bytes: bytes) -> np.ndarray:
    """
    Load an image from bytes (e.g., from an uploaded file).
    
    Args:
        file_bytes: Raw bytes of the image file
    
    Returns:
        Image as numpy array in BGR format
    
    Raises:
        ValueError: If image cannot be decoded
    """
    # Convert bytes to numpy array
    nparr = np.frombuffer(file_bytes, np.uint8)
    
    # Decode the image
    image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    
    if image is None:
        raise ValueError("Could not decode image. Ensure it's a valid image file.")
    
    return image


def save_image(image: np.ndarray, filepath: str) -> bool:
    """
    Save an image to a file.
    
    Args:
        image: Image as numpy array
        filepath: Path where to save the image
    
    Returns:
        True if saved successfully, False otherwise
    """
    try:
        success = cv2.imwrite(filepath, image)
        return success
    except Exception:
        return False


def encode_image_to_bytes(image: np.ndarray, format: str = ".png") -> Optional[bytes]:
    """
    Encode an image to bytes for API response.
    
    Args:
        image: Image as numpy array
        format: Image format extension (e.g., ".png", ".jpg")
    
    Returns:
        Encoded image bytes, or None if encoding fails
    """
    try:
        success, encoded = cv2.imencode(format, image)
        if success:
            return encoded.tobytes()
        return None
    except Exception:
        return None
