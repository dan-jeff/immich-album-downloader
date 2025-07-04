import os
from dotenv import load_dotenv
import json

def get_config():
    """Loads configuration from .env file."""
    load_dotenv(override=True)
    immich_url = os.getenv("IMMICH_URL")
    api_key = os.getenv("IMMICH_API_KEY")
    profiles_str = os.getenv("RESIZE_PROFILES", "[]")
    try:
        profiles = json.loads(profiles_str)
    except json.JSONDecodeError:
        profiles = [] # Return empty list if JSON is invalid

    return {
        "immich_url": immich_url,
        "api_key": api_key,
        "resize_profiles": profiles
    }

def save_config(url, api_key, profiles):
    """Saves configuration to .env file."""
    with open(".env", "w") as f:
        f.write(f'IMMICH_URL="{url}"\n')
        f.write(f'IMMICH_API_KEY="{api_key}"\n')
        f.write(f'RESIZE_PROFILES=\'{json.dumps(profiles)}\'\n') # Use single quotes around the JSON string

def add_resize_profile(name, width, height, include_horizontal, include_vertical):
    """Adds a new resize profile to the configuration."""
    config = get_config()
    profiles = config.get("resize_profiles", [])
    new_profile = {
        "name": name,
        "width": width,
        "height": height,
        "include_horizontal": include_horizontal,
        "include_vertical": include_vertical
    }
    profiles.append(new_profile)
    save_config(config["immich_url"], config["api_key"], profiles)

def delete_resize_profile(profile_name):
    """Deletes a resize profile from the configuration."""
    config = get_config()
    profiles = config.get("resize_profiles", [])
    initial_len = len(profiles)
    profiles = [p for p in profiles if p["name"] != profile_name]
    if len(profiles) < initial_len:
        save_config(config["immich_url"], config["api_key"], profiles)
        return True
    return False

def update_resize_profile(old_name, new_name, width, height, include_horizontal, include_vertical):
    """Updates an existing resize profile in the configuration."""
    config = get_config()
    profiles = config.get("resize_profiles", [])
    for i, profile in enumerate(profiles):
        if profile["name"] == old_name:
            profiles[i] = {
                "name": new_name,
                "width": width,
                "height": height,
                "include_horizontal": include_horizontal,
                "include_vertical": include_vertical
            }
            save_config(config["immich_url"], config["api_key"], profiles)
            return True
    return False