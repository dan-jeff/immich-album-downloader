import aiosqlite
import json
import hashlib
import asyncio
from datetime import datetime
from typing import List, Dict, Optional, Tuple
from pathlib import Path
import os

class AsyncDatabase:
    """Async version of database operations for the Immich downloader application"""
    
    def __init__(self, db_path: str = "immich_downloader.db"):
        self.db_path = db_path
        self._lock = asyncio.Lock()
        # Don't auto-initialize in constructor for async operations
    
    async def initialize(self):
        """Initialize the database - call this once at startup"""
        await self.init_database()
    
    async def _get_connection(self):
        """Get a database connection with optimized settings for concurrent access"""
        conn = await aiosqlite.connect(self.db_path)
        # Optimize for concurrent access
        await conn.execute('PRAGMA journal_mode=WAL')  # Write-Ahead Logging
        await conn.execute('PRAGMA synchronous=NORMAL')  # Balance safety/speed
        await conn.execute('PRAGMA temp_store=memory')  # Use memory for temp tables
        await conn.execute('PRAGMA busy_timeout=5000')  # 5 second busy timeout
        return conn
    
    async def init_database(self):
        """Initialize database schema with all required tables"""
        async with aiosqlite.connect(self.db_path) as conn:
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')
            
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS resize_profiles (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL,
                    width INTEGER NOT NULL,
                    height INTEGER NOT NULL,
                    include_horizontal BOOLEAN NOT NULL,
                    include_vertical BOOLEAN NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')
            
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS immich_albums (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    photo_count INTEGER DEFAULT 0,
                    last_synced TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ''')
            
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS downloaded_albums (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    album_id TEXT NOT NULL,
                    album_name TEXT NOT NULL,
                    photo_count INTEGER NOT NULL,
                    total_size INTEGER DEFAULT 0,
                    chunk_count INTEGER DEFAULT 0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (album_id) REFERENCES immich_albums (id)
                )
            ''')
            
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS album_chunks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    album_id INTEGER NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    chunk_data BLOB NOT NULL,
                    chunk_size INTEGER NOT NULL,
                    photo_count INTEGER NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (album_id) REFERENCES downloaded_albums (id),
                    UNIQUE(album_id, chunk_index)
                )
            ''')
            
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS resize_tasks (
                    id TEXT PRIMARY KEY,
                    downloaded_album_id INTEGER NOT NULL,
                    profile_id INTEGER NOT NULL,
                    status TEXT NOT NULL DEFAULT 'pending',
                    progress INTEGER DEFAULT 0,
                    total INTEGER DEFAULT 0,
                    current_step TEXT,
                    zip_data BLOB,
                    zip_size INTEGER DEFAULT 0,
                    processed_count INTEGER DEFAULT 0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    completed_at TIMESTAMP,
                    FOREIGN KEY (downloaded_album_id) REFERENCES downloaded_albums (id),
                    FOREIGN KEY (profile_id) REFERENCES resize_profiles (id)
                )
            ''')
            
            await conn.execute('''
                CREATE TABLE IF NOT EXISTS download_tasks (
                    id TEXT PRIMARY KEY,
                    album_id TEXT NOT NULL,
                    album_name TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'pending',
                    progress INTEGER DEFAULT 0,
                    total INTEGER DEFAULT 0,
                    current_step TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    completed_at TIMESTAMP,
                    FOREIGN KEY (album_id) REFERENCES immich_albums (id)
                )
            ''')
            
            await conn.commit()
    
    # User authentication methods
    async def create_user(self, username: str, password: str) -> bool:
        """Create a new user with hashed password"""
        password_hash = hashlib.sha256(password.encode()).hexdigest()
        try:
            async with aiosqlite.connect(self.db_path) as conn:
                await conn.execute(
                    'INSERT INTO users (username, password_hash) VALUES (?, ?)',
                    (username, password_hash)
                )
                await conn.commit()
                return True
        except aiosqlite.IntegrityError:
            return False
    
    async def verify_user(self, username: str, password: str) -> bool:
        """Verify user credentials against stored hash"""
        password_hash = hashlib.sha256(password.encode()).hexdigest()
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                'SELECT id FROM users WHERE username = ? AND password_hash = ?',
                (username, password_hash)
            )
            result = await cursor.fetchone()
            return result is not None
    
    async def user_exists(self) -> bool:
        """Check if any user has been registered"""
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute('SELECT COUNT(*) FROM users')
            result = await cursor.fetchone()
            return result[0] > 0
    
    # Profile management methods
    async def add_resize_profile(self, name: str, width: int, height: int, 
                          include_horizontal: bool, include_vertical: bool) -> int:
        """Add a new resize profile"""
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                '''INSERT INTO resize_profiles 
                   (name, width, height, include_horizontal, include_vertical) 
                   VALUES (?, ?, ?, ?, ?)''',
                (name, width, height, include_horizontal, include_vertical)
            )
            await conn.commit()
            return cursor.lastrowid
    
    async def get_resize_profiles(self) -> List[Dict]:
        async with aiosqlite.connect(self.db_path) as conn:
            conn.row_factory = aiosqlite.Row
            cursor = await conn.execute(
                'SELECT * FROM resize_profiles ORDER BY created_at DESC'
            )
            rows = await cursor.fetchall()
            return [dict(row) for row in rows]
    
    async def update_resize_profile(self, profile_id: int, name: str, width: int, height: int,
                             include_horizontal: bool, include_vertical: bool) -> bool:
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                '''UPDATE resize_profiles 
                   SET name = ?, width = ?, height = ?, 
                       include_horizontal = ?, include_vertical = ?
                   WHERE id = ?''',
                (name, width, height, include_horizontal, include_vertical, profile_id)
            )
            await conn.commit()
            return cursor.rowcount > 0
    
    async def delete_resize_profile(self, profile_id: int) -> bool:
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute('DELETE FROM resize_profiles WHERE id = ?', (profile_id,))
            await conn.commit()
            return cursor.rowcount > 0
    
    # Album synchronization and management
    async def sync_immich_albums(self, albums: List[Dict]) -> None:
        """Sync album data from Immich server to local database"""
        async with aiosqlite.connect(self.db_path) as conn:
            for album in albums:
                # Handle different API response formats for asset count
                asset_count = (album.get('assetCount') or 
                              album.get('assetCountTotal') or 
                              album.get('assets', []) if isinstance(album.get('assets'), list) else 0 or
                              len(album.get('assets', [])) if isinstance(album.get('assets'), list) else 0)
                
                await conn.execute(
                    '''INSERT OR REPLACE INTO immich_albums (id, name, photo_count, last_synced)
                       VALUES (?, ?, ?, CURRENT_TIMESTAMP)''',
                    (album['id'], album['albumName'], asset_count)
                )
            await conn.commit()
    
    async def get_immich_stats(self) -> Dict:
        """Get statistics about albums and downloads"""
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute('SELECT COUNT(*) as album_count, SUM(photo_count) as total_photos FROM immich_albums')
            result = await cursor.fetchone()
            
            cursor = await conn.execute('SELECT COUNT(*) as download_count FROM downloaded_albums')
            download_result = await cursor.fetchone()
            download_count = download_result[0]
            
            return {
                'album_count': result[0] or 0,
                'image_count': result[1] or 0,
                'download_count': download_count
            }
    
    async def save_downloaded_album_chunked(self, album_id: str, album_name: str, 
                                     photo_count: int, chunks: List[bytes]) -> int:
        """Save downloaded album in chunks for better memory management and database performance"""
        try:
            async with self._get_connection() as conn:
                # Create album record
                cursor = await conn.execute(
                    '''INSERT INTO downloaded_albums 
                       (album_id, album_name, photo_count, total_size, chunk_count)
                       VALUES (?, ?, ?, ?, ?)''',
                    (album_id, album_name, photo_count, sum(len(chunk) for chunk in chunks), len(chunks))
                )
                album_db_id = cursor.lastrowid
                
                # Save chunks in separate transactions to avoid long locks
                await conn.commit()
                
                # Save each chunk in separate transaction for better concurrency
                for i, chunk_data in enumerate(chunks):
                    async with self._get_connection() as chunk_conn:
                        await chunk_conn.execute(
                            '''INSERT INTO album_chunks 
                               (album_id, chunk_index, chunk_data, chunk_size, photo_count)
                               VALUES (?, ?, ?, ?, ?)''',
                            (album_db_id, i, chunk_data, len(chunk_data), 
                             photo_count // len(chunks) + (1 if i < photo_count % len(chunks) else 0))
                        )
                        await chunk_conn.commit()
                
                return album_db_id
        except aiosqlite.OperationalError as e:
            print(f"Database error in save_downloaded_album_chunked: {e}")
            return -1
    
    async def get_downloaded_album_images(self, downloaded_album_id: int) -> Optional[bytes]:
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                'SELECT images_data FROM downloaded_albums WHERE id = ?',
                (downloaded_album_id,)
            )
            result = await cursor.fetchone()
            return result[0] if result else None
    
    async def get_downloaded_albums(self) -> List[Dict]:
        async with aiosqlite.connect(self.db_path) as conn:
            conn.row_factory = aiosqlite.Row
            cursor = await conn.execute(
                '''SELECT id, album_id, album_name, photo_count, created_at 
                   FROM downloaded_albums ORDER BY created_at DESC'''
            )
            rows = await cursor.fetchall()
            return [dict(row) for row in rows]
    
    async def get_local_asset_counts(self) -> Dict[str, int]:
        """Get count of locally downloaded photos for each album"""
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                '''SELECT album_id, photo_count FROM downloaded_albums'''
            )
            rows = await cursor.fetchall()
            return {row[0]: row[1] for row in rows}
    
    # Task management methods
    async def create_download_task(self, task_id: str, album_id: str, album_name: str, total: int) -> None:
        """Create a new download task record"""
        async with aiosqlite.connect(self.db_path) as conn:
            await conn.execute(
                '''INSERT INTO download_tasks 
                   (id, album_id, album_name, total, status, current_step)
                   VALUES (?, ?, ?, ?, 'pending', 'Initializing...')''',
                (task_id, album_id, album_name, total)
            )
            await conn.commit()
    
    async def update_download_task(self, task_id: str, status: str = None, 
                           progress: int = None, current_step: str = None) -> None:
        """Update download task progress and status"""
        updates = []
        params = []
        
        if status:
            updates.append('status = ?')
            params.append(status)
            if status == 'completed':
                updates.append('completed_at = CURRENT_TIMESTAMP')
        
        if progress is not None:
            updates.append('progress = ?')
            params.append(progress)
        
        if current_step:
            updates.append('current_step = ?')
            params.append(current_step)
        
        if updates:
            params.append(task_id)
            try:
                async with self._get_connection() as conn:
                    await conn.execute(
                        f'UPDATE download_tasks SET {", ".join(updates)} WHERE id = ?',
                        params
                    )
                    await conn.commit()
            except aiosqlite.OperationalError as e:
                print(f"Database error in update_download_task: {e}")
                # Continue operation even if database update fails
    
    async def update_download_task_total(self, task_id: str, total: int) -> None:
        """Update the total count for a download task"""
        try:
            async with self._get_connection() as conn:
                await conn.execute(
                    'UPDATE download_tasks SET total = ? WHERE id = ?',
                    (total, task_id)
                )
                await conn.commit()
        except aiosqlite.OperationalError as e:
            print(f"Database error in update_download_task_total: {e}")
    
    async def create_resize_task(self, task_id: str, downloaded_album_id: int, 
                          profile_id: int, total: int) -> None:
        """Create a new resize task record"""
        async with aiosqlite.connect(self.db_path) as conn:
            await conn.execute(
                '''INSERT INTO resize_tasks 
                   (id, downloaded_album_id, profile_id, total, status, current_step)
                   VALUES (?, ?, ?, ?, 'pending', 'Initializing...')''',
                (task_id, downloaded_album_id, profile_id, total)
            )
            await conn.commit()
    
    async def update_resize_task(self, task_id: str, status: str = None, progress: int = None,
                          current_step: str = None, zip_data: bytes = None, 
                          zip_size: int = None, processed_count: int = None, total: int = None) -> None:
        """Update resize task with progress, status, and result data"""
        updates = []
        params = []
        
        if status:
            updates.append('status = ?')
            params.append(status)
            if status == 'completed':
                updates.append('completed_at = CURRENT_TIMESTAMP')
        
        if progress is not None:
            updates.append('progress = ?')
            params.append(progress)
        
        if current_step:
            updates.append('current_step = ?')
            params.append(current_step)
        
        if zip_data:
            updates.append('zip_data = ?')
            params.append(zip_data)
        
        if zip_size is not None:
            updates.append('zip_size = ?')
            params.append(zip_size)
        
        if processed_count is not None:
            updates.append('processed_count = ?')
            params.append(processed_count)
        
        if total is not None:
            updates.append('total = ?')
            params.append(total)
        
        if updates:
            params.append(task_id)
            async with aiosqlite.connect(self.db_path) as conn:
                await conn.execute(
                    f'UPDATE resize_tasks SET {", ".join(updates)} WHERE id = ?',
                    params
                )
                await conn.commit()
    
    async def get_active_tasks(self) -> List[Dict]:
        """Get all active and recently completed tasks for display"""
        try:
            async with self._get_connection() as conn:
                conn.row_factory = aiosqlite.Row
                
                cursor1 = await conn.execute(
                    '''SELECT 'download' as task_type, id, album_name as name, 
                              status, progress, total, current_step, created_at, completed_at
                       FROM download_tasks 
                       ORDER BY created_at DESC
                       LIMIT 50'''
                )
                download_tasks = await cursor1.fetchall()
                
                cursor2 = await conn.execute(
                    '''SELECT 'resize' as task_type, rt.id, 
                              da.album_name || ' (' || rp.name || ')' as name,
                              rt.status, rt.progress, rt.total, rt.current_step, rt.created_at, rt.completed_at
                       FROM resize_tasks rt
                       JOIN downloaded_albums da ON rt.downloaded_album_id = da.id
                       JOIN resize_profiles rp ON rt.profile_id = rp.id
                       ORDER BY rt.created_at DESC
                       LIMIT 50'''
                )
                resize_tasks = await cursor2.fetchall()
                
                return [dict(row) for row in list(download_tasks) + list(resize_tasks)]
        except aiosqlite.OperationalError as e:
            print(f"Database error in get_active_tasks: {e}")
            return []
    
    async def get_completed_downloads(self) -> List[Dict]:
        """Get completed resize tasks that have downloadable ZIP files"""
        async with aiosqlite.connect(self.db_path) as conn:
            conn.row_factory = aiosqlite.Row
            cursor = await conn.execute(
                '''SELECT rt.id, da.album_name, rp.name as profile_name,
                          rt.zip_size, rt.processed_count, rt.created_at
                   FROM resize_tasks rt
                   JOIN downloaded_albums da ON rt.downloaded_album_id = da.id
                   JOIN resize_profiles rp ON rt.profile_id = rp.id
                   WHERE rt.status = 'completed' AND rt.zip_data IS NOT NULL
                   ORDER BY rt.created_at DESC'''
            )
            rows = await cursor.fetchall()
            return [dict(row) for row in rows]
    
    async def get_resize_task_zip(self, task_id: str) -> Optional[bytes]:
        """Retrieve ZIP file data for a completed resize task"""
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                'SELECT zip_data FROM resize_tasks WHERE id = ? AND status = "completed"',
                (task_id,)
            )
            result = await cursor.fetchone()
            return result[0] if result else None
    
    async def delete_completed_download(self, task_id: str) -> bool:
        """Delete a completed download/resize task and its data"""
        async with aiosqlite.connect(self.db_path) as conn:
            cursor = await conn.execute(
                'DELETE FROM resize_tasks WHERE id = ? AND status = "completed"',
                (task_id,)
            )
            await conn.commit()
            return cursor.rowcount > 0

# Global async database instance
async_db = AsyncDatabase()