import React, { useEffect, useState } from 'react';
import { Container, Card, Table, Button, Modal, Form, Alert, Row, Col } from 'react-bootstrap';
import api from '../api';
import { ResizeProfile } from '../types';

const ProfileManagement: React.FC = () => {
  const [profiles, setProfiles] = useState<ResizeProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingProfile, setEditingProfile] = useState<ResizeProfile | null>(null);
  const [formData, setFormData] = useState({
    name: '',
    width: 1920,
    height: 1080,
    include_horizontal: true,
    include_vertical: true,
  });
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'danger'; text: string } | null>(null);

  useEffect(() => {
    fetchProfiles();
  }, []);

  const fetchProfiles = async () => {
    try {
      const data = await api.getProfiles();
      setProfiles(data);
    } catch (error) {
      console.error('Error fetching profiles:', error);
      setMessage({ type: 'danger', text: 'Failed to fetch profiles' });
    } finally {
      setLoading(false);
    }
  };

  const handleNewProfile = () => {
    setEditingProfile(null);
    setFormData({
      name: '',
      width: 1920,
      height: 1080,
      include_horizontal: true,
      include_vertical: true,
    });
    setShowModal(true);
  };

  const handleEditProfile = (profile: ResizeProfile) => {
    setEditingProfile(profile);
    setFormData({
      name: profile.name,
      width: profile.width,
      height: profile.height,
      include_horizontal: profile.include_horizontal,
      include_vertical: profile.include_vertical,
    });
    setShowModal(true);
  };

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setMessage(null);

    try {
      if (editingProfile && editingProfile.id) {
        await api.updateProfile(editingProfile.id, formData);
        setMessage({ type: 'success', text: 'Profile updated successfully' });
      } else {
        await api.createProfile(formData);
        setMessage({ type: 'success', text: 'Profile created successfully' });
      }
      setShowModal(false);
      await fetchProfiles();
    } catch (error) {
      console.error('Error saving profile:', error);
      setMessage({ type: 'danger', text: 'Failed to save profile' });
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteProfile = async (id: number) => {
    if (!window.confirm('Are you sure you want to delete this profile?')) {
      return;
    }

    try {
      await api.deleteProfile(id);
      setMessage({ type: 'success', text: 'Profile deleted successfully' });
      await fetchProfiles();
    } catch (error) {
      console.error('Error deleting profile:', error);
      setMessage({ type: 'danger', text: 'Failed to delete profile' });
    }
  };

  const handleInputChange = (field: string, value: any) => {
    setFormData(prev => ({ ...prev, [field]: value }));
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
        <h1 className="page-title mb-0">Profile Management</h1>
        <Button variant="primary" onClick={handleNewProfile}>
          New Profile
        </Button>
      </div>

      {message && (
        <Alert variant={message.type} dismissible onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <Card>
        <Card.Header>
          <h5 className="mb-0">Resize Profiles</h5>
        </Card.Header>
        <Card.Body>
          {profiles.length === 0 ? (
            <div className="empty-state">
              <h3>No Profiles</h3>
              <p>Create your first resize profile to get started.</p>
            </div>
          ) : (
            <>
              {/* Desktop Table View */}
              <div className="d-none d-lg-block">
                <Table hover>
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Dimensions</th>
                      <th>Orientation</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {profiles.map(profile => (
                      <tr key={profile.id}>
                        <td><strong>{profile.name}</strong></td>
                        <td>{profile.width} x {profile.height}px</td>
                        <td>
                          {profile.include_horizontal && profile.include_vertical
                            ? 'All'
                            : profile.include_horizontal
                            ? 'Horizontal'
                            : profile.include_vertical
                            ? 'Vertical'
                            : 'None'}
                        </td>
                        <td>
                          <div className="d-flex gap-2">
                            <Button
                              variant="outline-primary"
                              size="sm"
                              onClick={() => handleEditProfile(profile)}
                            >
                              Edit
                            </Button>
                            <Button
                              variant="outline-danger"
                              size="sm"
                              onClick={() => profile.id && handleDeleteProfile(profile.id)}
                            >
                              Delete
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
                {profiles.map(profile => (
                  <Card key={profile.id} className="mb-3">
                    <Card.Body>
                      <div className="d-flex justify-content-between align-items-start mb-3">
                        <h6 className="mb-0">{profile.name}</h6>
                      </div>
                      
                      <div className="row mb-3">
                        <div className="col-6">
                          <small className="text-muted">Dimensions</small>
                          <div>{profile.width} x {profile.height}px</div>
                        </div>
                        <div className="col-6">
                          <small className="text-muted">Orientation</small>
                          <div>
                            {profile.include_horizontal && profile.include_vertical
                              ? 'All'
                              : profile.include_horizontal
                              ? 'Horizontal'
                              : profile.include_vertical
                              ? 'Vertical'
                              : 'None'}
                          </div>
                        </div>
                      </div>
                      
                      <div className="d-flex gap-2">
                        <Button
                          variant="outline-primary"
                          size="sm"
                          onClick={() => handleEditProfile(profile)}
                          className="flex-fill"
                        >
                          Edit
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => profile.id && handleDeleteProfile(profile.id)}
                          className="flex-fill"
                        >
                          Delete
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

      <Modal show={showModal} onHide={() => setShowModal(false)} size="lg">
        <Modal.Header closeButton>
          <Modal.Title>
            {editingProfile ? 'Edit Profile' : 'New Profile'}
          </Modal.Title>
        </Modal.Header>
        <Form onSubmit={handleSaveProfile}>
          <Modal.Body>
            <Row>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Profile Name</Form.Label>
                  <Form.Control
                    type="text"
                    value={formData.name}
                    onChange={(e) => handleInputChange('name', e.target.value)}
                    placeholder="Enter profile name"
                    required
                  />
                </Form.Group>
              </Col>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Width (px)</Form.Label>
                  <Form.Control
                    type="number"
                    min="1"
                    value={formData.width}
                    onChange={(e) => handleInputChange('width', parseInt(e.target.value))}
                    required
                  />
                </Form.Group>
              </Col>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Height (px)</Form.Label>
                  <Form.Control
                    type="number"
                    min="1"
                    value={formData.height}
                    onChange={(e) => handleInputChange('height', parseInt(e.target.value))}
                    required
                  />
                </Form.Group>
              </Col>
            </Row>

            <Form.Group className="mb-3">
              <Form.Label>Orientation Filter</Form.Label>
              <div>
                <Form.Check
                  type="checkbox"
                  id="include_horizontal"
                  label="Include Horizontal Images"
                  checked={formData.include_horizontal}
                  onChange={(e) => handleInputChange('include_horizontal', e.target.checked)}
                />
                <Form.Check
                  type="checkbox"
                  id="include_vertical"
                  label="Include Vertical Images"
                  checked={formData.include_vertical}
                  onChange={(e) => handleInputChange('include_vertical', e.target.checked)}
                />
              </div>
              <Form.Text className="text-muted">
                Select which image orientations to include in the resize operation.
              </Form.Text>
            </Form.Group>

            {!formData.include_horizontal && !formData.include_vertical && (
              <Alert variant="warning">
                Warning: No orientations selected. This profile will not process any images.
              </Alert>
            )}
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={() => setShowModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" type="submit" disabled={saving}>
              {saving ? 'Saving...' : 'Save Profile'}
            </Button>
          </Modal.Footer>
        </Form>
      </Modal>
    </Container>
  );
};

export default ProfileManagement;