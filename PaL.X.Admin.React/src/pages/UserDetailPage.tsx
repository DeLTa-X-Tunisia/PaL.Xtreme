import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeftIcon,
  UserIcon,
  EnvelopeIcon,
  CalendarIcon,
  ChatBubbleLeftIcon,
  HomeIcon,
  ShieldCheckIcon,
  NoSymbolIcon,
  SparklesIcon,
  ExclamationTriangleIcon,
  PencilIcon,
  TrashIcon,
  CheckIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { User, UserRole, SubscriptionType } from '../types';
import toast from 'react-hot-toast';

const UserDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [editForm, setEditForm] = useState({ role: '', subscriptionType: '' });

  useEffect(() => {
    fetchUser();
  }, [id]);

  const fetchUser = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const data = await apiService.getUserById(parseInt(id));
      setUser(data);
      setEditForm({ role: data.role, subscriptionType: data.subscriptionType });
    } catch (error) {
      console.error('Failed to fetch user:', error);
      // Mock data for demo
      setUser({
        id: parseInt(id),
        username: 'DemoUser',
        email: 'demo@palx.com',
        role: 'User',
        subscriptionType: 'Premium',
        subscriptionEndDate: '2025-12-31',
        isOnline: true,
        isBanned: false,
        createdAt: '2024-06-15T10:30:00Z',
        lastLoginAt: '2025-01-15T14:22:00Z',
        profilePicture: undefined,
        bio: 'Un utilisateur passionné de PaL.Xtreme !',
        roomsCreated: 5,
        messagesCount: 1250,
        warningsCount: 1,
      });
      setEditForm({ role: 'User', subscriptionType: 'Premium' });
    } finally {
      setLoading(false);
    }
  };

  const handleSaveChanges = async () => {
    if (!user) return;
    try {
      // Update role if changed
      if (editForm.role !== user.role) {
        await apiService.changeUserRole(user.id, editForm.role);
      }
      // Update subscription if changed
      if (editForm.subscriptionType !== user.subscriptionType) {
        if (editForm.subscriptionType === 'Free') {
          await apiService.revokeSubscription(user.id);
        } else {
          await apiService.grantSubscription(user.id, editForm.subscriptionType, 365);
        }
      }
      toast.success('Modifications enregistrées');
      setIsEditing(false);
      fetchUser();
    } catch (error) {
      toast.error('Échec de la sauvegarde');
    }
  };

  const handleBanUser = async () => {
    if (!user) return;
    const reason = prompt('Raison du bannissement:');
    if (!reason) return;
    
    try {
      await apiService.banUser(user.id, reason);
      toast.success(`${user.displayName || user.username} a été banni`);
      fetchUser();
    } catch (error) {
      toast.error('Échec du bannissement');
    }
  };

  const handleUnbanUser = async () => {
    if (!user) return;
    try {
      await apiService.unbanUser(user.id);
      toast.success(`${user.displayName || user.username} a été débanni`);
      fetchUser();
    } catch (error) {
      toast.error('Échec du débannissement');
    }
  };

  const handleWarnUser = async () => {
    if (!user) return;
    const reason = prompt('Raison de l\'avertissement:');
    if (!reason) return;
    
    try {
      await apiService.warnUser(user.id, reason);
      toast.success('Avertissement envoyé');
      fetchUser();
    } catch (error) {
      toast.error('Échec de l\'avertissement');
    }
  };

  const handleDeleteUser = async () => {
    if (!user) return;
    if (!confirm(`Êtes-vous sûr de vouloir supprimer ${user.displayName || user.username} ? Cette action est irréversible.`)) return;
    
    try {
      await apiService.deleteUser(user.id);
      toast.success('Utilisateur supprimé');
      navigate('/users');
    } catch (error) {
      toast.error('Échec de la suppression');
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="text-center py-12">
        <p className="text-dark-400">Utilisateur non trouvé</p>
        <Link to="/users" className="text-palx-400 hover:text-palx-300 mt-4 inline-block">
          ← Retour à la liste
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center gap-4">
        <button
          onClick={() => navigate('/users')}
          className="p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
        >
          <ArrowLeftIcon className="w-5 h-5" />
        </button>
        <h1 className="text-2xl font-bold text-white">Profil utilisateur</h1>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Profile Card */}
        <div className="lg:col-span-1">
          <div className="card text-center">
            {/* Avatar */}
            {user.avatarPath || user.profilePicture ? (
              <img 
                src={`http://localhost:5145/${user.avatarPath || user.profilePicture}`}
                alt={user.displayName || user.username}
                className="w-24 h-24 mx-auto rounded-full object-cover shadow-glow mb-4"
                onError={(e) => {
                  e.currentTarget.style.display = 'none';
                  e.currentTarget.nextElementSibling?.classList.remove('hidden');
                }}
              />
            ) : null}
            <div className={`w-24 h-24 mx-auto rounded-full bg-gradient-to-br from-palx-500 to-palx-700 flex items-center justify-center text-white text-4xl font-bold shadow-glow mb-4 ${user.avatarPath || user.profilePicture ? 'hidden' : ''}`}>
              {(user.displayName || user.username).charAt(0).toUpperCase()}
            </div>

            {/* Username */}
            <h2 className="text-2xl font-bold text-white">{user.displayName || user.username}</h2>
            <p className="text-dark-400 text-sm">@{user.username}</p>
            
            {/* Status badges */}
            <div className="flex items-center justify-center gap-2 mt-3">
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
              <span className={`badge ${
                user.subscriptionType === 'VIP' ? 'bg-warning/20 text-warning' :
                user.subscriptionType === 'Premium' ? 'bg-palx-500/20 text-palx-400' :
                'bg-dark-600/50 text-dark-300'
              }`}>
                {user.subscriptionType}
              </span>
            </div>

            {/* Online status */}
            {user.isBanned ? (
              <div className="mt-4 p-3 bg-danger/10 border border-danger/30 rounded-lg">
                <p className="text-danger font-medium">🚫 Compte banni</p>
                {user.banReason && <p className="text-dark-400 text-sm mt-1">{user.banReason}</p>}
              </div>
            ) : (
              <div className="mt-4 flex items-center justify-center gap-2">
                <span className={`w-3 h-3 rounded-full ${user.isOnline ? 'bg-success animate-pulse' : 'bg-dark-500'}`}></span>
                <span className={user.isOnline ? 'text-success' : 'text-dark-400'}>
                  {user.isOnline ? 'En ligne' : 'Hors ligne'}
                </span>
              </div>
            )}

            {/* Bio */}
            {user.bio && (
              <p className="mt-4 text-dark-300 text-sm">{user.bio}</p>
            )}

            {/* Quick actions */}
            <div className="mt-6 pt-6 border-t border-dark-700/50 space-y-2">
              {!isEditing ? (
                <button onClick={() => setIsEditing(true)} className="btn-secondary w-full">
                  <PencilIcon className="w-5 h-5" />
                  Modifier
                </button>
              ) : (
                <button onClick={handleSaveChanges} className="btn-primary w-full">
                  <CheckIcon className="w-5 h-5" />
                  Enregistrer
                </button>
              )}
              
              {user.isBanned ? (
                <button onClick={handleUnbanUser} className="btn-success w-full">
                  <CheckIcon className="w-5 h-5" />
                  Débannir
                </button>
              ) : (
                <button onClick={handleBanUser} className="btn-danger w-full">
                  <NoSymbolIcon className="w-5 h-5" />
                  Bannir
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Details */}
        <div className="lg:col-span-2 space-y-6">
          {/* Edit Form or Info */}
          <div className="card">
            <h3 className="text-lg font-semibold text-white mb-4">Informations</h3>
            
            {isEditing ? (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="label">Rôle</label>
                  <select
                    value={editForm.role}
                    onChange={(e) => setEditForm(prev => ({ ...prev, role: e.target.value }))}
                    className="input"
                  >
                    <option value="User">Utilisateur</option>
                    <option value="Moderator">Modérateur</option>
                    <option value="Admin">Admin</option>
                    <option value="SuperAdmin">SuperAdmin</option>
                  </select>
                </div>
                <div>
                  <label className="label">Abonnement</label>
                  <select
                    value={editForm.subscriptionType}
                    onChange={(e) => setEditForm(prev => ({ ...prev, subscriptionType: e.target.value }))}
                    className="input"
                  >
                    <option value="Free">Gratuit</option>
                    <option value="Premium">Premium</option>
                    <option value="VIP">VIP</option>
                  </select>
                </div>
                <div className="sm:col-span-2 flex gap-2">
                  <button onClick={() => setIsEditing(false)} className="btn-secondary">
                    Annuler
                  </button>
                  <button onClick={handleSaveChanges} className="btn-primary">
                    Sauvegarder
                  </button>
                </div>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                  <EnvelopeIcon className="w-5 h-5 text-dark-400" />
                  <div>
                    <p className="text-dark-400 text-xs">Email</p>
                    <p className="text-white">{user.email}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                  <CalendarIcon className="w-5 h-5 text-dark-400" />
                  <div>
                    <p className="text-dark-400 text-xs">Inscrit le</p>
                    <p className="text-white">{new Date(user.createdAt).toLocaleDateString('fr-FR', { dateStyle: 'long' })}</p>
                  </div>
                </div>
                {user.lastLoginAt && (
                  <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                    <UserIcon className="w-5 h-5 text-dark-400" />
                    <div>
                      <p className="text-dark-400 text-xs">Dernière connexion</p>
                      <p className="text-white">{new Date(user.lastLoginAt).toLocaleString('fr-FR')}</p>
                    </div>
                  </div>
                )}
                {user.subscriptionEndDate && (
                  <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                    <SparklesIcon className="w-5 h-5 text-warning" />
                    <div>
                      <p className="text-dark-400 text-xs">Abonnement expire le</p>
                      <p className="text-white">{new Date(user.subscriptionEndDate).toLocaleDateString('fr-FR')}</p>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Statistics */}
          <div className="card">
            <h3 className="text-lg font-semibold text-white mb-4">Statistiques</h3>
            <div className="grid grid-cols-3 gap-4">
              <div className="text-center p-4 bg-dark-700/30 rounded-lg">
                <HomeIcon className="w-6 h-6 text-palx-400 mx-auto mb-2" />
                <p className="text-2xl font-bold text-white">{user.roomsCreated}</p>
                <p className="text-dark-400 text-sm">Salons créés</p>
              </div>
              <div className="text-center p-4 bg-dark-700/30 rounded-lg">
                <ChatBubbleLeftIcon className="w-6 h-6 text-info mx-auto mb-2" />
                <p className="text-2xl font-bold text-white">{user.messagesCount.toLocaleString()}</p>
                <p className="text-dark-400 text-sm">Messages</p>
              </div>
              <div className="text-center p-4 bg-dark-700/30 rounded-lg">
                <ExclamationTriangleIcon className="w-6 h-6 text-warning mx-auto mb-2" />
                <p className="text-2xl font-bold text-white">{user.warningsCount}</p>
                <p className="text-dark-400 text-sm">Avertissements</p>
              </div>
            </div>
          </div>

          {/* Actions */}
          <div className="card">
            <h3 className="text-lg font-semibold text-white mb-4">Actions</h3>
            <div className="flex flex-wrap gap-3">
              <button onClick={handleWarnUser} className="btn-secondary">
                <ExclamationTriangleIcon className="w-5 h-5" />
                Envoyer un avertissement
              </button>
              <button className="btn-secondary">
                <ShieldCheckIcon className="w-5 h-5" />
                Voir les logs
              </button>
              <button onClick={handleDeleteUser} className="btn-danger">
                <TrashIcon className="w-5 h-5" />
                Supprimer le compte
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default UserDetailPage;
