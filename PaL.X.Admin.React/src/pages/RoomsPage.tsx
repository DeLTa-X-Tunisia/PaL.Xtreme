import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  MagnifyingGlassIcon, 
  FunnelIcon, 
  UsersIcon,
  LockClosedIcon,
  LockOpenIcon,
  TrashIcon,
  XMarkIcon,
  EyeIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { Room, RoomCategory, RoomFilters } from '../types';
import toast from 'react-hot-toast';

const RoomsPage: React.FC = () => {
  const navigate = useNavigate();
  const [rooms, setRooms] = useState<Room[]>([]);
  const [loading, setLoading] = useState(true);
  const [pagination, setPagination] = useState({ page: 1, pageSize: 20, totalPages: 1, totalCount: 0 });
  const [filters, setFilters] = useState<RoomFilters>({});
  const [showFilters, setShowFilters] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    fetchRooms();
  }, [pagination.page, filters]);

  const fetchRooms = async () => {
    setLoading(true);
    try {
      const response = await apiService.getRooms(pagination.page, pagination.pageSize, filters);
      setRooms(response.items);
      setPagination(prev => ({
        ...prev,
        totalPages: response.totalPages,
        totalCount: response.totalCount,
      }));
    } catch (error) {
      console.error('Failed to fetch rooms:', error);
      // Mock data for demo
      setRooms([
        { id: 1, name: 'Salon Principal', description: 'Le salon principal de discussion', ownerId: 1, ownerUsername: 'admin', ownerDisplayName: 'Admin System', createdAt: '2024-01-01', isActive: true, currentUsers: 45, maxUsers: 100, isPrivate: false, hasPassword: false, category: 'General', bannedUsers: [], tags: ['chat', 'communauté'] },
        { id: 2, name: 'Gaming Zone', description: 'Pour les gamers', ownerId: 2, ownerUsername: 'gamer1', ownerDisplayName: 'Jean Gamer', createdAt: '2024-03-15', isActive: true, currentUsers: 23, maxUsers: 50, isPrivate: false, hasPassword: false, category: 'Gaming', bannedUsers: [], tags: ['jeux', 'fps', 'mmorpg'] },
        { id: 3, name: 'Music Lounge', description: 'Partagez votre musique', ownerId: 5, ownerUsername: 'dj_master', ownerDisplayName: 'DJ Master Mix', createdAt: '2024-04-20', isActive: true, currentUsers: 12, maxUsers: 30, isPrivate: false, hasPassword: false, category: 'Music', bannedUsers: [], tags: ['musique', 'dj'] },
        { id: 4, name: 'VIP Only', description: 'Réservé aux VIP', ownerId: 1, ownerUsername: 'admin', ownerDisplayName: 'Admin System', createdAt: '2024-02-10', isActive: true, currentUsers: 8, maxUsers: 20, isPrivate: true, hasPassword: true, category: 'General', bannedUsers: [], tags: ['vip', 'exclusif'] },
        { id: 5, name: 'Tech Talk', description: 'Discussions tech', ownerId: 3, ownerUsername: 'techguru', ownerDisplayName: 'Pierre Technologie', createdAt: '2024-05-01', isActive: false, currentUsers: 0, maxUsers: 40, isPrivate: false, hasPassword: false, category: 'Tech', bannedUsers: [], tags: ['tech', 'dev', 'code'] },
      ]);
      setPagination(prev => ({ ...prev, totalPages: 3, totalCount: 23 }));
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setFilters(prev => ({ ...prev, search: searchQuery }));
    setPagination(prev => ({ ...prev, page: 1 }));
  };

  const handleCloseRoom = async (room: Room) => {
    const reason = prompt('Raison de la fermeture (optionnel):');
    try {
      await apiService.closeRoom(room.id, reason || undefined);
      toast.success(`Salon "${room.name}" fermé`);
      fetchRooms();
    } catch (error) {
      toast.error('Échec de la fermeture');
    }
  };

  const handleDeleteRoom = async (room: Room) => {
    if (!confirm(`Êtes-vous sûr de vouloir supprimer le salon "${room.name}" ?`)) return;
    
    try {
      await apiService.deleteRoom(room.id);
      toast.success('Salon supprimé');
      fetchRooms();
    } catch (error) {
      toast.error('Échec de la suppression');
    }
  };

  const getCategoryColor = (category: RoomCategory) => {
    switch (category) {
      case 'Gaming': return 'bg-success/20 text-success';
      case 'Music': return 'bg-palx-500/20 text-palx-400';
      case 'Art': return 'bg-pink-500/20 text-pink-400';
      case 'Tech': return 'bg-info/20 text-info';
      default: return 'bg-dark-600/50 text-dark-300';
    }
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Gestion des salons</h1>
          <p className="text-dark-400 text-sm mt-1">{pagination.totalCount} salons au total</p>
        </div>
      </div>

      {/* Search & Filters */}
      <div className="card">
        <div className="flex flex-col sm:flex-row gap-4">
          <div className="flex-1 flex gap-2">
            <div className="flex-1 relative">
              <MagnifyingGlassIcon className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-dark-400" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                placeholder="Rechercher un salon..."
                className="input pl-12"
              />
            </div>
            <button onClick={handleSearch} className="btn-primary">
              Rechercher
            </button>
          </div>
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={`btn ${showFilters ? 'btn-primary' : 'btn-secondary'}`}
          >
            <FunnelIcon className="w-5 h-5" />
            <span className="hidden sm:inline">Filtres</span>
          </button>
        </div>

        {showFilters && (
          <div className="mt-4 pt-4 border-t border-dark-700/50 grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div>
              <label className="label">Catégorie</label>
              <select
                value={filters.category || ''}
                onChange={(e) => setFilters(prev => ({ ...prev, category: e.target.value as RoomCategory || undefined }))}
                className="input"
              >
                <option value="">Toutes</option>
                <option value="General">Général</option>
                <option value="Gaming">Gaming</option>
                <option value="Music">Musique</option>
                <option value="Art">Art</option>
                <option value="Tech">Tech</option>
                <option value="Other">Autre</option>
              </select>
            </div>
            <div>
              <label className="label">Statut</label>
              <select
                value={filters.isActive === undefined ? '' : String(filters.isActive)}
                onChange={(e) => setFilters(prev => ({ ...prev, isActive: e.target.value === '' ? undefined : e.target.value === 'true' }))}
                className="input"
              >
                <option value="">Tous</option>
                <option value="true">Actif</option>
                <option value="false">Fermé</option>
              </select>
            </div>
            <div>
              <label className="label">Accès</label>
              <select
                value={filters.isPrivate === undefined ? '' : String(filters.isPrivate)}
                onChange={(e) => setFilters(prev => ({ ...prev, isPrivate: e.target.value === '' ? undefined : e.target.value === 'true' }))}
                className="input"
              >
                <option value="">Tous</option>
                <option value="false">Public</option>
                <option value="true">Privé</option>
              </select>
            </div>
          </div>
        )}
      </div>

      {/* Rooms Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {loading ? (
          <div className="col-span-full flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
        ) : rooms.length === 0 ? (
          <div className="col-span-full text-center py-12 text-dark-400">
            Aucun salon trouvé
          </div>
        ) : (
          rooms.map((room) => (
            <div key={room.id} className="card-hover">
              {/* Header */}
              <div className="flex items-start justify-between mb-4">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="text-lg font-semibold text-white truncate">{room.name}</h3>
                    {room.isPrivate && (
                      <LockClosedIcon className="w-4 h-4 text-warning flex-shrink-0" title="Privé" />
                    )}
                    {room.hasPassword && (
                      <span className="text-warning text-xs">🔑</span>
                    )}
                  </div>
                  <p className="text-dark-400 text-sm truncate">{room.description || 'Aucune description'}</p>
                </div>
                <span className={`badge ${getCategoryColor(room.category)}`}>
                  {room.category}
                </span>
              </div>

              {/* Stats */}
              <div className="flex items-center gap-4 mb-4">
                <div className="flex items-center gap-2">
                  <UsersIcon className="w-5 h-5 text-dark-400" />
                  <span className="text-white font-medium">{room.currentUsers}</span>
                  <span className="text-dark-400">/ {room.maxUsers}</span>
                </div>
                <div className={`px-2 py-1 rounded-full text-xs font-medium ${
                  room.isActive ? 'bg-success/20 text-success' : 'bg-dark-600/50 text-dark-400'
                }`}>
                  {room.isActive ? '● Actif' : '○ Fermé'}
                </div>
              </div>

              {/* Owner & Date */}
              <div className="flex items-center justify-between text-sm text-dark-400 mb-4">
                <span>Par {room.ownerDisplayName || room.ownerUsername}</span>
                <span>{new Date(room.createdAt).toLocaleDateString('fr-FR')}</span>
              </div>

              {/* Tags */}
              {room.tags && room.tags.length > 0 && (
                <div className="flex flex-wrap gap-1 mb-4">
                  {room.tags.slice(0, 3).map((tag, i) => (
                    <span key={i} className="px-2 py-0.5 bg-dark-700/50 rounded text-xs text-dark-300">
                      #{tag}
                    </span>
                  ))}
                  {room.tags.length > 3 && (
                    <span className="text-dark-400 text-xs">+{room.tags.length - 3}</span>
                  )}
                </div>
              )}

              {/* Progress bar for capacity */}
              <div className="mb-4">
                <div className="h-2 bg-dark-700 rounded-full overflow-hidden">
                  <div 
                    className={`h-full transition-all ${
                      room.currentUsers / room.maxUsers > 0.8 ? 'bg-danger' :
                      room.currentUsers / room.maxUsers > 0.5 ? 'bg-warning' :
                      'bg-success'
                    }`}
                    style={{ width: `${(room.currentUsers / room.maxUsers) * 100}%` }}
                  />
                </div>
              </div>

              {/* Actions */}
              <div className="flex items-center gap-2 pt-4 border-t border-dark-700/50">
                <button 
                  onClick={() => navigate(`/rooms/${room.id}`)}
                  className="btn-ghost flex-1 py-2 text-sm"
                >
                  <EyeIcon className="w-4 h-4" />
                  Détails
                </button>
                {room.isActive && (
                  <button 
                    onClick={() => handleCloseRoom(room)}
                    className="btn-ghost py-2 text-sm text-warning hover:text-warning"
                  >
                    <XMarkIcon className="w-4 h-4" />
                    Fermer
                  </button>
                )}
                <button 
                  onClick={() => handleDeleteRoom(room)}
                  className="btn-ghost py-2 text-sm text-danger hover:text-danger"
                >
                  <TrashIcon className="w-4 h-4" />
                </button>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};

export default RoomsPage;
