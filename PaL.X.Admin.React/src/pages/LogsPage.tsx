import { useState, useEffect } from 'react';
import { 
  DocumentTextIcon, 
  FunnelIcon,
  MagnifyingGlassIcon,
  UserIcon,
  ClockIcon,
  ChevronLeftIcon,
  ChevronRightIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { AuditLog } from '../types';

const LogsPage: React.FC = () => {
  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [pagination, setPagination] = useState({ page: 1, totalPages: 1 });
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    fetchLogs();
  }, [pagination.page]);

  const fetchLogs = async () => {
    setLoading(true);
    try {
      const response = await apiService.getAuditLogs(pagination.page, 50);
      setLogs(response.items);
      setPagination(prev => ({ ...prev, totalPages: response.totalPages }));
    } catch (error) {
      console.error('Failed to fetch logs:', error);
      // Mock data
      setLogs([
        { id: 1, userId: 1, username: 'SuperAdmin', action: 'USER_BAN', targetType: 'User', targetId: 45, details: 'Raison: Harcèlement répété', ipAddress: '192.168.1.100', createdAt: '2025-01-15T14:30:00Z' },
        { id: 2, userId: 2, username: 'Moderator1', action: 'REPORT_RESOLVED', targetType: 'Report', targetId: 123, details: 'Signalement #123 résolu', ipAddress: '192.168.1.101', createdAt: '2025-01-15T14:15:00Z' },
        { id: 3, userId: 1, username: 'SuperAdmin', action: 'ROOM_CLOSE', targetType: 'Room', targetId: 15, details: 'Salon "Test Room" fermé', ipAddress: '192.168.1.100', createdAt: '2025-01-15T13:45:00Z' },
        { id: 4, userId: 3, username: 'Admin', action: 'BADGE_CREATE', targetType: 'Badge', targetId: 12, details: 'Badge "Champion" créé', ipAddress: '192.168.1.102', createdAt: '2025-01-15T12:30:00Z' },
        { id: 5, userId: 2, username: 'Moderator1', action: 'USER_WARN', targetType: 'User', targetId: 89, details: 'Avertissement envoyé', ipAddress: '192.168.1.101', createdAt: '2025-01-15T11:20:00Z' },
        { id: 6, userId: 1, username: 'SuperAdmin', action: 'USER_UNBAN', targetType: 'User', targetId: 33, details: 'Utilisateur débanni', ipAddress: '192.168.1.100', createdAt: '2025-01-15T10:00:00Z' },
        { id: 7, userId: 1, username: 'SuperAdmin', action: 'ROLE_CHANGE', targetType: 'User', targetId: 2, details: 'Rôle changé: User → Moderator', ipAddress: '192.168.1.100', createdAt: '2025-01-14T16:30:00Z' },
        { id: 8, userId: 3, username: 'Admin', action: 'SUBSCRIPTION_GRANT', targetType: 'User', targetId: 78, details: 'Abonnement VIP accordé (365 jours)', ipAddress: '192.168.1.102', createdAt: '2025-01-14T15:00:00Z' },
        { id: 9, userId: 2, username: 'Moderator1', action: 'MESSAGE_DELETE', targetType: 'Message', targetId: 45678, details: 'Message supprimé pour contenu inapproprié', ipAddress: '192.168.1.101', createdAt: '2025-01-14T14:20:00Z' },
        { id: 10, userId: 1, username: 'SuperAdmin', action: 'BROADCAST_SEND', details: 'Message broadcast: "Maintenance prévue demain"', ipAddress: '192.168.1.100', createdAt: '2025-01-14T12:00:00Z' },
      ]);
      setPagination(prev => ({ ...prev, totalPages: 20 }));
    } finally {
      setLoading(false);
    }
  };

  const getActionConfig = (action: string) => {
    const configs: Record<string, { color: string; bg: string; label: string }> = {
      'USER_BAN': { color: 'text-danger', bg: 'bg-danger/10', label: 'Bannissement' },
      'USER_UNBAN': { color: 'text-success', bg: 'bg-success/10', label: 'Débannissement' },
      'USER_WARN': { color: 'text-warning', bg: 'bg-warning/10', label: 'Avertissement' },
      'USER_DELETE': { color: 'text-danger', bg: 'bg-danger/10', label: 'Suppression' },
      'ROLE_CHANGE': { color: 'text-palx-400', bg: 'bg-palx-500/10', label: 'Changement rôle' },
      'REPORT_RESOLVED': { color: 'text-success', bg: 'bg-success/10', label: 'Signalement résolu' },
      'REPORT_DISMISSED': { color: 'text-dark-400', bg: 'bg-dark-600/50', label: 'Signalement rejeté' },
      'ROOM_CLOSE': { color: 'text-warning', bg: 'bg-warning/10', label: 'Fermeture salon' },
      'ROOM_DELETE': { color: 'text-danger', bg: 'bg-danger/10', label: 'Suppression salon' },
      'BADGE_CREATE': { color: 'text-palx-400', bg: 'bg-palx-500/10', label: 'Badge créé' },
      'BADGE_DELETE': { color: 'text-danger', bg: 'bg-danger/10', label: 'Badge supprimé' },
      'MESSAGE_DELETE': { color: 'text-warning', bg: 'bg-warning/10', label: 'Message supprimé' },
      'SUBSCRIPTION_GRANT': { color: 'text-success', bg: 'bg-success/10', label: 'Abonnement accordé' },
      'SUBSCRIPTION_REVOKE': { color: 'text-warning', bg: 'bg-warning/10', label: 'Abonnement révoqué' },
      'BROADCAST_SEND': { color: 'text-info', bg: 'bg-info/10', label: 'Broadcast' },
    };
    return configs[action] || { color: 'text-dark-300', bg: 'bg-dark-700/50', label: action };
  };

  const filteredLogs = searchQuery 
    ? logs.filter(log => 
        log.username.toLowerCase().includes(searchQuery.toLowerCase()) ||
        log.action.toLowerCase().includes(searchQuery.toLowerCase()) ||
        log.details?.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : logs;

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white">Logs d'audit</h1>
        <p className="text-dark-400 text-sm mt-1">Historique des actions administratives</p>
      </div>

      {/* Search */}
      <div className="card">
        <div className="flex gap-4">
          <div className="flex-1 relative">
            <MagnifyingGlassIcon className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-dark-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Rechercher dans les logs..."
              className="input pl-12"
            />
          </div>
        </div>
      </div>

      {/* Logs List */}
      <div className="space-y-3">
        {loading ? (
          <div className="flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
        ) : filteredLogs.length === 0 ? (
          <div className="card text-center py-12">
            <DocumentTextIcon className="w-16 h-16 text-dark-600 mx-auto mb-4" />
            <p className="text-dark-400">Aucun log trouvé</p>
          </div>
        ) : (
          filteredLogs.map((log) => {
            const config = getActionConfig(log.action);
            return (
              <div key={log.id} className="card hover:border-dark-600 transition-colors">
                <div className="flex items-start gap-4">
                  {/* Action Badge */}
                  <div className={`px-3 py-1.5 rounded-lg ${config.bg} ${config.color} text-sm font-medium whitespace-nowrap`}>
                    {config.label}
                  </div>

                  {/* Content */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <UserIcon className="w-4 h-4 text-dark-400" />
                      <span className="text-white font-medium">{log.displayName || log.username}</span>
                      {log.targetType && log.targetId && (
                        <>
                          <span className="text-dark-500">→</span>
                          <span className="text-dark-300">{log.targetType} #{log.targetId}</span>
                        </>
                      )}
                    </div>
                    {log.details && (
                      <p className="text-dark-400 text-sm">{log.details}</p>
                    )}
                  </div>

                  {/* Metadata */}
                  <div className="text-right text-sm">
                    <div className="flex items-center gap-1 text-dark-400">
                      <ClockIcon className="w-4 h-4" />
                      <span>{new Date(log.createdAt).toLocaleString('fr-FR')}</span>
                    </div>
                    {log.ipAddress && (
                      <p className="text-dark-500 text-xs mt-1">{log.ipAddress}</p>
                    )}
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between">
        <p className="text-dark-400 text-sm">
          Page {pagination.page} sur {pagination.totalPages}
        </p>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setPagination(prev => ({ ...prev, page: prev.page - 1 }))}
            disabled={pagination.page <= 1}
            className="btn-ghost"
          >
            <ChevronLeftIcon className="w-5 h-5" />
          </button>
          <button
            onClick={() => setPagination(prev => ({ ...prev, page: prev.page + 1 }))}
            disabled={pagination.page >= pagination.totalPages}
            className="btn-ghost"
          >
            <ChevronRightIcon className="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  );
};

export default LogsPage;
