import React, { useEffect, useState } from 'react';
import { Container, Card, Button, Alert } from 'react-bootstrap';
import api from '../api';
import { Album } from '../types';

const Albums: React.FC = () => {
  const [albums, setAlbums] = useState<Album[]>([]);
  const [loading, setLoading] = useState(true);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'danger'; text: string } | null>(null);

  useEffect(() => {
    fetchAlbums();
  }, []);

  const fetchAlbums = async () => {
    try {
      const data = await api.getAlbums();
      setAlbums(data);
    } catch (error) {
      console.error('Error fetching albums:', error);
      setMessage({ type: 'danger', text: 'Failed to fetch albums' });
    } finally {
      setLoading(false);
    }
  };

  const handleDownload = async (albumId: string) => {
    setDownloading(albumId);
    setMessage(null);
    
    try {
      const album = albums.find(a => a.id === albumId);
      if (album) {
        await api.startDownload(albumId, album.albumName);
        setMessage({ type: 'success', text: 'Download started successfully! Check Active Tasks for progress.' });
      }
    } catch (error) {
      console.error('Error downloading album:', error);
      setMessage({ type: 'danger', text: 'Failed to start download' });
    } finally {
      setDownloading(null);
    }
  };

  if (loading) {
    return (
      <Container className="page-container">
        <div className="d-flex justify-content-center">
          <div className="spinner-border" role="status">
            <span className="visually-hidden">Loading...</span>
          </div>
        </div>
      </Container>
    );
  }

  return (
    <Container className="page-container">
      <h1 className="page-title">Albums</h1>
      
      {message && (
        <Alert variant={message.type} dismissible onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {albums.length === 0 ? (
        <div className="empty-state">
          <h3>No Albums Found</h3>
          <p>There are no albums in your Immich library.</p>
        </div>
      ) : (
        <div className="album-grid">
          {albums.map(album => {
            // Check if album is out of sync (has local assets but counts don't match)
            const isOutOfSync = (album.localAssetCount || 0) > 0 && album.localAssetCount !== album.assetCount;
            const cardClassName = `album-card ${isOutOfSync ? 'out-of-sync' : ''}`;
            
            return (
            <Card key={album.id} className={cardClassName}>
              {album.albumThumbnailAssetId ? (
                <img
                  src={api.getAlbumThumbnailUrl(album.id, album.albumThumbnailAssetId)}
                  alt={album.albumName}
                  className="album-thumbnail"
                  onError={(e) => {
                    (e.target as HTMLImageElement).style.display = 'none';
                  }}
                />
              ) : (
                <div className="album-thumbnail d-flex align-items-center justify-content-center bg-light">
                  <span className="text-muted">No thumbnail</span>
                </div>
              )}
              <div className="album-info">
                <h5 className="album-title">{album.albumName}</h5>
                <p className="album-details mb-2">
                  <strong>Immich:</strong> {album.assetCount} {album.assetCount === 1 ? 'asset' : 'assets'}
                  <br />
                  <strong>Local:</strong> {album.localAssetCount || 0} {(album.localAssetCount || 0) === 1 ? 'asset' : 'assets'}
                  {album.shared && <><br /><strong>Shared</strong></>}
                </p>
                {album.startDate && album.endDate && (
                  <p className="album-details mb-3">
                    {new Date(album.startDate).toLocaleDateString()} - {new Date(album.endDate).toLocaleDateString()}
                  </p>
                )}
                <Button
                  variant="primary"
                  size="sm"
                  className="w-100"
                  onClick={() => handleDownload(album.id)}
                  disabled={downloading === album.id}
                >
                  {downloading === album.id ? 'Downloading...' : 'Download Album'}
                </Button>
              </div>
            </Card>
            );
          })}
        </div>
      )}
    </Container>
  );
};

export default Albums;