import os
import asyncio
import aiofiles
from PIL import Image, ImageOps
from pathlib import Path

class ImageProcessor:
    """Handles image resizing and processing according to configured profiles"""
    
    def __init__(self, album_name, profiles):
        """Initialize processor with album name and resize profiles"""
        self.album_name = album_name
        self.profiles = profiles

    async def resize_image(self, image_path):
        """Resize image according to all configured profiles with letterboxing"""
        try:
            # Run CPU-intensive image processing in a thread to avoid blocking
            return await asyncio.to_thread(self._resize_image_sync, image_path)
        except Exception as e:
            return False, f"Failed to resize {os.path.basename(image_path)}. Error: {e}"
    
    def _resize_image_sync(self, image_path):
        """Synchronous image processing called from async wrapper"""
        try:
            img = Image.open(image_path)
            
            # Correct image orientation from EXIF data
            img = ImageOps.exif_transpose(img)

            original_width, original_height = img.size
            is_horizontal = original_width >= original_height

            # Process image for each configured profile
            for profile in self.profiles:
                profile_name = profile["name"]
                target_width = profile["width"]
                target_height = profile["height"]
                include_horizontal = profile["include_horizontal"]
                include_vertical = profile["include_vertical"]

                # Skip if image orientation doesn't match profile settings
                if (is_horizontal and not include_horizontal) or (not is_horizontal and not include_vertical):
                    continue

                # Create output directory for this profile
                output_dir_name = f"{self.album_name}_{profile_name}"
                output_dir = os.path.join("resized", output_dir_name)
                os.makedirs(output_dir, exist_ok=True)

                # Calculate aspect ratios for letterboxing
                original_aspect = original_width / original_height
                target_aspect = target_width / target_height

                if original_aspect > target_aspect:
                    # Image is wider - fit to target width
                    new_width = target_width
                    new_height = int(new_width / original_aspect)
                else:
                    # Image is taller - fit to target height
                    new_height = target_height
                    new_width = int(new_height * original_aspect)

                # Resize image maintaining aspect ratio
                resized_img = img.resize((new_width, new_height), Image.LANCZOS)

                # Create black canvas with target dimensions
                new_img = Image.new("RGB", (target_width, target_height), (0, 0, 0))

                # Center the resized image on the canvas
                paste_x = (target_width - new_width) // 2
                paste_y = (target_height - new_height) // 2

                # Apply letterboxing by pasting on black background
                new_img.paste(resized_img, (paste_x, paste_y))

                filename = os.path.basename(image_path)
                output_path = os.path.join(output_dir, filename)
                
                # Skip processing if output already exists
                if os.path.exists(output_path):
                    continue
                
                # Save as JPEG with default quality
                new_img.save(output_path, "JPEG")
            return True, None
        except Exception as e:
            return False, f"Failed to resize {os.path.basename(image_path)}. Error: {e}"
