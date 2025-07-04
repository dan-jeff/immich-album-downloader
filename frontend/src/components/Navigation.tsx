import React, { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import './Navigation.css';

interface NavigationProps {
  onLogout: () => void;
}

const Navigation: React.FC<NavigationProps> = ({ onLogout }) => {
  const [isCollapsed, setIsCollapsed] = useState(false);
  const location = useLocation();

  const toggleNav = () => {
    setIsCollapsed(!isCollapsed);
  };

  const navItems = [
    { path: '/', label: 'Home' },
    { path: '/albums', label: 'Albums' },
    { path: '/tasks', label: 'Active Tasks' },
    { path: '/resizer', label: 'Resizer' },
    { path: '/profiles', label: 'Profile Management' },
    { path: '/downloads', label: 'Available Downloads' },
    { path: '/config', label: 'Configuration' },
  ];

  return (
    <>
      <button className="nav-toggle d-md-none" onClick={toggleNav}>
        <span className="navbar-toggler-icon"></span>
      </button>
      <nav className={`sidebar ${isCollapsed ? 'collapsed' : ''}`}>
        <div className="sidebar-header">
          <h3>Immich Downloader</h3>
        </div>
        <ul className="sidebar-nav">
          {navItems.map((item) => (
            <li key={item.path} className="nav-item">
              <Link
                to={item.path}
                className={`nav-link ${location.pathname === item.path ? 'active' : ''}`}
                onClick={() => setIsCollapsed(true)}
              >
                {item.label}
              </Link>
            </li>
          ))}
        </ul>
        <div className="sidebar-footer">
          <button className="btn btn-outline-secondary btn-sm w-100" onClick={onLogout}>
            Logout
          </button>
        </div>
      </nav>
      {!isCollapsed && <div className="nav-overlay d-md-none" onClick={toggleNav}></div>}
    </>
  );
};

export default Navigation;