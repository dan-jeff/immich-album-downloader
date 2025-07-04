import React, { useEffect, useState } from 'react';
import { Container, Card, Form, Button, Alert, Row, Col } from 'react-bootstrap';
import api from '../api';
import { Album, ResizeProfile } from '../types';

const Resizer: React.FC = () => {
  const [albums, setAlbums] = useState<Album[]>([]);
  const [profiles, setProfiles] = useState<ResizeProfile[]>([]);
  const [selectedAlbum, setSelectedAlbum] = useState('');
  const [selectedProfile, setSelectedProfile] = useState('');
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'danger'; text: string } | null>(null);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [albumsData, profilesData] = await Promise.all([
        api.getDownloadedAlbums(),
        api.getProfiles(),
      ]);
      setAlbums(albumsData);
      setProfiles(profilesData);
    } catch (error) {
      console.error('Error fetching data:', error);
      setMessage({ type: 'danger', text: 'Failed to fetch data' });
    } finally {
      setLoading(false);
    }
  };

  const handleResize = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedAlbum || !selectedProfile) {
      setMessage({ type: 'danger', text: 'Please select both an album and a profile' });
      return;
    }

    setProcessing(true);
    setMessage(null);

    try {
      await api.startResize(parseInt(selectedAlbum), parseInt(selectedProfile));
      setMessage({ type: 'success', text: 'Resize task started successfully! Check Active Tasks for progress.' });
      setSelectedAlbum('');
      setSelectedProfile('');
    } catch (error) {
      console.error('Error starting resize:', error);
      setMessage({ type: 'danger', text: 'Failed to start resize task' });
    } finally {
      setProcessing(false);
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
      <h1 className="page-title">Image Resizer</h1>

      {message && (
        <Alert variant={message.type} dismissible onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <Card>
        <Card.Header>
          <h5 className="mb-0">Resize Images</h5>
        </Card.Header>
        <Card.Body>
          <Form onSubmit={handleResize}>
            <Row>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Select Album</Form.Label>
                  <Form.Select
                    value={selectedAlbum}
                    onChange={(e) => setSelectedAlbum(e.target.value)}
                    required
                  >
                    <option value="">Choose an album...</option>
                    {albums.map(album => (
                      <option key={album.id} value={album.id}>
                        {album.albumName} ({album.localAssetCount || 0} local assets)
                      </option>
                    ))}
                  </Form.Select>
                </Form.Group>
              </Col>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Select Resize Profile</Form.Label>
                  <Form.Select
                    value={selectedProfile}
                    onChange={(e) => setSelectedProfile(e.target.value)}
                    required
                  >
                    <option value="">Choose a profile...</option>
                    {profiles.map(profile => (
                      <option key={profile.id} value={profile.id}>
                        {profile.name} ({profile.width}x{profile.height})
                      </option>
                    ))}
                  </Form.Select>
                </Form.Group>
              </Col>
            </Row>

            {selectedProfile && (
              <Card className="mb-3 bg-light">
                <Card.Body>
                  <h6>Profile Details</h6>
                  {(() => {
                    const profile = profiles.find(p => p.id === parseInt(selectedProfile));
                    if (!profile) return null;
                    return (
                      <div>
                        <p className="mb-1"><strong>Name:</strong> {profile.name}</p>
                        <p className="mb-1"><strong>Max Dimensions:</strong> {profile.width} x {profile.height}px</p>
                        <p className="mb-1">
                          <strong>Orientation Filter:</strong>{' '}
                          {profile.include_horizontal && profile.include_vertical
                            ? 'All images'
                            : profile.include_horizontal
                            ? 'Horizontal only'
                            : profile.include_vertical
                            ? 'Vertical only'
                            : 'None (no images will be processed)'}
                        </p>
                      </div>
                    );
                  })()}
                </Card.Body>
              </Card>
            )}

            <div className="d-flex gap-2">
              <Button
                variant="primary"
                type="submit"
                disabled={processing || !selectedAlbum || !selectedProfile}
              >
                {processing ? 'Starting Resize...' : 'Start Resize'}
              </Button>
            </div>
          </Form>
        </Card.Body>
      </Card>

      {profiles.length === 0 && (
        <Alert variant="info" className="mt-3">
          No resize profiles found. <a href="/profiles">Create a profile</a> to get started.
        </Alert>
      )}

      {albums.length === 0 && (
        <Alert variant="info" className="mt-3">
          No albums found. Make sure you have albums in your Immich library.
        </Alert>
      )}
    </Container>
  );
};

export default Resizer;