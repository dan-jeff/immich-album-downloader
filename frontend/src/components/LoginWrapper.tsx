import React from 'react';
import { useAuth } from '../contexts/AuthContext';
import Login from './Login';

const LoginWrapper: React.FC = () => {
  const { login } = useAuth();
  
  return <Login onLogin={login} />;
};

export default LoginWrapper;