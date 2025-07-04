import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import api from '../api';

interface AuthContextType {
  isAuthenticated: boolean;
  loading: boolean;
  login: (token: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const checkAuth = async () => {
      const token = localStorage.getItem('token');
      if (token) {
        api.setAuthToken(token);
        try {
          // Validate token with a simple API call
          await api.checkSetup();
          setIsAuthenticated(true);
        } catch (error: any) {
          // Token is invalid or expired
          if (error.response?.status === 401) {
            localStorage.removeItem('token');
            api.setAuthToken('');
            setIsAuthenticated(false);
          } else {
            // Network error - assume token is valid for offline use
            setIsAuthenticated(true);
          }
        }
      }
      setLoading(false);
    };

    checkAuth();
  }, []);

  const login = (token: string) => {
    localStorage.setItem('token', token);
    api.setAuthToken(token);
    setIsAuthenticated(true);
  };

  const logout = () => {
    localStorage.removeItem('token');
    api.setAuthToken('');
    setIsAuthenticated(false);
  };

  const value = {
    isAuthenticated,
    loading,
    login,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};