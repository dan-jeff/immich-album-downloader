import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

/**
 * SignalR message structure for task progress updates
 */
interface WebSocketMessage {
  TaskId: string;
  Type: string;
  Status?: string;
  Progress?: number;
  Total?: number;
  Message?: string;
}

/**
 * Custom hook for SignalR connection with auto-reconnection
 * @param onMessage - Callback function to handle incoming messages
 */
export const useWebSocket = (onMessage?: (message: WebSocketMessage) => void) => {
  const connection = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const reconnectTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  const connect = useCallback(async () => {
    try {
      // Get the JWT token from localStorage
      const token = localStorage.getItem('token');
      if (!token) {
        console.log('No auth token found, skipping SignalR connection');
        return;
      }

      // Create SignalR connection with JWT authentication
      connection.current = new signalR.HubConnectionBuilder()
        .withUrl(`${window.location.origin}/progressHub`, {
          accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .build();

      // Set up event handlers
      connection.current.onreconnecting(() => {
        setIsConnected(false);
        console.log('SignalR reconnecting...');
      });

      connection.current.onreconnected(() => {
        setIsConnected(true);
        console.log('SignalR reconnected');
      });

      connection.current.onclose(() => {
        setIsConnected(false);
        console.log('SignalR disconnected');
        // Attempt to reconnect after 5 seconds
        if (reconnectTimeoutRef.current) {
          clearTimeout(reconnectTimeoutRef.current);
        }
        reconnectTimeoutRef.current = setTimeout(connect, 5000);
      });

      // Listen for task progress messages
      connection.current.on('TaskStatusUpdated', (message: WebSocketMessage) => {
        console.log('Received task progress:', message);
        if (onMessage) {
          onMessage(message);
        }
      });

      // Start the connection
      console.log('Attempting SignalR connection to:', `${window.location.origin}/progressHub`);
      await connection.current.start();
      setIsConnected(true);
      console.log('SignalR connected successfully');

      // Cancel any pending reconnection attempts
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
    } catch (error) {
      console.error('Error creating SignalR connection:', error);
      setIsConnected(false);
      // Try to reconnect after 5 seconds
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      reconnectTimeoutRef.current = setTimeout(connect, 5000);
    }
  }, [onMessage]);

  useEffect(() => {
    // Delay initial connection to ensure the app is fully loaded
    const initialConnectionTimeout = setTimeout(() => {
      connect();
    }, 1000);

    // Cleanup function
    return () => {
      clearTimeout(initialConnectionTimeout);
      // Cancel reconnection attempts
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }
      // Close SignalR connection
      if (connection.current) {
        connection.current.stop();
      }
    };
  }, [connect]);

  const sendMessage = useCallback(async (message: any) => {
    if (connection.current && connection.current.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.current.invoke('SendMessage', message);
      } catch (error) {
        console.error('Error sending SignalR message:', error);
      }
    }
  }, []);

  return { isConnected, sendMessage };
};