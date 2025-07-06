import React, { useEffect, useState } from 'react';
import { Container, Card, Button, Alert, ButtonGroup } from 'react-bootstrap';
import api from '../api';
import { Album } from '../types';
import ThumbnailImage from './ThumbnailImage';

const Albums: React.FC = () => {
  const [albums, setAlbums] = useState<Album[]>([]);
  const [loading, setLoading] = useState(true);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [removing, setRemoving] = useState<string | null>(null);
  const [cleaningUp, setCleaningUp] = useState(false);
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

  const handleRemoveLocal = async (albumId: string) => {
    setRemoving(albumId);
    setMessage(null);
    
    try {
      const album = albums.find(a => a.id === albumId);
      if (album && window.confirm(`Are you sure you want to remove all local assets for "${album.albumName}"? This will delete ${album.localAssetCount} local files permanently.`)) {
        const result = await api.removeLocalAssets(albumId);
        if (result.success) {
          setMessage({ type: 'success', text: result.message });
          // Refresh albums to update local counts
          await fetchAlbums();
        } else {
          setMessage({ type: 'danger', text: 'Failed to remove local assets' });
        }
      }
    } catch (error) {
      console.error('Error removing local assets:', error);
      setMessage({ type: 'danger', text: 'Failed to remove local assets' });
    } finally {
      setRemoving(null);
    }
  };

  const handleCleanupOrphans = async () => {
    setCleaningUp(true);
    setMessage(null);
    
    try {
      if (window.confirm('Are you sure you want to cleanup orphaned assets? This will remove local assets that no longer exist on the Immich server.')) {
        const result = await api.cleanupOrphanedAssets();
        if (result.success) {
          const { albumsProcessed, orphansFound, orphansRemoved } = result.summary;
          setMessage({ 
            type: 'success', 
            text: `Cleanup completed: ${orphansRemoved} orphaned assets removed from ${albumsProcessed} albums (${orphansFound} orphans found)` 
          });
          // Refresh albums to update local counts
          await fetchAlbums();
        } else {
          setMessage({ type: 'danger', text: 'Failed to cleanup orphaned assets' });
        }
      }
    } catch (error) {
      console.error('Error cleaning up orphaned assets:', error);
      setMessage({ type: 'danger', text: 'Failed to cleanup orphaned assets' });
    } finally {
      setCleaningUp(false);
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
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h1 className="page-title mb-0">Albums</h1>
        <Button
          variant="outline-warning"
          size="sm"
          onClick={handleCleanupOrphans}
          disabled={cleaningUp || downloading !== null || removing !== null}
        >
          {cleaningUp ? 'Cleaning up...' : 'Cleanup Orphaned Assets'}
        </Button>
      </div>
      
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
          {albums.sort((a, b) => a.albumName.localeCompare(b.albumName)).map(album => {
            // Check if album is out of sync (has local assets but counts don't match)
            const isOutOfSync = (album.localAssetCount || 0) > 0 && album.localAssetCount !== album.assetCount;
            const cardClassName = `album-card ${isOutOfSync ? 'out-of-sync' : ''}`;
            
            return (
            <Card key={album.id} className={cardClassName}>
              {album.albumThumbnailAssetId ? (
                <ThumbnailImage
                  assetId={album.albumThumbnailAssetId}
                  alt={album.albumName}
                  className="album-thumbnail"
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
                <div className="d-grid gap-2">
                  <Button
                    variant="primary"
                    size="sm"
                    onClick={() => handleDownload(album.id)}
                    disabled={downloading === album.id || removing === album.id}
                  >
                    {downloading === album.id ? 'Downloading...' : 'Download Album'}
                  </Button>
                  {(album.localAssetCount || 0) > 0 && (
                    <Button
                      variant="outline-danger"
                      size="sm"
                      onClick={() => handleRemoveLocal(album.id)}
                      disabled={downloading === album.id || removing === album.id}
                    >
                      {removing === album.id ? 'Removing...' : `Remove Local Assets (${album.localAssetCount})`}
                    </Button>
                  )}
                </div>
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