import React, { useEffect, useState } from 'react';
import { Container, Card, Table, Button, Alert, Badge } from 'react-bootstrap';
import api from '../api';
import { Download } from '../types';

const AvailableDownloads: React.FC = () => {
  const [downloads, setDownloads] = useState<Download[]>([]);
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'danger'; text: string } | null>(null);

  useEffect(() => {
    fetchDownloads();
  }, []);

  const fetchDownloads = async () => {
    try {
      const data = await api.getCompletedDownloads();
      setDownloads(data);
    } catch (error) {
      console.error('Error fetching downloads:', error);
      setMessage({ type: 'danger', text: 'Failed to fetch downloads' });
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this download? This will remove the downloaded files from disk.')) {
      return;
    }

    setDeleting(id);
    try {
      await api.deleteDownload(id);
      setMessage({ type: 'success', text: 'Download deleted successfully' });
      await fetchDownloads();
    } catch (error) {
      console.error('Error deleting download:', error);
      setMessage({ type: 'danger', text: 'Failed to delete download' });
    } finally {
      setDeleting(null);
    }
  };

  const handleDownload = async (id: string, albumName: string) => {
    setDownloading(id);
    try {
      const blob = await api.downloadZip(id);
      
      // Create a download link
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${albumName}.zip`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
      
      setMessage({ type: 'success', text: 'Download started successfully' });
    } catch (error) {
      console.error('Error downloading file:', error);
      setMessage({ type: 'danger', text: 'Failed to download file' });
    } finally {
      setDownloading(null);
    }
  };

  const formatSize = (bytes: number) => {
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    if (bytes === 0) return '0 Bytes';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const getStatusBadge = (status: string) => {
    const variants: Record<string, string> = {
      'completed': 'success',
      'in_progress': 'primary',
      'failed': 'danger',
    };
    return <Badge bg={variants[status] || 'secondary'}>{status.replace('_', ' ')}</Badge>;
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
      <h1 className="page-title">Available Downloads</h1>

      {message && (
        <Alert variant={message.type} dismissible onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <Card>
        <Card.Header>
          <h5 className="mb-0">Processed Albums</h5>
        </Card.Header>
        <Card.Body>
          {downloads.length === 0 ? (
            <div className="empty-state">
              <h3>No Downloads</h3>
              <p>You haven't downloaded any albums yet. Visit the Albums page to start downloading.</p>
            </div>
          ) : (
            <>
              {/* Desktop Table View */}
              <div className="d-none d-lg-block">
                <Table hover>
                  <thead>
                    <tr>
                      <th>Album Name</th>
                      <th>Assets</th>
                      <th>Size</th>
                      <th>Status</th>
                      <th>Downloaded</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {downloads.map(download => (
                      <tr key={download.id}>
                        <td><strong>{download.album_name}</strong></td>
                        <td>
                          {download.processed_count ? download.processed_count.toLocaleString() : 'N/A'}
                          {download.profile_name && <><br /><small className="text-muted">({download.profile_name})</small></>}
                        </td>
                        <td>{formatSize(download.total_size)}</td>
                        <td>{getStatusBadge(download.status)}</td>
                        <td>{formatDate(download.created_at)}</td>
                        <td>
                          <div className="d-flex gap-2">
                            <Button
                              variant="outline-primary"
                              size="sm"
                              onClick={() => handleDownload(download.id, download.album_name)}
                              disabled={downloading === download.id}
                            >
                              {downloading === download.id ? 'Downloading...' : 'Download'}
                            </Button>
                            <Button
                              variant="outline-danger"
                              size="sm"
                              onClick={() => handleDelete(download.id)}
                              disabled={deleting === download.id}
                            >
                              {deleting === download.id ? 'Deleting...' : 'Delete'}
                            </Button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>

              {/* Mobile Card View */}
              <div className="d-lg-none">
                {downloads.map(download => (
                  <Card key={download.id} className="mb-3">
                    <Card.Body>
                      <div className="d-flex justify-content-between align-items-start mb-3">
                        <div>
                          <h6 className="mb-1">{download.album_name}</h6>
                          <div className="mb-2">{getStatusBadge(download.status)}</div>
                        </div>
                      </div>
                      
                      <div className="row mb-3">
                        <div className="col-6">
                          <small className="text-muted">Assets</small>
                          <div>{download.processed_count ? download.processed_count.toLocaleString() : 'N/A'}</div>
                          {download.profile_name && <small className="text-muted">({download.profile_name})</small>}
                        </div>
                        <div className="col-6">
                          <small className="text-muted">Size</small>
                          <div>{formatSize(download.total_size)}</div>
                        </div>
                      </div>
                      
                      <div className="mb-3">
                        <small className="text-muted">Downloaded</small>
                        <div>{formatDate(download.created_at)}</div>
                      </div>
                      
                      <div className="d-flex gap-2">
                        <Button
                          variant="outline-primary"
                          size="sm"
                          onClick={() => handleDownload(download.id, download.album_name)}
                          disabled={downloading === download.id}
                          className="flex-fill"
                        >
                          {downloading === download.id ? 'Downloading...' : 'Download'}
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(download.id)}
                          disabled={deleting === download.id}
                          className="flex-fill"
                        >
                          {deleting === download.id ? 'Deleting...' : 'Delete'}
                        </Button>
                      </div>
                    </Card.Body>
                  </Card>
                ))}
              </div>
            </>
          )}
        </Card.Body>
      </Card>

      {downloads.length > 0 && (
        <Card className="mt-4">
          <Card.Header>
            <h5 className="mb-0">Storage Summary</h5>
          </Card.Header>
          <Card.Body>
            <div className="row">
              <div className="col-md-3">
                <div className="stat-card">
                  <div className="stat-value">{downloads.length}</div>
                  <div className="stat-label">Total Downloads</div>
                </div>
              </div>
              <div className="col-md-3">
                <div className="stat-card">
                  <div className="stat-value">
                    {downloads.reduce((sum, d) => sum + (d.processed_count || 0), 0).toLocaleString()}
                  </div>
                  <div className="stat-label">Processed Assets</div>
                </div>
              </div>
              <div className="col-md-3">
                <div className="stat-card">
                  <div className="stat-value">
                    {formatSize(downloads.reduce((sum, d) => sum + d.total_size, 0))}
                  </div>
                  <div className="stat-label">Total Size</div>
                </div>
              </div>
              <div className="col-md-3">
                <div className="stat-card">
                  <div className="stat-value">
                    {downloads.filter(d => d.status === 'completed').length}
                  </div>
                  <div className="stat-label">Completed</div>
                </div>
              </div>
            </div>
          </Card.Body>
        </Card>
      )}
    </Container>
  );
};

export default AvailableDownloads;