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
  CheckIcon,
  CreditCardIcon,
  ClockIcon,
  XMarkIcon,
  PlusIcon,
  ArrowPathIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { User, Role } from '../types';
import toast from 'react-hot-toast';

// Helper pour convertir le type d'abonnement numérique en string
const subscriptionTierToString = (tierId: number | string): string => {
  if (typeof tierId === 'string') return tierId;
  switch (tierId) {
    case 0: return 'Free';
    case 1: return 'Premium';
    case 2: return 'VIP';
    default: return 'Free';
  }
};

// Helper pour convertir le type d'abonnement string en numérique
const subscriptionStringToTier = (type: string): number => {
  switch (type) {
    case 'Free': return 0;
    case 'Premium': return 1;
    case 'VIP': return 2;
    default: return 0;
  }
};

// Interface pour les tiers d'abonnement
interface SubscriptionTier {
  id: number;
  name: string;
  displayName: string;
  color: string;
  maxRoomsOwned: number;
  maxRoomsJoined: number;
  canBroadcast: boolean;
  canUploadFiles: boolean;
  maxFileSize: number;
  customStatus: boolean;
  prioritySupport: boolean;
  adFree: boolean;
}

// Interface pour les durées d'abonnement
interface SubscriptionDuration {
  id: number;
  name: string;
  days: number;
  displayName: string;
}

const UserDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [editForm, setEditForm] = useState({ roleLevel: 7, subscriptionType: 'Free' });
  const [roles, setRoles] = useState<Role[]>([]);
  
  // États pour la gestion des abonnements
  const [subscriptionTiers, setSubscriptionTiers] = useState<SubscriptionTier[]>([]);
  const [subscriptionDurations, setSubscriptionDurations] = useState<SubscriptionDuration[]>([]);
  const [showSubscriptionModal, setShowSubscriptionModal] = useState(false);
  const [subscriptionForm, setSubscriptionForm] = useState({
    tierId: 1,
    durationId: 1,
    paymentMethod: 'admin_grant'
  });
  const [subscriptionLoading, setSubscriptionLoading] = useState(false);

  useEffect(() => {
    fetchRoles();
    fetchSubscriptionData();
  }, []);

  useEffect(() => {
    fetchUser();
  }, [id]);

  const fetchSubscriptionData = async () => {
    try {
      const [tiers, durations] = await Promise.all([
        apiService.getSubscriptionTiers(),
        apiService.getSubscriptionDurations()
      ]);
      // Filtrer pour exclure "Free" des tiers assignables
      setSubscriptionTiers(tiers.filter((t: SubscriptionTier) => t.name !== 'Free'));
      setSubscriptionDurations(durations);
      if (tiers.length > 0) {
        const firstPaidTier = tiers.find((t: SubscriptionTier) => t.name !== 'Free');
        if (firstPaidTier) {
          setSubscriptionForm(prev => ({ ...prev, tierId: firstPaidTier.id }));
        }
      }
      if (durations.length > 0) {
        setSubscriptionForm(prev => ({ ...prev, durationId: durations[0].id }));
      }
    } catch (error) {
      console.error('Failed to fetch subscription data:', error);
    }
  };

  const fetchRoles = async () => {
    try {
      const data = await apiService.getRoles();
      // Charger tous les rôles pour l'affichage
      setRoles(data);
    } catch (error) {
      console.error('Failed to fetch roles:', error);
    }
  };

  const fetchUser = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const data = await apiService.getUserById(parseInt(id));
      console.log('User data received:', data);
      console.log('RoleLevel:', data.roleLevel, 'SubscriptionType:', data.subscriptionType);
      setUser(data);
      // Utiliser roleLevel et convertir subscriptionType
      const subType = subscriptionTierToString(data.subscriptionType);
      console.log('Setting editForm - roleLevel:', data.roleLevel || 7, 'subscriptionType:', subType);
      setEditForm({ 
        roleLevel: data.roleLevel || 7,
        subscriptionType: subType
      });
    } catch (error) {
      console.error('Failed to fetch user:', error);
      // Mock data for demo
      setUser({
        id: parseInt(id),
        username: 'DemoUser',
        email: 'demo@palx.com',
        role: 'User',
        roleLevel: 7,
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
      setEditForm({ roleLevel: 7, subscriptionType: 'Premium' });
    } finally {
      setLoading(false);
    }
  };

  const handleSaveChanges = async () => {
    if (!user) return;
    try {
      // Update role if changed (compare by roleLevel)
      if (editForm.roleLevel !== user.roleLevel) {
        // Trouver le nom du rôle correspondant au roleLevel
        const selectedRole = roles.find(r => r.roleLevel === editForm.roleLevel);
        if (selectedRole) {
          await apiService.changeUserRole(user.id, selectedRole.roleName);
        }
      }
      // Update subscription if changed
      const currentSubType = subscriptionTierToString(user.subscriptionType);
      if (editForm.subscriptionType !== currentSubType) {
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

  // Gestion des abonnements
  const handleGrantSubscription = async () => {
    if (!user) return;
    setSubscriptionLoading(true);
    try {
      await apiService.grantUserSubscription(user.id, subscriptionForm);
      toast.success('Abonnement attribué avec succès');
      setShowSubscriptionModal(false);
      fetchUser();
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Échec de l\'attribution');
    } finally {
      setSubscriptionLoading(false);
    }
  };

  const handleRevokeSubscription = async () => {
    if (!user) return;
    if (!confirm(`Révoquer l'abonnement de ${user.displayName || user.username} ?`)) return;
    
    try {
      await apiService.revokeSubscription(user.id);
      toast.success('Abonnement révoqué');
      fetchUser();
    } catch (error) {
      toast.error('Échec de la révocation');
    }
  };

  const handleExtendSubscription = async (days: number) => {
    if (!user) return;
    try {
      // Utiliser le tier actuel pour prolonger
      const currentTier = subscriptionTiers.find(t => t.name === user.subscriptionType);
      if (currentTier) {
        await apiService.grantSubscription(user.id, currentTier.name, days);
        toast.success(`Abonnement prolongé de ${days} jours`);
        fetchUser();
      }
    } catch (error) {
      toast.error('Échec de la prolongation');
    }
  };

  // Calcul des jours restants
  const getDaysRemaining = () => {
    if (!user?.subscriptionEndDate) return 0;
    const end = new Date(user.subscriptionEndDate);
    const now = new Date();
    const diff = Math.ceil((end.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
    return Math.max(0, diff);
  };

  const isSubscriptionActive = () => {
    return user?.subscriptionType && user.subscriptionType !== 'Free' && getDaysRemaining() > 0;
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
                    value={editForm.roleLevel}
                    onChange={(e) => setEditForm(prev => ({ ...prev, roleLevel: parseInt(e.target.value) }))}
                    className="input"
                  >
                    {roles.length > 0 ? (
                      roles.map((role) => (
                        <option key={role.id} value={role.roleLevel}>
                          {role.displayName}
                        </option>
                      ))
                    ) : (
                      <>
                        <option value={7}>Utilisateur</option>
                        <option value={5}>Modérateur</option>
                        <option value={4}>Admin</option>
                      </>
                    )}
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

          {/* Subscription Management Card */}
          <div className="card">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-white flex items-center gap-2">
                <CreditCardIcon className="w-5 h-5 text-palx-400" />
                Abonnement Membre
              </h3>
              {isSubscriptionActive() && (
                <span className="badge bg-success/20 text-success">Actif</span>
              )}
            </div>

            {/* Current Subscription Status */}
            <div className={`p-4 rounded-xl border ${
              isSubscriptionActive() 
                ? 'bg-gradient-to-r from-palx-600/10 to-warning/10 border-palx-500/30' 
                : 'bg-dark-700/30 border-dark-600/50'
            }`}>
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${
                    user.subscriptionType === 'VIP' 
                      ? 'bg-gradient-to-br from-warning to-amber-600' 
                      : user.subscriptionType === 'Premium'
                        ? 'bg-gradient-to-br from-palx-500 to-palx-700'
                        : 'bg-dark-600'
                  }`}>
                    <SparklesIcon className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <p className="text-white font-semibold text-lg">
                      {user.subscriptionType === 'VIP' ? '👑 VIP' : 
                       user.subscriptionType === 'Premium' ? '⭐ Premium' : 
                       '🆓 Gratuit'}
                    </p>
                    {isSubscriptionActive() ? (
                      <div className="flex items-center gap-2 text-sm">
                        <ClockIcon className="w-4 h-4 text-dark-400" />
                        <span className="text-dark-300">
                          Expire le {new Date(user.subscriptionEndDate!).toLocaleDateString('fr-FR')}
                        </span>
                        <span className={`font-medium ${
                          getDaysRemaining() <= 7 ? 'text-danger' : 
                          getDaysRemaining() <= 30 ? 'text-warning' : 'text-success'
                        }`}>
                          ({getDaysRemaining()} jours restants)
                        </span>
                      </div>
                    ) : (
                      <p className="text-dark-400 text-sm">Aucun abonnement actif</p>
                    )}
                  </div>
                </div>
              </div>
            </div>

            {/* Actions */}
            <div className="mt-4 flex flex-wrap gap-3">
              {!isSubscriptionActive() ? (
                <button 
                  onClick={() => setShowSubscriptionModal(true)}
                  className="btn-primary"
                >
                  <PlusIcon className="w-5 h-5" />
                  Attribuer un abonnement
                </button>
              ) : (
                <>
                  <button 
                    onClick={() => setShowSubscriptionModal(true)}
                    className="btn-primary"
                  >
                    <ArrowPathIcon className="w-5 h-5" />
                    Modifier / Prolonger
                  </button>
                  <div className="flex gap-2">
                    <button 
                      onClick={() => handleExtendSubscription(7)}
                      className="btn-secondary text-sm"
                    >
                      +7 jours
                    </button>
                    <button 
                      onClick={() => handleExtendSubscription(30)}
                      className="btn-secondary text-sm"
                    >
                      +30 jours
                    </button>
                  </div>
                  <button 
                    onClick={handleRevokeSubscription}
                    className="btn-danger"
                  >
                    <XMarkIcon className="w-5 h-5" />
                    Révoquer
                  </button>
                </>
              )}
            </div>

            {/* Subscription Benefits Preview */}
            {isSubscriptionActive() && (
              <div className="mt-4 pt-4 border-t border-dark-700/50">
                <p className="text-dark-400 text-xs mb-2">Avantages actifs :</p>
                <div className="flex flex-wrap gap-2">
                  {user.subscriptionType === 'VIP' && (
                    <>
                      <span className="text-xs px-2 py-1 bg-warning/10 text-warning rounded">Support prioritaire</span>
                      <span className="text-xs px-2 py-1 bg-warning/10 text-warning rounded">Sans publicité</span>
                      <span className="text-xs px-2 py-1 bg-warning/10 text-warning rounded">Salons illimités</span>
                    </>
                  )}
                  {user.subscriptionType === 'Premium' && (
                    <>
                      <span className="text-xs px-2 py-1 bg-palx-500/10 text-palx-400 rounded">Diffusion vidéo</span>
                      <span className="text-xs px-2 py-1 bg-palx-500/10 text-palx-400 rounded">Upload fichiers</span>
                      <span className="text-xs px-2 py-1 bg-palx-500/10 text-palx-400 rounded">Status personnalisé</span>
                    </>
                  )}
                </div>
              </div>
            )}
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

      {/* Subscription Modal */}
      {showSubscriptionModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto">
          <div className="bg-dark-800 border border-dark-700 rounded-2xl max-w-4xl w-full shadow-2xl animate-fade-in my-4">
            {/* Modal Header */}
            <div className="flex items-center justify-between p-5 border-b border-dark-700">
              <div className="flex items-center gap-3">
                <CreditCardIcon className="w-6 h-6 text-palx-400" />
                <div>
                  <h3 className="text-lg font-semibold text-white">
                    {isSubscriptionActive() ? 'Modifier l\'abonnement' : 'Attribuer un abonnement'}
                  </h3>
                  <p className="text-dark-400 text-sm">{user.displayName || user.username} (@{user.username})</p>
                </div>
              </div>
              <button
                onClick={() => setShowSubscriptionModal(false)}
                className="p-2 text-dark-400 hover:text-white hover:bg-dark-700 rounded-lg"
              >
                <XMarkIcon className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-5">
              {/* Tier Selection - Grid 5 colonnes */}
              <div className="mb-4">
                <label className="label mb-2">Type d'abonnement ({subscriptionTiers.length} disponibles)</label>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2 max-h-64 overflow-y-auto pr-1">
                  {subscriptionTiers.map((tier) => (
                    <button
                      key={tier.id}
                      type="button"
                      onClick={() => setSubscriptionForm(prev => ({ ...prev, tierId: tier.id }))}
                      className={`p-3 rounded-xl border-2 transition-all text-left ${
                        subscriptionForm.tierId === tier.id
                          ? 'border-palx-500 bg-palx-500/10'
                          : 'border-dark-600 bg-dark-700/30 hover:border-dark-500'
                      }`}
                    >
                      <div className="flex items-center gap-1.5 mb-1">
                        <span 
                          className="w-2 h-2 rounded-full flex-shrink-0"
                          style={{ backgroundColor: tier.color || '#8B5CF6' }}
                        />
                        <span 
                          className="font-semibold text-sm truncate"
                          style={{ color: tier.color || '#8B5CF6' }}
                        >
                          {tier.displayName}
                        </span>
                      </div>
                      <p className="text-dark-400 text-xs">
                        {tier.maxRoomsOwned > 0 ? `${tier.maxRoomsOwned} salons` : 'Fonctionnalités étendues'}
                      </p>
                    </button>
                  ))}
                </div>
              </div>

              {/* Duration & Payment Method - Side by side */}
              <div className="grid grid-cols-2 gap-4 mb-4">
                <div>
                  <label className="label">Durée</label>
                  <select
                    value={subscriptionForm.durationId}
                    onChange={(e) => setSubscriptionForm(prev => ({ ...prev, durationId: parseInt(e.target.value) }))}
                    className="input"
                  >
                    {subscriptionDurations.map((duration) => (
                      <option key={duration.id} value={duration.id}>
                        {duration.displayName} ({duration.days} jours)
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="label">Méthode</label>
                  <select
                    value={subscriptionForm.paymentMethod}
                    onChange={(e) => setSubscriptionForm(prev => ({ ...prev, paymentMethod: e.target.value }))}
                    className="input"
                  >
                    <option value="admin_grant">Attribution Admin (Gratuit)</option>
                    <option value="gift">Cadeau</option>
                    <option value="compensation">Compensation</option>
                    <option value="promotion">Promotion</option>
                  </select>
                </div>
              </div>

              {/* Info */}
              <div className="p-3 bg-info/10 border border-info/30 rounded-lg text-sm">
                <p className="text-info">
                  💡 L'abonnement sera attribué immédiatement. Si l'utilisateur a déjà un abonnement actif, la durée sera ajoutée.
                </p>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="flex gap-3 p-5 border-t border-dark-700">
              <button
                onClick={() => setShowSubscriptionModal(false)}
                className="btn-secondary flex-1"
                disabled={subscriptionLoading}
              >
                Annuler
              </button>
              <button
                onClick={handleGrantSubscription}
                className="btn-primary flex-1"
                disabled={subscriptionLoading}
              >
                {subscriptionLoading ? (
                  <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                ) : (
                  <>
                    <CheckIcon className="w-5 h-5" />
                    Confirmer
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default UserDetailPage;
