import React, { useState, useEffect } from 'react';
import { Container, Card, Form, Button, Alert, Row, Col } from 'react-bootstrap';
import api from '../api';
import { Config } from '../types';

const Configuration: React.FC = () => {
  const [config, setConfig] = useState<Config>({
    immich_url: '',
    api_key: '',
    download_path: 'downloads',
    resized_path: 'resized',
  });
  const [loading, setLoading] = useState(false);
  const [testing, setTesting] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'danger' | 'info'; text: string } | null>(null);

  useEffect(() => {
    loadConfig();
  }, []);

  const loadConfig = async () => {
    try {
      const currentConfig = await api.getConfig();
      setConfig({
        immich_url: currentConfig.immich_url,
        api_key: currentConfig.api_key,
        download_path: 'downloads',
        resized_path: 'resized'
      });
    } catch (error) {
      console.error('Failed to load config:', error);
    }
  };

  const handleInputChange = (field: keyof Config, value: string) => {
    setConfig(prev => ({ ...prev, [field]: value }));
  };

  const handleTestConnection = async () => {
    setTesting(true);
    setMessage(null);

    try {
      const result = await api.testConnection(config.immich_url, config.api_key);
      
      if (result.success) {
        setMessage({ type: 'success', text: 'Connection successful!' });
      } else {
        setMessage({ type: 'danger', text: `Connection failed: ${result.message}` });
      }
    } catch (error) {
      setMessage({ type: 'danger', text: 'Failed to test connection' });
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setMessage(null);

    try {
      await api.saveConfig(config.immich_url, config.api_key);
      setMessage({ type: 'success', text: 'Configuration saved successfully! Refreshing application...' });
      
      // Force a hard refresh after a brief delay to show the success message
      setTimeout(() => {
        window.location.reload();
      }, 1500);
    } catch (error) {
      setMessage({ type: 'danger', text: 'Failed to save configuration' });
      setLoading(false);
    }
  };

  return (
    <Container className="page-container">
      <h1 className="page-title">Configuration</h1>
      
      <Card>
        <Card.Body>
          <h5 className="mb-4">Immich Server Settings</h5>
          
          {message && (
            <Alert variant={message.type} dismissible onClose={() => setMessage(null)}>
              {message.text}
            </Alert>
          )}

          <Form onSubmit={handleSave}>
            <Row>
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>Immich Server URL</Form.Label>
                  <Form.Control
                    type="url"
                    placeholder="https://immich.example.com"
                    value={config.immich_url}
                    onChange={(e) => handleInputChange('immich_url', e.target.value)}
                    required
                  />
                  <Form.Text className="text-muted">
                    The full URL of your Immich server
                  </Form.Text>
                </Form.Group>
              </Col>
              
              <Col md={6}>
                <Form.Group className="mb-3">
                  <Form.Label>API Key</Form.Label>
                  <Form.Control
                    type="password"
                    placeholder="Your Immich API Key"
                    value={config.api_key}
                    onChange={(e) => handleInputChange('api_key', e.target.value)}
                    required
                  />
                  <Form.Text className="text-muted">
                    Found in Immich under Account Settings → API Keys
                  </Form.Text>
                </Form.Group>
              </Col>
            </Row>

            <div className="d-flex gap-2">
              <Button
                variant="outline-primary"
                onClick={handleTestConnection}
                disabled={testing || !config.immich_url || !config.api_key}
              >
                {testing ? 'Testing...' : 'Test Connection'}
              </Button>
              
              <Button
                variant="primary"
                type="submit"
                disabled={loading || !config.immich_url || !config.api_key}
              >
                {loading ? 'Saving...' : 'Save Configuration'}
              </Button>
            </div>
          </Form>
        </Card.Body>
      </Card>
    </Container>
  );
};

export default Configuration;