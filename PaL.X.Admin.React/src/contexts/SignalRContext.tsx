import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';
import { DashboardStats, Report } from '../types';
import toast from 'react-hot-toast';

interface SignalRContextType {
  connection: signalR.HubConnection | null;
  isConnected: boolean;
  onlineUsers: Set<number>;
  realtimeStats: DashboardStats | null;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

const HUB_URL = import.meta.env.VITE_SIGNALR_URL || '/hub/admin';

export const SignalRProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { isAuthenticated, token } = useAuth();
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [onlineUsers, setOnlineUsers] = useState<Set<number>>(new Set());
  const [realtimeStats, setRealtimeStats] = useState<DashboardStats | null>(null);

  // Setup connection when authenticated
  useEffect(() => {
    if (!isAuthenticated || !token) {
      if (connection) {
        connection.stop();
        setConnection(null);
        setIsConnected(false);
      }
      return;
    }

    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    setConnection(newConnection);

    return () => {
      newConnection.stop();
    };
  }, [isAuthenticated, token]);

  // Start connection and setup event handlers
  useEffect(() => {
    if (!connection) return;

    const startConnection = async () => {
      try {
        await connection.start();
        console.log('SignalR Admin Hub connected');
        setIsConnected(true);

        // Join admin channel
        await connection.invoke('JoinAdminChannel');
      } catch (err) {
        console.error('SignalR Connection Error:', err);
        setIsConnected(false);
        
        // Retry after 5 seconds
        setTimeout(startConnection, 5000);
      }
    };

    // Event Handlers
    connection.on('UserConnected', (userId: number) => {
      setOnlineUsers(prev => new Set(prev).add(userId));
    });

    connection.on('UserDisconnected', (userId: number) => {
      setOnlineUsers(prev => {
        const newSet = new Set(prev);
        newSet.delete(userId);
        return newSet;
      });
    });

    connection.on('StatsUpdated', (stats: DashboardStats) => {
      setRealtimeStats(stats);
    });

    connection.on('NewReport', (report: Report) => {
      toast('📢 Nouveau signalement reçu', {
        icon: '⚠️',
        style: {
          background: '#1e293b',
          color: '#fbbf24',
          border: '1px solid #f59e0b',
        },
      });
    });

    connection.on('UserBanned', (userId: number, username: string) => {
      toast.success(`🔨 ${username} a été banni`);
    });

    connection.on('RoomClosed', (roomId: number, roomName: string) => {
      toast(`🚪 Salon "${roomName}" fermé`, { icon: '🔒' });
    });

    connection.on('BroadcastMessage', (message: string) => {
      toast(message, {
        icon: '📢',
        duration: 10000,
        style: {
          background: '#4f46e5',
          color: 'white',
        },
      });
    });

    connection.onreconnecting(() => {
      console.log('SignalR reconnecting...');
      setIsConnected(false);
      toast.loading('Reconnexion en cours...', { id: 'signalr-reconnect' });
    });

    connection.onreconnected(() => {
      console.log('SignalR reconnected');
      setIsConnected(true);
      toast.success('Reconnecté!', { id: 'signalr-reconnect' });
      connection.invoke('JoinAdminChannel');
    });

    connection.onclose(() => {
      console.log('SignalR connection closed');
      setIsConnected(false);
    });

    startConnection();

    return () => {
      connection.off('UserConnected');
      connection.off('UserDisconnected');
      connection.off('StatsUpdated');
      connection.off('NewReport');
      connection.off('UserBanned');
      connection.off('RoomClosed');
      connection.off('BroadcastMessage');
    };
  }, [connection]);

  return (
    <SignalRContext.Provider value={{ connection, isConnected, onlineUsers, realtimeStats }}>
      {children}
    </SignalRContext.Provider>
  );
};

export const useSignalR = (): SignalRContextType => {
  const context = useContext(SignalRContext);
  if (!context) {
    throw new Error('useSignalR must be used within a SignalRProvider');
  }
  return context;
};
