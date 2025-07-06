import React, { useEffect, useState } from 'react';
import { Container, Row, Col, Card } from 'react-bootstrap';
import { Link } from 'react-router-dom';
import api from '../api';
import { Album, Task, Download } from '../types';

const Home: React.FC = () => {
  const [stats, setStats] = useState({ album_count: 0, image_count: 0, download_count: 0 });
  const [albums, setAlbums] = useState<Album[]>([]);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [downloads, setDownloads] = useState<Download[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [statsData, albumsData, tasksData, downloadsData] = await Promise.all([
          api.getStats(),
          api.getAlbums(),
          api.getActiveTasks(),
          api.getCompletedDownloads(),
        ]);
        setStats(statsData);
        setAlbums(albumsData);
        setTasks(tasksData);
        setDownloads(downloadsData);
      } catch (error) {
        console.error('Error fetching data:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, []);

  const activeTasks = tasks.filter(task => task.status === 'in_progress' || task.status === 'pending');

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
      <h1 className="page-title">Dashboard</h1>
      
      <Row className="mb-4">
        <Col md={3} className="mb-3">
          <div className="stat-card">
            <div className="stat-value">{stats.album_count}</div>
            <div className="stat-label">Immich Albums</div>
          </div>
        </Col>
        <Col md={3} className="mb-3">
          <div className="stat-card">
            <div className="stat-value">{stats.image_count.toLocaleString()}</div>
            <div className="stat-label">Total Images</div>
          </div>
        </Col>
        <Col md={3} className="mb-3">
          <div className="stat-card">
            <div className="stat-value">{activeTasks.length}</div>
            <div className="stat-label">Active Tasks</div>
          </div>
        </Col>
        <Col md={3} className="mb-3">
          <div className="stat-card">
            <div className="stat-value">{stats.download_count}</div>
            <div className="stat-label">Downloads</div>
          </div>
        </Col>
      </Row>

      <Row>
        <Col md={6} className="mb-4">
          <Card>
            <Card.Header>
              <div className="d-flex justify-content-between align-items-center">
                <h5 className="mb-0">Recent Albums</h5>
                <Link to="/albums" className="btn btn-sm btn-outline-primary">View All</Link>
              </div>
            </Card.Header>
            <Card.Body>
              {albums.length > 0 ? (
                <div className="list-group list-group-flush">
                  {albums.sort((a, b) => a.albumName.localeCompare(b.albumName)).slice(0, 5).map(album => (
                    <div key={album.id} className="list-group-item px-0">
                      <div className="d-flex justify-content-between align-items-center">
                        <div>
                          <h6 className="mb-1">{album.albumName}</h6>
                          <small className="text-muted">
                            Immich: {album.assetCount} | Local: {album.localAssetCount || 0} assets
                          </small>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-muted mb-0">No albums found</p>
              )}
            </Card.Body>
          </Card>
        </Col>

        <Col md={6} className="mb-4">
          <Card>
            <Card.Header>
              <div className="d-flex justify-content-between align-items-center">
                <h5 className="mb-0">Active Tasks</h5>
                <Link to="/tasks" className="btn btn-sm btn-outline-primary">View All</Link>
              </div>
            </Card.Header>
            <Card.Body>
              {activeTasks.length > 0 ? (
                <div className="list-group list-group-flush">
                  {activeTasks.slice(0, 5).map(task => (
                    <div key={task.id} className="list-group-item px-0">
                      <div className="d-flex justify-content-between align-items-center">
                        <div>
                          <h6 className="mb-1">{task.type}</h6>
                          <small className="text-muted">{task.message || 'Processing...'}</small>
                        </div>
                        <span className={`badge bg-${task.status === 'in_progress' ? 'primary' : 'secondary'}`}>
                          {task.status}
                        </span>
                      </div>
                      {task.total > 0 && (
                        <div className="progress mt-2" style={{ height: '5px' }}>
                          <div
                            className="progress-bar"
                            style={{ width: `${(task.progress / task.total) * 100}%` }}
                          ></div>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-muted mb-0">No active tasks</p>
              )}
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <Row>
        <Col>
          <Card>
            <Card.Header>
              <h5 className="mb-0">Quick Actions</h5>
            </Card.Header>
            <Card.Body>
              <Row>
                <Col md={4} className="mb-3">
                  <Link to="/albums" className="btn btn-outline-primary w-100">
                    Browse Albums
                  </Link>
                </Col>
                <Col md={4} className="mb-3">
                  <Link to="/resizer" className="btn btn-outline-primary w-100">
                    Resize Images
                  </Link>
                </Col>
                <Col md={4} className="mb-3">
                  <Link to="/profiles" className="btn btn-outline-primary w-100">
                    Manage Profiles
                  </Link>
                </Col>
              </Row>
            </Card.Body>
          </Card>
        </Col>
      </Row>
    </Container>
  );
};

export default Home;