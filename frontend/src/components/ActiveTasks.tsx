import React, { useEffect, useState, useCallback } from 'react';
import { Container, Card, Table, Badge, ProgressBar, Button } from 'react-bootstrap';
import api from '../api';
import { Task } from '../types';
import { useWebSocket } from '../hooks/useWebSocket';

/**
 * Component to display active and completed tasks with real-time updates
 */
const ActiveTasks: React.FC = () => {
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);

  // Fetch all tasks from the API
  const fetchTasks = useCallback(async () => {
    try {
      const data = await api.getActiveTasks();
      setTasks(data);
    } catch (error: any) {
      console.error('Error fetching tasks:', error);
      // If unauthorized, the token might be expired - redirect to login
      if (error.response?.status === 401) {
        localStorage.removeItem('token');
        window.location.href = '/login';
      }
    } finally {
      setLoading(false);
    }
  }, []);

  // Handle real-time task updates via SignalR
  const handleWebSocketMessage = useCallback((message: any) => {
    console.log('ActiveTasks received WebSocket message:', message);
    // Refresh task list when any task status changes
    if (message.TaskId && (message.Type === 'download' || message.Type === 'resize')) {
      console.log('Refreshing tasks due to message type:', message.Type);
      fetchTasks();
    }
  }, [fetchTasks]);

  // Handle task deletion
  const handleDeleteTask = useCallback(async (taskId: string, taskType: string) => {
    if (!window.confirm(`Are you sure you want to delete this ${taskType} task?`)) {
      return;
    }

    try {
      await api.deleteTask(taskId);
      // Refresh task list after deletion
      fetchTasks();
    } catch (error: any) {
      console.error('Error deleting task:', error);
      alert(`Failed to delete task: ${error.response?.data?.detail || error.message}`);
    }
  }, [fetchTasks]);

  // Use WebSocket for real-time updates
  const { isConnected } = useWebSocket(handleWebSocketMessage);

  useEffect(() => {
    // Initial fetch
    fetchTasks();
    
    // Polling fallback every 5 seconds for active tasks
    const pollInterval = setInterval(() => {
      // Only poll if we have active tasks or aren't connected to SignalR
      if (!isConnected || tasks.some(task => task.status === 'in_progress' || task.status === 'pending')) {
        fetchTasks();
      }
    }, 5000);
    
    return () => clearInterval(pollInterval);
  }, [fetchTasks, isConnected, tasks]);

  const getStatusBadge = (status: string) => {
    const variants: Record<string, string> = {
      'pending': 'secondary',
      'in_progress': 'primary',
      'completed': 'success',
      'failed': 'danger',
    };
    return <Badge bg={variants[status] || 'secondary'}>{status.replace('_', ' ')}</Badge>;
  };

  const formatDate = (dateString: string) => {
    // Ensure the date is parsed correctly (backend sends UTC, convert to local)
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  // Calculate human-readable duration between start and end times
  const calculateDuration = (startTime: string, endTime?: string) => {
    const start = new Date(startTime).getTime();
    const end = endTime ? new Date(endTime).getTime() : Date.now();
    const durationMs = end - start;
    
    const seconds = Math.floor(durationMs / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    
    if (hours > 0) {
      return `${hours}h ${minutes % 60}m ${seconds % 60}s`;
    } else if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    } else {
      return `${seconds}s`;
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

  // Separate tasks by status for different UI sections
  const activeTasks = tasks.filter(task => task.status === 'in_progress' || task.status === 'pending');
  const completedTasks = tasks.filter(task => task.status === 'completed' || task.status === 'failed');

  return (
    <Container className="page-container">
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="page-title mb-0">Active Tasks</h1>
        <small className={`text-${isConnected ? 'success' : 'danger'}`}>
          {isConnected ? '● Live updates enabled' : '● Reconnecting...'}
        </small>
      </div>

      <Card className="mb-4">
        <Card.Header>
          <h5 className="mb-0">Running Tasks</h5>
        </Card.Header>
        <Card.Body>
          {activeTasks.length === 0 ? (
            <p className="text-muted mb-0">No active tasks</p>
          ) : (
            <>
              {/* Desktop Table View */}
              <div className="d-none d-lg-block">
                <Table hover>
                  <thead>
                    <tr>
                      <th>Type</th>
                      <th>Status</th>
                      <th>Progress</th>
                      <th>Message</th>
                      <th>Started</th>
                      <th>Duration</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {activeTasks.map(task => (
                      <tr key={task.id}>
                        <td>{task.type}</td>
                        <td>{getStatusBadge(task.status)}</td>
                        <td style={{ minWidth: '200px' }}>
                          {task.total > 0 ? (
                            <div>
                              <ProgressBar
                                now={(task.progress / task.total) * 100}
                                label={`${task.progress}/${task.total}`}
                                className="mb-1"
                              />
                              <small className="text-muted">
                                {Math.round((task.progress / task.total) * 100)}% complete
                              </small>
                            </div>
                          ) : (
                            // Show indeterminate progress when total is unknown
                            <ProgressBar animated striped now={100} />
                          )}
                        </td>
                        <td>{task.message || '-'}</td>
                        <td>{formatDate(task.created_at)}</td>
                        <td>{calculateDuration(task.created_at)}</td>
                        <td>
                          {task.status === 'pending' || task.status === 'failed' ? (
                            <Button
                              variant="outline-danger"
                              size="sm"
                              onClick={() => handleDeleteTask(task.id, task.type)}
                              title="Delete task"
                            >
                              🗑️
                            </Button>
                          ) : (
                            <span className="text-muted">-</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>

              {/* Mobile Card View */}
              <div className="d-lg-none">
                {activeTasks.map(task => (
                  <Card key={task.id} className="mb-3">
                    <Card.Body>
                      <div className="d-flex justify-content-between align-items-start mb-3">
                        <div>
                          <h6 className="mb-1">{task.type}</h6>
                          <div className="mb-2">{getStatusBadge(task.status)}</div>
                        </div>
                        {task.status === 'pending' || task.status === 'failed' ? (
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => handleDeleteTask(task.id, task.type)}
                            title="Delete task"
                          >
                            🗑️
                          </Button>
                        ) : null}
                      </div>
                      
                      <div className="mb-3">
                        <small className="text-muted">Progress</small>
                        {task.total > 0 ? (
                          <div>
                            <ProgressBar
                              now={(task.progress / task.total) * 100}
                              label={`${task.progress}/${task.total}`}
                              className="mb-1"
                            />
                            <small className="text-muted">
                              {Math.round((task.progress / task.total) * 100)}% complete
                            </small>
                          </div>
                        ) : (
                          <ProgressBar animated striped now={100} />
                        )}
                      </div>
                      
                      <div className="row mb-3">
                        <div className="col-12 mb-2">
                          <small className="text-muted">Message</small>
                          <div>{task.message || '-'}</div>
                        </div>
                        <div className="col-6">
                          <small className="text-muted">Started</small>
                          <div>{formatDate(task.created_at)}</div>
                        </div>
                        <div className="col-6">
                          <small className="text-muted">Duration</small>
                          <div>{calculateDuration(task.created_at)}</div>
                        </div>
                      </div>
                    </Card.Body>
                  </Card>
                ))}
              </div>
            </>
          )}
        </Card.Body>
      </Card>

      <Card>
        <Card.Header>
          <h5 className="mb-0">Completed Tasks</h5>
        </Card.Header>
        <Card.Body>
          {completedTasks.length === 0 ? (
            <p className="text-muted mb-0">No completed tasks</p>
          ) : (
            <>
              {/* Desktop Table View */}
              <div className="d-none d-lg-block">
                <Table hover>
                  <thead>
                    <tr>
                      <th>Type</th>
                      <th>Status</th>
                      <th>Message</th>
                      <th>Started</th>
                      <th>Completed</th>
                      <th>Duration</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {completedTasks.slice(0, 10).map(task => (
                      <tr key={task.id}>
                        <td>{task.type}</td>
                        <td>{getStatusBadge(task.status)}</td>
                        <td>{task.message || '-'}</td>
                        <td>{formatDate(task.created_at)}</td>
                        <td>{formatDate(task.updated_at)}</td>
                        <td>{calculateDuration(task.created_at, task.updated_at)}</td>
                        <td>
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => handleDeleteTask(task.id, task.type)}
                            title="Delete task"
                          >
                            🗑️
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>

              {/* Mobile Card View */}
              <div className="d-lg-none">
                {completedTasks.slice(0, 10).map(task => (
                  <Card key={task.id} className="mb-3">
                    <Card.Body>
                      <div className="d-flex justify-content-between align-items-start mb-3">
                        <div>
                          <h6 className="mb-1">{task.type}</h6>
                          <div className="mb-2">{getStatusBadge(task.status)}</div>
                        </div>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDeleteTask(task.id, task.type)}
                          title="Delete task"
                        >
                          🗑️
                        </Button>
                      </div>
                      
                      <div className="mb-3">
                        <small className="text-muted">Message</small>
                        <div>{task.message || '-'}</div>
                      </div>
                      
                      <div className="row">
                        <div className="col-4">
                          <small className="text-muted">Started</small>
                          <div className="small">{formatDate(task.created_at)}</div>
                        </div>
                        <div className="col-4">
                          <small className="text-muted">Completed</small>
                          <div className="small">{formatDate(task.updated_at)}</div>
                        </div>
                        <div className="col-4">
                          <small className="text-muted">Duration</small>
                          <div>{calculateDuration(task.created_at, task.updated_at)}</div>
                        </div>
                      </div>
                    </Card.Body>
                  </Card>
                ))}
              </div>
            </>
          )}
        </Card.Body>
      </Card>
    </Container>
  );
};

export default ActiveTasks;