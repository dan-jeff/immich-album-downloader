import os
import aiohttp
import asyncio

class ImmichApi:
    """Client for interacting with the Immich server API"""
    
    def __init__(self, url, api_key):
        """Initialize the API client with server URL and authentication"""
        # Normalize URL to always end with /api
        if url.endswith('/api'):
            self.base_url = url.rstrip('/')
        else:
            self.base_url = url.rstrip('/') + '/api'
        self.api_key = api_key
        
        # Session will be created when needed
        self._session = None
        self._headers = {
            "x-api-key": self.api_key,
            "Accept": "application/json"
        }
    
    async def _get_session(self):
        """Get or create aiohttp session with connection pooling"""
        if self._session is None or self._session.closed:
            timeout = aiohttp.ClientTimeout(total=30)
            connector = aiohttp.TCPConnector(limit=100, limit_per_host=30)
            self._session = aiohttp.ClientSession(
                headers=self._headers,
                timeout=timeout,
                connector=connector
            )
        return self._session
    
    async def close(self):
        """Close the aiohttp session"""
        if self._session and not self._session.closed:
            await self._session.close()

    async def validate_connection(self):
        """Test connection to Immich server and validate API key"""
        try:
            session = await self._get_session()
            async with session.get(f"{self.base_url}/server-info/ping") as response:
                if response.status == 401:
                    return False, "Failed to connect: The provided API Key is invalid."
                elif response.status != 200:
                    return False, f"Failed to connect: HTTP Error {response.status}"
                
                try:
                    data = await response.json()
                    if data.get("res") == "pong":
                        return True, "Successfully connected to Immich!"
                    else:
                        return False, "Failed to connect: Unexpected response from server."
                except aiohttp.ContentTypeError:
                    return False, "Failed to connect: The server response was not in the expected JSON format."
        except aiohttp.ClientError as err:
            return False, f"Failed to connect: Could not reach the server at the provided URL. Error: {err}"
        except Exception as err:
            return False, f"Failed to connect: Unexpected error. Error: {err}"

    async def get_albums(self):
        """Retrieve all albums from the Immich server"""
        try:
            session = await self._get_session()
            async with session.get(f"{self.base_url}/albums") as response:
                if response.status != 200:
                    return False, f"Error fetching albums: HTTP Error {response.status}"
                
                data = await response.json()
                return True, data
        except aiohttp.ClientError as err:
            return False, f"Error fetching albums: Could not reach the server. Error: {err}"
        except Exception as err:
            return False, f"Error fetching albums: Unexpected error. Error: {err}"

    async def get_photos_for_album(self, album_id):
        """Get all assets (photos/videos) for a specific album"""
        try:
            session = await self._get_session()
            async with session.get(f"{self.base_url}/albums/{album_id}") as response:
                if response.status != 200:
                    return False, f"Error fetching photos: HTTP Error {response.status}"
                
                data = await response.json()
                return True, data['assets']
        except aiohttp.ClientError as err:
            return False, f"Error fetching photos: Could not reach the server. Error: {err}"
        except Exception as err:
            return False, f"Error fetching photos: Unexpected error. Error: {err}"

    async def download_photo(self, asset_id, download_path):
        """Download a single photo to the local filesystem"""
        # Skip if file already exists
        if os.path.exists(download_path):
            return True, "File already exists, skipped"
        
        url = f"{self.base_url}/assets/{asset_id}/original"
        print(f"Attempting to download from URL: {url}")
        try:
            session = await self._get_session()
            async with session.get(url) as response:
                if response.status != 200:
                    return False, f"Error downloading photo: HTTP Error {response.status}"
                
                # Stream download to handle large files efficiently
                with open(download_path, 'wb') as f:
                    async for chunk in response.content.iter_chunked(8192):
                        f.write(chunk)
                return True, None
        except aiohttp.ClientError as err:
            return False, f"Error downloading photo: {err}"
        except Exception as err:
            return False, f"Error downloading photo: Unexpected error. {err}"
    
    async def get_photo_data(self, asset_id):
        """Retrieve photo data as bytes for in-memory processing"""
        url = f"{self.base_url}/assets/{asset_id}/original"
        try:
            session = await self._get_session()
            async with session.get(url) as response:
                if response.status != 200:
                    print(f"Error getting photo data: HTTP Error {response.status}")
                    return None
                
                return await response.read()
        except aiohttp.ClientError as err:
            print(f"Error getting photo data: {err}")
            return None
        except Exception as err:
            print(f"Error getting photo data: Unexpected error. {err}")
            return None