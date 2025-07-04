import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import 'bootstrap/dist/css/bootstrap.min.css';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import Navigation from './components/Navigation';
import ProtectedRoute from './components/ProtectedRoute';
import LoginWrapper from './components/LoginWrapper';
import Home from './components/Home';
import Albums from './components/Albums';
import ActiveTasks from './components/ActiveTasks';
import Resizer from './components/Resizer';
import ProfileManagement from './components/ProfileManagement';
import AvailableDownloads from './components/AvailableDownloads';
import Configuration from './components/Configuration';
import './App.css';

function AppContent() {
  const { isAuthenticated, loading, logout } = useAuth();

  if (loading) {
    return (
      <div className="d-flex justify-content-center align-items-center" style={{ height: '100vh' }}>
        <div className="spinner-border" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="App">
      {isAuthenticated && <Navigation onLogout={logout} />}
      <div className={isAuthenticated ? 'content-with-nav' : ''}>
        <Routes>
          <Route
            path="/login"
            element={
              isAuthenticated ? (
                <Navigate to="/" replace />
              ) : (
                <LoginWrapper />
              )
            }
          />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <Home />
              </ProtectedRoute>
            }
          />
          <Route
            path="/albums"
            element={
              <ProtectedRoute>
                <Albums />
              </ProtectedRoute>
            }
          />
          <Route
            path="/tasks"
            element={
              <ProtectedRoute>
                <ActiveTasks />
              </ProtectedRoute>
            }
          />
          <Route
            path="/resizer"
            element={
              <ProtectedRoute>
                <Resizer />
              </ProtectedRoute>
            }
          />
          <Route
            path="/profiles"
            element={
              <ProtectedRoute>
                <ProfileManagement />
              </ProtectedRoute>
            }
          />
          <Route
            path="/downloads"
            element={
              <ProtectedRoute>
                <AvailableDownloads />
              </ProtectedRoute>
            }
          />
          <Route
            path="/config"
            element={
              <ProtectedRoute>
                <Configuration />
              </ProtectedRoute>
            }
          />
        </Routes>
      </div>
    </div>
  );
}

function App() {
  return (
    <AuthProvider>
      <Router>
        <AppContent />
      </Router>
    </AuthProvider>
  );
}

export default App;