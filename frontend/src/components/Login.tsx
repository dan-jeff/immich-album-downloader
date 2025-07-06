import React, { useState, useEffect } from 'react';
import { Container, Row, Col, Card, Form, Button, Alert, Spinner } from 'react-bootstrap';
import api from '../api';

interface LoginProps {
  onLogin: (token: string) => void;
}

const Login: React.FC<LoginProps> = ({ onLogin }) => {
  const [setupRequired, setSetupRequired] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);
  const [formData, setFormData] = useState({
    username: '',
    password: ''
  });
  const [error, setError] = useState<string>('');
  const [success, setSuccess] = useState<string>('');

  useEffect(() => {
    checkSetup();
  }, []);

  const checkSetup = async () => {
    try {
      const response = await api.checkSetup();
      setSetupRequired(response.setup_required);
    } catch (error) {
      console.error('Setup check failed:', error);
      setError('Failed to check setup status');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    try {
      if (setupRequired) {
        // Register new user and automatically log them in
        const response = await api.register(formData.username, formData.password);
        onLogin(response.access_token);
      } else {
        // Login existing user
        const response = await api.login(formData.username, formData.password);
        onLogin(response.access_token);
      }
    } catch (error: any) {
      setError(error.response?.data?.detail || error.message || 'Operation failed');
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    });
  };

  if (loading) {
    return (
      <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '100vh' }}>
        <Spinner animation="border" />
      </Container>
    );
  }

  return (
    <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '100vh' }}>
      <Row>
        <Col>
          <Card style={{ width: '400px' }}>
            <Card.Body>
              <div className="text-center mb-4">
                <h2 className="mb-2">Immich Downloader</h2>
                <p className="text-muted">
                  {setupRequired ? 'Create your account to get started' : 'Sign in to your account'}
                </p>
              </div>
              
              {error && <Alert variant="danger">{error}</Alert>}
              {success && <Alert variant="success">{success}</Alert>}

              <Form onSubmit={handleSubmit}>
                <Form.Group className="mb-3">
                  <Form.Label>Username</Form.Label>
                  <Form.Control
                    type="text"
                    name="username"
                    value={formData.username}
                    onChange={handleInputChange}
                    placeholder="Enter your username"
                    required
                  />
                </Form.Group>

                <Form.Group className="mb-3">
                  <Form.Label>Password</Form.Label>
                  <Form.Control
                    type="password"
                    name="password"
                    value={formData.password}
                    onChange={handleInputChange}
                    placeholder="Enter your password"
                    required
                  />
                </Form.Group>

                <Button variant="primary" type="submit" className="w-100">
                  {setupRequired ? 'Create Account' : 'Sign In'}
                </Button>
              </Form>

              {setupRequired && (
                <div className="mt-3 p-3 bg-light rounded">
                  <small className="text-muted">
                    <strong>Note:</strong> After creating your account, you'll need to configure your Immich server connection in the Settings page.
                  </small>
                </div>
              )}
            </Card.Body>
          </Card>
        </Col>
      </Row>
    </Container>
  );
};

export default Login;