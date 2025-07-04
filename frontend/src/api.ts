import axios, { AxiosInstance } from 'axios';
import { Album, Asset, ResizeProfile, Task, Download } from './types';

class API {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: process.env.REACT_APP_API_URL || 'http://192.168.68.21:5000/api',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Add request interceptor to ensure token is always included
    this.client.interceptors.request.use((config) => {
      const token = localStorage.getItem('token');
      if (token && !config.headers['Authorization']) {
        config.headers['Authorization'] = `Bearer ${token}`;
      }
      return config;
    });

    // Add response interceptor to handle 401 errors
    this.client.interceptors.response.use(
      (response) => response,
      (error) => {
        if (error.response?.status === 401) {
          localStorage.removeItem('token');
          window.location.href = '/login';
        }
        return Promise.reject(error);
      }
    );
  }

  public setAuthToken(token: string): void {
    if (token) {
      this.client.defaults.headers.common['Authorization'] = `Bearer ${token}`;
    } else {
      delete this.client.defaults.headers.common['Authorization'];
    }
  }

  // Authentication endpoints
  public async checkSetup(): Promise<{ setup_required: boolean }> {
    const response = await this.client.get('/auth/check-setup');
    return response.data;
  }

  public async register(username: string, password: string): Promise<{ message: string }> {
    const response = await this.client.post('/auth/register', { username, password });
    return response.data;
  }

  public async login(username: string, password: string): Promise<{ access_token: string; token_type: string }> {
    const response = await this.client.post('/auth/login', { username, password });
    return response.data;
  }

  // Configuration endpoints
  public async getConfig(): Promise<{ immich_url: string; api_key: string; resize_profiles: ResizeProfile[] }> {
    const response = await this.client.get('/config');
    return response.data;
  }

  public async saveConfig(immich_url: string, api_key: string): Promise<{ success: boolean }> {
    const response = await this.client.post('/config', { immich_url, api_key });
    return response.data;
  }

  public async testConnection(immich_url: string, api_key: string): Promise<{ success: boolean; message: string }> {
    const response = await this.client.post('/config/test', { immich_url, api_key });
    return response.data;
  }

  // Album endpoints
  public async getAlbums(): Promise<Album[]> {
    const response = await this.client.get('/albums');
    return response.data;
  }

  public async getDownloadedAlbums(): Promise<any[]> {
    const response = await this.client.get('/downloaded-albums');
    return response.data;
  }

  // Statistics
  public async getStats(): Promise<{ album_count: number; image_count: number; download_count: number }> {
    const response = await this.client.get('/stats');
    return response.data;
  }

  // Profile management
  public async getProfiles(): Promise<ResizeProfile[]> {
    const config = await this.getConfig();
    return config.resize_profiles;
  }

  public async createProfile(profile: Omit<ResizeProfile, 'id'>): Promise<{ success: boolean; id: number }> {
    const profileData = {
      Name: profile.name,
      Width: profile.width,
      Height: profile.height,
      IncludeHorizontal: profile.include_horizontal,
      IncludeVertical: profile.include_vertical
    };
    const response = await this.client.post('/profiles', profileData);
    return response.data;
  }

  public async updateProfile(profileId: number, profile: Omit<ResizeProfile, 'id'>): Promise<{ success: boolean }> {
    const profileData = {
      Name: profile.name,
      Width: profile.width,
      Height: profile.height,
      IncludeHorizontal: profile.include_horizontal,
      IncludeVertical: profile.include_vertical
    };
    const response = await this.client.put(`/profiles/${profileId}`, profileData);
    return response.data;
  }

  public async deleteProfile(profileId: number): Promise<{ success: boolean }> {
    const response = await this.client.delete(`/profiles/${profileId}`);
    return response.data;
  }

  // Task management
  public async startDownload(albumId: string, albumName: string): Promise<{ task_id: string }> {
    const response = await this.client.post('/download', { AlbumId: albumId, AlbumName: albumName });
    return response.data;
  }

  public async startResize(downloadedAlbumId: number, profileId: number): Promise<{ task_id: string }> {
    const response = await this.client.post('/resize', { DownloadedAlbumId: downloadedAlbumId, ProfileId: profileId });
    return response.data;
  }

  public async getActiveTasks(): Promise<Task[]> {
    const response = await this.client.get('/tasks');
    return response.data;
  }

  // Downloads
  public async getCompletedDownloads(): Promise<Download[]> {
    const response = await this.client.get('/downloads');
    return response.data;
  }

  public async downloadZip(taskId: string): Promise<Blob> {
    const response = await this.client.get(`/downloads/${taskId}`, {
      responseType: 'blob'
    });
    return response.data;
  }

  public async deleteDownload(taskId: string): Promise<{ success: boolean }> {
    const response = await this.client.delete(`/downloads/${taskId}`);
    return response.data;
  }

  public async deleteTask(taskId: string): Promise<{ success: boolean }> {
    const response = await this.client.delete(`/tasks/${taskId}`);
    return response.data;
  }

  // Thumbnail helper
  public getAlbumThumbnailUrl(albumId: string, assetId: string): string {
    return `/api/proxy/thumbnail/${assetId}`;
  }

  public getAuthHeaders(): Record<string, string> {
    const auth = this.client.defaults.headers.common['Authorization'];
    return auth ? { 'Authorization': auth as string } : {};
  }
}

// Export singleton instance
export default new API();