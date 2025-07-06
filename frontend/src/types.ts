export interface Album {
  id: string;
  albumName: string;
  assetCount: number;
  localAssetCount?: number;
  shared: boolean;
  sharedUsers: any[];
  startDate?: string;
  endDate?: string;
  albumThumbnailAssetId?: string;
}

export interface Asset {
  id: string;
  type: string;
  originalFileName: string;
  fileCreatedAt: string;
  fileModifiedAt: string;
}

export interface ResizeProfile {
  id?: number;
  name: string;
  width: number;
  height: number;
  include_horizontal: boolean;
  include_vertical: boolean;
  quality?: number;
  created_at?: string;
}

export interface Task {
  id: string;
  type: string;
  status: string;
  progress: number;
  total: number;
  message?: string;
  created_at: string;
  updated_at: string;
}

export interface Download {
  id: string;
  album_id?: string;
  album_name: string;
  profile_name?: string;
  asset_count?: number;
  processed_count?: number;
  zip_size?: number;
  total_size: number;
  created_at: string;
  status: string;
}

export interface Config {
  immich_url: string;
  api_key: string;
  download_path: string;
  resized_path: string;
}