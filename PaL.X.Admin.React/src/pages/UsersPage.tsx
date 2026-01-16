import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { 
  MagnifyingGlassIcon, 
  FunnelIcon, 
  EllipsisVerticalIcon,
  UserIcon,
  ShieldCheckIcon,
  NoSymbolIcon,
  TrashIcon,
  PencilIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  XMarkIcon,
  CheckIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { User, UserRole, SubscriptionType, UserFilters, PaginatedResponse } from '../types';
import toast from 'react-hot-toast';

const UsersPage: React.FC = () => {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [pagination, setPagination] = useState({ page: 1, pageSize: 20, totalPages: 1, totalCount: 0 });
  const [filters, setFilters] = useState<UserFilters>({});
  const [showFilters, setShowFilters] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedUser, setSelectedUser] = useState<User | null>(null);
  const [showBanModal, setShowBanModal] = useState(false);
  const [banReason, setBanReason] = useState('');
  const [banDuration, setBanDuration] = useState<number | undefined>();

  useEffect(() => {
    fetchUsers();
  }, [pagination.page, filters]);

  const fetchUsers = async () => {
    setLoading(true);
    try {
      const response = await apiService.getUsers(pagination.page, pagination.pageSize, filters);
      setUsers(response.items);
      setPagination(prev => ({
        ...prev,
        totalPages: response.totalPages,
        totalCount: response.totalCount,
      }));
    } catch (error) {
      console.error('Failed to fetch users:', error);
      // Mock data for demo
      setUsers([
        { id: 1, username: 'SuperAdmin', email: 'admin@palx.com', role: 'SuperAdmin', subscriptionType: 'VIP', subscriptionEndDate: null, isOnline: true, isBanned: false, createdAt: '2024-01-01', roomsCreated: 15, messagesCount: 1250, warningsCount: 0 },
        { id: 2, username: 'Moderator1', email: 'mod1@palx.com', role: 'Moderator', subscriptionType: 'Premium', subscriptionEndDate: '2025-06-15', isOnline: true, isBanned: false, createdAt: '2024-02-15', roomsCreated: 5, messagesCount: 850, warningsCount: 0 },
        { id: 3, username: 'Player_XYZ', email: 'player@email.com', role: 'User', subscriptionType: 'Free', subscriptionEndDate: null, isOnline: false, isBanned: false, createdAt: '2024-06-20', roomsCreated: 2, messagesCount: 120, warningsCount: 1 },
        { id: 4, username: 'ToxicUser', email: 'toxic@email.com', role: 'User', subscriptionType: 'Free', subscriptionEndDate: null, isOnline: false, isBanned: true, banReason: 'Harcèlement répété', createdAt: '2024-03-10', roomsCreated: 0, messagesCount: 45, warningsCount: 3 },
        { id: 5, username: 'VIP_Member', email: 'vip@email.com', role: 'User', subscriptionType: 'VIP', subscriptionEndDate: '2025-12-31', isOnline: true, isBanned: false, createdAt: '2024-04-05', roomsCreated: 8, messagesCount: 560, warningsCount: 0 },
      ]);
      setPagination(prev => ({ ...prev, totalPages: 10, totalCount: 1247 }));
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setFilters(prev => ({ ...prev, search: searchQuery }));
    setPagination(prev => ({ ...prev, page: 1 }));
  };

  const handleBanUser = async () => {
    if (!selectedUser || !banReason.trim()) return;

    try {
      await apiService.banUser(selectedUser.id, banReason, banDuration);
      toast.success(`${selectedUser.displayName || selectedUser.username} a été banni`);
      setShowBanModal(false);
      setBanReason('');
      setBanDuration(undefined);
      setSelectedUser(null);
      fetchUsers();
    } catch (error) {
      toast.error('Échec du bannissement');
    }
  };

  const handleUnbanUser = async (user: User) => {
    try {
      await apiService.unbanUser(user.id);
      toast.success(`${user.displayName || user.username} a été débanni`);
      fetchUsers();
    } catch (error) {
      toast.error('Échec du débannissement');
    }
  };

  const handleChangeRole = async (user: User, newRole: UserRole) => {
    try {
      await apiService.changeUserRole(user.id, newRole);
      toast.success(`Rôle de ${user.displayName || user.username} changé en ${newRole}`);
      fetchUsers();
    } catch (error) {
      toast.error('Échec du changement de rôle');
    }
  };

  const getRoleBadgeClass = (role: UserRole) => {
    switch (role) {
      case 'SuperAdmin': return 'badge-danger';
      case 'Admin': return 'badge-warning';
      case 'Moderator': return 'badge-info';
      default: return 'badge-primary';
    }
  };

  const getSubscriptionBadgeClass = (sub: SubscriptionType) => {
    switch (sub) {
      case 'VIP': return 'bg-warning/20 text-warning';
      case 'Premium': return 'bg-palx-500/20 text-palx-400';
      default: return 'bg-dark-600/50 text-dark-300';
    }
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Gestion des utilisateurs</h1>
          <p className="text-dark-400 text-sm mt-1">{pagination.totalCount.toLocaleString()} utilisateurs au total</p>
        </div>
      </div>

      {/* Search & Filters */}
      <div className="card">
        <div className="flex flex-col sm:flex-row gap-4">
          {/* Search */}
          <div className="flex-1 flex gap-2">
            <div className="flex-1 relative">
              <MagnifyingGlassIcon className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-dark-400" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                placeholder="Rechercher un utilisateur..."
                className="input pl-12"
              />
            </div>
            <button onClick={handleSearch} className="btn-primary">
              Rechercher
            </button>
          </div>

          {/* Filter Toggle */}
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={`btn ${showFilters ? 'btn-primary' : 'btn-secondary'}`}
          >
            <FunnelIcon className="w-5 h-5" />
            <span className="hidden sm:inline">Filtres</span>
          </button>
        </div>

        {/* Filters Panel */}
        {showFilters && (
          <div className="mt-4 pt-4 border-t border-dark-700/50 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div>
              <label className="label">Rôle</label>
              <select
                value={filters.role || ''}
                onChange={(e) => setFilters(prev => ({ ...prev, role: e.target.value as UserRole || undefined }))}
                className="input"
              >
                <option value="">Tous les rôles</option>
                <option value="User">Utilisateur</option>
                <option value="Moderator">Modérateur</option>
                <option value="Admin">Admin</option>
                <option value="SuperAdmin">SuperAdmin</option>
              </select>
            </div>
            <div>
              <label className="label">Abonnement</label>
              <select
                value={filters.subscription || ''}
                onChange={(e) => setFilters(prev => ({ ...prev, subscription: e.target.value as SubscriptionType || undefined }))}
                className="input"
              >
                <option value="">Tous</option>
                <option value="Free">Gratuit</option>
                <option value="Premium">Premium</option>
                <option value="VIP">VIP</option>
              </select>
            </div>
            <div>
              <label className="label">Statut</label>
              <select
                value={filters.isOnline === undefined ? '' : String(filters.isOnline)}
                onChange={(e) => setFilters(prev => ({ ...prev, isOnline: e.target.value === '' ? undefined : e.target.value === 'true' }))}
                className="input"
              >
                <option value="">Tous</option>
                <option value="true">En ligne</option>
                <option value="false">Hors ligne</option>
              </select>
            </div>
            <div>
              <label className="label">Banni</label>
              <select
                value={filters.isBanned === undefined ? '' : String(filters.isBanned)}
                onChange={(e) => setFilters(prev => ({ ...prev, isBanned: e.target.value === '' ? undefined : e.target.value === 'true' }))}
                className="input"
              >
                <option value="">Tous</option>
                <option value="true">Oui</option>
                <option value="false">Non</option>
              </select>
            </div>
          </div>
        )}
      </div>

      {/* Users Table */}
      <div className="table-container bg-dark-800/30">
        <table className="table">
          <thead>
            <tr>
              <th>Utilisateur</th>
              <th>Rôle</th>
              <th>Abonnement</th>
              <th>Statut</th>
              <th>Stats</th>
              <th>Inscrit le</th>
              <th className="text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} className="text-center py-8">
                  <div className="w-8 h-8 border-4 border-palx-500 border-t-transparent rounded-full animate-spin mx-auto"></div>
                </td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-dark-400">
                  Aucun utilisateur trouvé
                </td>
              </tr>
            ) : (
              users.map((user) => (
                <tr key={user.id}>
                  <td>
                    <Link to={`/users/${user.id}`} className="flex items-center gap-3 group">
                      {user.avatarPath ? (
                        <img 
                          src={`http://localhost:5145/${user.avatarPath}`} 
                          alt={user.displayName || user.username}
                          className="w-10 h-10 rounded-full object-cover"
                          onError={(e) => {
                            e.currentTarget.style.display = 'none';
                            e.currentTarget.nextElementSibling?.classList.remove('hidden');
                          }}
                        />
                      ) : null}
                      <div className={`w-10 h-10 rounded-full bg-palx-600/50 flex items-center justify-center text-white font-semibold ${user.avatarPath ? 'hidden' : ''}`}>
                        {(user.displayName || user.username).charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p className="text-white font-medium group-hover:text-palx-400 transition-colors">
                          {user.displayName || user.username}
                        </p>
                        <p className="text-dark-400 text-xs">@{user.username}</p>
                      </div>
                    </Link>
                  </td>
                  <td>
                    <span 
                      className="badge"
                      style={{ 
                        backgroundColor: user.roleColor ? `${user.roleColor}20` : undefined,
                        color: user.roleColor || undefined,
                        borderColor: user.roleColor || undefined
                      }}
                    >
                      {user.roleDisplayName || user.role}
                    </span>
                  </td>
                  <td>
                    <span className={`badge ${getSubscriptionBadgeClass(user.subscriptionType)}`}>
                      {user.subscriptionType}
                    </span>
                  </td>
                  <td>
                    {user.isBanned ? (
                      <span className="badge badge-danger">🚫 Banni</span>
                    ) : user.isOnline ? (
                      <span className="flex items-center gap-2">
                        <span className="w-2 h-2 bg-success rounded-full animate-pulse"></span>
                        <span className="text-success text-sm">En ligne</span>
                      </span>
                    ) : (
                      <span className="text-dark-400 text-sm">Hors ligne</span>
                    )}
                  </td>
                  <td>
                    <div className="text-xs text-dark-400">
                      <p>{user.roomsCreated} salons</p>
                      <p>{user.messagesCount.toLocaleString()} messages</p>
                    </div>
                  </td>
                  <td className="text-dark-400 text-sm">
                    {new Date(user.createdAt).toLocaleDateString('fr-FR')}
                  </td>
                  <td>
                    <div className="flex items-center justify-end gap-2">
                      <Link
                        to={`/users/${user.id}`}
                        className="p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
                        title="Voir profil"
                      >
                        <UserIcon className="w-5 h-5" />
                      </Link>
                      {user.isBanned ? (
                        <button
                          onClick={() => handleUnbanUser(user)}
                          className="p-2 text-success hover:bg-success/10 rounded-lg"
                          title="Débannir"
                        >
                          <CheckIcon className="w-5 h-5" />
                        </button>
                      ) : (
                        <button
                          onClick={() => {
                            setSelectedUser(user);
                            setShowBanModal(true);
                          }}
                          className="p-2 text-danger hover:bg-danger/10 rounded-lg"
                          title="Bannir"
                        >
                          <NoSymbolIcon className="w-5 h-5" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
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

      {/* Ban Modal */}
      {showBanModal && selectedUser && (
        <div className="modal-overlay" onClick={() => setShowBanModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="text-lg font-semibold text-white">Bannir {selectedUser.displayName || selectedUser.username}</h3>
              <button
                onClick={() => setShowBanModal(false)}
                className="p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
              >
                <XMarkIcon className="w-5 h-5" />
              </button>
            </div>
            <div className="modal-body space-y-4">
              <div>
                <label className="label">Raison du bannissement *</label>
                <textarea
                  value={banReason}
                  onChange={(e) => setBanReason(e.target.value)}
                  className="input min-h-24"
                  placeholder="Expliquez la raison du bannissement..."
                />
              </div>
              <div>
                <label className="label">Durée (jours, laisser vide = permanent)</label>
                <input
                  type="number"
                  value={banDuration || ''}
                  onChange={(e) => setBanDuration(e.target.value ? parseInt(e.target.value) : undefined)}
                  className="input"
                  placeholder="Ex: 7 pour 7 jours"
                  min="1"
                />
              </div>
            </div>
            <div className="modal-footer">
              <button onClick={() => setShowBanModal(false)} className="btn-secondary">
                Annuler
              </button>
              <button
                onClick={handleBanUser}
                disabled={!banReason.trim()}
                className="btn-danger"
              >
                <NoSymbolIcon className="w-5 h-5" />
                Confirmer le ban
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default UsersPage;
