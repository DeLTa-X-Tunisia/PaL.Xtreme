import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { User, AuthState, LoginRequest } from '../types';
import apiService from '../services/api';
import toast from 'react-hot-toast';

interface AuthContextType extends AuthState {
  login: (credentials: LoginRequest) => Promise<boolean>;
  logout: () => Promise<void>;
  updateUser: (user: User) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

// Helper pour vérifier si l'utilisateur a des privilèges admin (RoleLevel 1-6)
const isAdminRole = (role: string, roleLevel?: number): boolean => {
  // Vérifier par roleLevel (1-6 = admin système)
  if (roleLevel !== undefined && roleLevel >= 1 && roleLevel <= 6) {
    return true;
  }
  // Vérifier par nom de rôle
  const adminRoles = [
    'ServerMaster', 'ServerEditor', 'ServerSuperAdmin', 
    'ServerAdmin', 'ServerModerator', 'ServerHelp',
    'Admin', 'SuperAdmin', 'Moderator' // Compatibilité
  ];
  return adminRoles.includes(role);
};

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [state, setState] = useState<AuthState>({
    user: null,
    token: null,
    isAuthenticated: false,
    isLoading: true,
  });

  // Check stored token on mount
  useEffect(() => {
    const checkAuth = async () => {
      const token = localStorage.getItem('auth_token');
      const storedUser = localStorage.getItem('admin_user');
      
      if (token && storedUser) {
        try {
          // Validate token with API
          const user = await apiService.validateToken();
          
          // Check if user has admin privileges
          if (!isAdminRole(user.role, (user as any).roleLevel)) {
            throw new Error('Insufficient privileges');
          }
          
          setState({
            user,
            token,
            isAuthenticated: true,
            isLoading: false,
          });
        } catch {
          // Token invalid or expired
          localStorage.removeItem('auth_token');
          localStorage.removeItem('admin_user');
          setState({
            user: null,
            token: null,
            isAuthenticated: false,
            isLoading: false,
          });
        }
      } else {
        setState(prev => ({ ...prev, isLoading: false }));
      }
    };

    checkAuth();
  }, []);

  const login = useCallback(async (credentials: LoginRequest): Promise<boolean> => {
    try {
      setState(prev => ({ ...prev, isLoading: true }));
      
      const response = await apiService.login(credentials);
      const { token, user } = response;

      // Check admin privileges
      if (!isAdminRole(user.role, (user as any).roleLevel)) {
        toast.error('Accès refusé. Privilèges administrateur requis.');
        setState(prev => ({ ...prev, isLoading: false }));
        return false;
      }

      // Store credentials
      localStorage.setItem('auth_token', token);
      localStorage.setItem('admin_user', JSON.stringify(user));

      setState({
        user,
        token,
        isAuthenticated: true,
        isLoading: false,
      });

      toast.success(`Bienvenue, ${user.displayName || user.username}!`);
      return true;
    } catch (error: any) {
      const message = error.response?.data?.message || 'Échec de la connexion';
      toast.error(message);
      setState(prev => ({ ...prev, isLoading: false }));
      return false;
    }
  }, []);

  const logout = useCallback(async () => {
    try {
      await apiService.logout();
    } catch {
      // Ignore logout errors
    } finally {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('admin_user');
      setState({
        user: null,
        token: null,
        isAuthenticated: false,
        isLoading: false,
      });
      toast.success('Déconnexion réussie');
    }
  }, []);

  const updateUser = useCallback((user: User) => {
    localStorage.setItem('admin_user', JSON.stringify(user));
    setState(prev => ({ ...prev, user }));
  }, []);

  return (
    <AuthContext.Provider value={{ ...state, login, logout, updateUser }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
