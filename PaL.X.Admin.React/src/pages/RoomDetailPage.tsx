import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeftIcon,
  UserIcon,
  UsersIcon,
  CalendarIcon,
  ChatBubbleLeftIcon,
  LockClosedIcon,
  LockOpenIcon,
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
  BuildingOffice2Icon,
  TagIcon,
  Cog6ToothIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { Room } from '../types';
import toast from 'react-hot-toast';

// Interface pour les tiers d'abonnement salon
interface RoomSubscriptionTier {
  id: number;
  name: string;
  displayName: string;
  color: string;
  maxMembers: number;
  maxModerators: number;
  canBroadcast: boolean;
  canUploadFiles: boolean;
  maxFileSize: number;
  customEmojis: boolean;
  priorityListing: boolean;
  adFree: boolean;
}

// Interface pour les durées d'abonnement
interface SubscriptionDuration {
  id: number;
  name: string;
  days: number;
  displayName: string;
}

const RoomDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [room, setRoom] = useState<Room | null>(null);
  const [loading, setLoading] = useState(true);
  
  // États pour la gestion des abonnements
  const [subscriptionTiers, setSubscriptionTiers] = useState<RoomSubscriptionTier[]>([]);
  const [subscriptionDurations, setSubscriptionDurations] = useState<SubscriptionDuration[]>([]);
  const [showSubscriptionModal, setShowSubscriptionModal] = useState(false);
  const [subscriptionForm, setSubscriptionForm] = useState({
    tierId: 1,
    durationDays: 30,
    paymentMethod: 'admin_grant'
  });
  const [subscriptionLoading, setSubscriptionLoading] = useState(false);

  useEffect(() => {
    fetchRoom();
    fetchSubscriptionData();
  }, [id]);

  const fetchSubscriptionData = async () => {
    try {
      const tiers = await apiService.getRoomSubscriptionTiers();
      // Filtrer pour exclure "Basic" (gratuit) des tiers assignables
      const paidTiers = tiers.filter((t: RoomSubscriptionTier) => t.name !== 'Basic' && t.name !== 'Free');
      setSubscriptionTiers(paidTiers);
      if (paidTiers.length > 0) {
        setSubscriptionForm(prev => ({ ...prev, tierId: paidTiers[0].id }));
      }
      // Durées par défaut pour les salons
      setSubscriptionDurations([
        { id: 1, name: '1week', days: 7, displayName: '1 Semaine' },
        { id: 2, name: '1month', days: 30, displayName: '1 Mois' },
        { id: 3, name: '3months', days: 90, displayName: '3 Mois' },
        { id: 4, name: '6months', days: 180, displayName: '6 Mois' },
        { id: 5, name: '1year', days: 365, displayName: '1 An' },
      ]);
    } catch (error) {
      console.error('Failed to fetch subscription data:', error);
      // Données par défaut basées sur la vraie liste
      setSubscriptionTiers([
        { id: 2, name: 'Deluxe', displayName: 'Deluxe', color: '#22C55E', maxMembers: 100, maxModerators: 1, canBroadcast: false, canUploadFiles: false, maxFileSize: 0, customEmojis: false, priorityListing: false, adFree: false },
        { id: 3, name: 'Extreme', displayName: 'Extreme', color: '#EAB308', maxMembers: 250, maxModerators: 2, canBroadcast: true, canUploadFiles: false, maxFileSize: 0, customEmojis: false, priorityListing: false, adFree: false },
        { id: 4, name: 'VIP', displayName: 'VIP', color: '#F97316', maxMembers: 500, maxModerators: 2, canBroadcast: true, canUploadFiles: true, maxFileSize: 10, customEmojis: false, priorityListing: false, adFree: true },
        { id: 5, name: 'Bronze', displayName: 'Bronze', color: '#CD7F32', maxMembers: 500, maxModerators: 3, canBroadcast: true, canUploadFiles: true, maxFileSize: 12, customEmojis: false, priorityListing: false, adFree: true },
        { id: 6, name: 'Silver', displayName: 'Silver', color: '#9CA3AF', maxMembers: 1000, maxModerators: 3, canBroadcast: true, canUploadFiles: true, maxFileSize: 14, customEmojis: false, priorityListing: true, adFree: true },
        { id: 7, name: 'Gold', displayName: 'Gold', color: '#FFD700', maxMembers: 1500, maxModerators: 4, canBroadcast: true, canUploadFiles: true, maxFileSize: 20, customEmojis: true, priorityListing: true, adFree: true },
        { id: 8, name: 'Platinum', displayName: 'Platinum', color: '#6B7280', maxMembers: 2000, maxModerators: 4, canBroadcast: true, canUploadFiles: true, maxFileSize: 30, customEmojis: true, priorityListing: true, adFree: true },
        { id: 9, name: 'Ultimate', displayName: 'Ultimate', color: '#EF4444', maxMembers: 5000, maxModerators: 5, canBroadcast: true, canUploadFiles: true, maxFileSize: 50, customEmojis: true, priorityListing: true, adFree: true },
        { id: 10, name: 'Legend', displayName: 'Legend', color: '#F97316', maxMembers: 10000, maxModerators: 5, canBroadcast: true, canUploadFiles: true, maxFileSize: 100, customEmojis: true, priorityListing: true, adFree: true },
      ]);
    }
  };

  const fetchRoom = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const data = await apiService.getRoomById(parseInt(id));
      setRoom(data);
    } catch (error) {
      console.error('Failed to fetch room:', error);
      // Mock data for demo
      setRoom({
        id: parseInt(id),
        name: 'Salon Principal',
        description: 'Le salon principal de discussion de la communauté PaL.Xtreme',
        ownerId: 1,
        ownerUsername: 'admin',
        ownerDisplayName: 'Admin System',
        createdAt: '2024-01-01T10:00:00Z',
        isActive: true,
        currentUsers: 45,
        maxUsers: 100,
        isPrivate: false,
        hasPassword: false,
        category: 'General',
        bannedUsers: [],
        tags: ['chat', 'communauté', 'général'],
        subscriptionType: 'Premium',
        subscriptionEndDate: '2025-06-15',
      });
    } finally {
      setLoading(false);
    }
  };

  // Gestion des abonnements
  const handleGrantSubscription = async () => {
    if (!room) return;
    setSubscriptionLoading(true);
    try {
      await apiService.grantRoomSubscription({
        roomId: room.id,
        tierId: subscriptionForm.tierId,
        durationDays: subscriptionForm.durationDays
      });
      toast.success('Abonnement attribué avec succès');
      setShowSubscriptionModal(false);
      fetchRoom();
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Échec de l\'attribution');
    } finally {
      setSubscriptionLoading(false);
    }
  };

  const handleRevokeSubscription = async () => {
    if (!room) return;
    if (!confirm(`Révoquer l'abonnement du salon "${room.name}" ?`)) return;
    
    try {
      await apiService.revokeRoomSubscription(room.id);
      toast.success('Abonnement révoqué');
      fetchRoom();
    } catch (error) {
      toast.error('Échec de la révocation');
    }
  };

  const handleExtendSubscription = async (days: number) => {
    if (!room) return;
    try {
      await apiService.extendRoomSubscription(room.id, days);
      toast.success(`Abonnement prolongé de ${days} jours`);
      fetchRoom();
    } catch (error) {
      toast.error('Échec de la prolongation');
    }
  };

  const handleCloseRoom = async () => {
    if (!room) return;
    const reason = prompt('Raison de la fermeture (optionnel):');
    try {
      await apiService.closeRoom(room.id, reason || undefined);
      toast.success('Salon fermé');
      fetchRoom();
    } catch (error) {
      toast.error('Échec de la fermeture');
    }
  };

  const handleDeleteRoom = async () => {
    if (!room) return;
    if (!confirm(`Êtes-vous sûr de vouloir supprimer le salon "${room.name}" ? Cette action est irréversible.`)) return;
    
    try {
      await apiService.deleteRoom(room.id);
      toast.success('Salon supprimé');
      navigate('/rooms');
    } catch (error) {
      toast.error('Échec de la suppression');
    }
  };

  // Calcul des jours restants
  const getDaysRemaining = () => {
    if (!room?.subscriptionEndDate) return 0;
    const end = new Date(room.subscriptionEndDate);
    const now = new Date();
    const diff = Math.ceil((end.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
    return Math.max(0, diff);
  };

  const isSubscriptionActive = () => {
    return room?.subscriptionType && room.subscriptionType !== 'Free' && getDaysRemaining() > 0;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  if (!room) {
    return (
      <div className="text-center py-12">
        <p className="text-dark-400">Salon non trouvé</p>
        <Link to="/rooms" className="text-palx-400 hover:text-palx-300 mt-4 inline-block">
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
          onClick={() => navigate('/rooms')}
          className="p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
        >
          <ArrowLeftIcon className="w-5 h-5" />
        </button>
        <h1 className="text-2xl font-bold text-white">Détails du salon</h1>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Room Card */}
        <div className="lg:col-span-1">
          <div className="card text-center">
            {/* Room Icon */}
            <div className={`w-24 h-24 mx-auto rounded-2xl flex items-center justify-center shadow-glow mb-4 ${
              room.subscriptionType === 'VIP' 
                ? 'bg-gradient-to-br from-warning to-amber-600' 
                : room.subscriptionType === 'Premium'
                  ? 'bg-gradient-to-br from-palx-500 to-palx-700'
                  : 'bg-gradient-to-br from-dark-600 to-dark-700'
            }`}>
              <ChatBubbleLeftIcon className="w-12 h-12 text-white" />
            </div>

            {/* Room Name */}
            <h2 className="text-2xl font-bold text-white">{room.name}</h2>
            
            {/* Status badges */}
            <div className="flex items-center justify-center gap-2 mt-3 flex-wrap">
              <span className={`badge ${
                room.isActive ? 'bg-success/20 text-success' : 'bg-dark-600/50 text-dark-400'
              }`}>
                {room.isActive ? '● Actif' : '○ Fermé'}
              </span>
              {room.isPrivate && (
                <span className="badge bg-warning/20 text-warning">
                  <LockClosedIcon className="w-3 h-3 mr-1" />
                  Privé
                </span>
              )}
              <span className={`badge ${
                room.subscriptionType === 'VIP' ? 'bg-warning/20 text-warning' :
                room.subscriptionType === 'Premium' ? 'bg-palx-500/20 text-palx-400' :
                'bg-dark-600/50 text-dark-300'
              }`}>
                {room.subscriptionType || 'Free'}
              </span>
            </div>

            {/* Description */}
            {room.description && (
              <p className="mt-4 text-dark-300 text-sm">{room.description}</p>
            )}

            {/* Tags */}
            {room.tags && room.tags.length > 0 && (
              <div className="flex flex-wrap justify-center gap-1 mt-4">
                {room.tags.map((tag, i) => (
                  <span key={i} className="px-2 py-0.5 bg-dark-700/50 rounded text-xs text-dark-300">
                    #{tag}
                  </span>
                ))}
              </div>
            )}

            {/* Quick actions */}
            <div className="mt-6 pt-6 border-t border-dark-700/50 space-y-2">
              {room.isActive ? (
                <button onClick={handleCloseRoom} className="btn-warning w-full">
                  <XMarkIcon className="w-5 h-5" />
                  Fermer le salon
                </button>
              ) : (
                <button className="btn-success w-full">
                  <CheckIcon className="w-5 h-5" />
                  Réactiver le salon
                </button>
              )}
              <button onClick={handleDeleteRoom} className="btn-danger w-full">
                <TrashIcon className="w-5 h-5" />
                Supprimer
              </button>
            </div>
          </div>
        </div>

        {/* Details */}
        <div className="lg:col-span-2 space-y-6">
          {/* Info */}
          <div className="card">
            <h3 className="text-lg font-semibold text-white mb-4">Informations</h3>
            <div className="space-y-4">
              <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                <UserIcon className="w-5 h-5 text-dark-400" />
                <div>
                  <p className="text-dark-400 text-xs">Propriétaire</p>
                  <Link to={`/users/${room.ownerId}`} className="text-palx-400 hover:text-palx-300">
                    {room.ownerDisplayName || room.ownerUsername}
                  </Link>
                </div>
              </div>
              <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                <CalendarIcon className="w-5 h-5 text-dark-400" />
                <div>
                  <p className="text-dark-400 text-xs">Créé le</p>
                  <p className="text-white">{new Date(room.createdAt).toLocaleDateString('fr-FR', { dateStyle: 'long' })}</p>
                </div>
              </div>
              <div className="flex items-center gap-3 p-3 bg-dark-700/30 rounded-lg">
                <TagIcon className="w-5 h-5 text-dark-400" />
                <div>
                  <p className="text-dark-400 text-xs">Catégorie</p>
                  <p className="text-white">{room.category}</p>
                </div>
              </div>
            </div>
          </div>

          {/* Statistics */}
          <div className="card">
            <h3 className="text-lg font-semibold text-white mb-4">Statistiques</h3>
            <div className="grid grid-cols-3 gap-4">
              <div className="text-center p-4 bg-dark-700/30 rounded-lg">
                <UsersIcon className="w-6 h-6 text-palx-400 mx-auto mb-2" />
                <p className="text-2xl font-bold text-white">{room.currentUsers}</p>
                <p className="text-dark-400 text-sm">En ligne</p>
              </div>
              <div className="text-center p-4 bg-dark-700/30 rounded-lg">
                <UsersIcon className="w-6 h-6 text-info mx-auto mb-2" />
                <p className="text-2xl font-bold text-white">{room.maxUsers}</p>
                <p className="text-dark-400 text-sm">Capacité max</p>
              </div>
              <div className="text-center p-4 bg-dark-700/30 rounded-lg">
                <ExclamationTriangleIcon className="w-6 h-6 text-warning mx-auto mb-2" />
                <p className="text-2xl font-bold text-white">{room.bannedUsers?.length || 0}</p>
                <p className="text-dark-400 text-sm">Bannis</p>
              </div>
            </div>
            {/* Capacity bar */}
            <div className="mt-4">
              <div className="flex justify-between text-sm mb-1">
                <span className="text-dark-400">Capacité utilisée</span>
                <span className="text-white">{Math.round((room.currentUsers / room.maxUsers) * 100)}%</span>
              </div>
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
          </div>

          {/* Subscription Management Card */}
          <div className="card">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold text-white flex items-center gap-2">
                <BuildingOffice2Icon className="w-5 h-5 text-palx-400" />
                Abonnement Salon
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
                    room.subscriptionType === 'VIP' 
                      ? 'bg-gradient-to-br from-warning to-amber-600' 
                      : room.subscriptionType === 'Premium'
                        ? 'bg-gradient-to-br from-palx-500 to-palx-700'
                        : 'bg-dark-600'
                  }`}>
                    <BuildingOffice2Icon className="w-6 h-6 text-white" />
                  </div>
                  <div>
                    <p className="text-white font-semibold text-lg">
                      {room.subscriptionType === 'VIP' ? '👑 VIP' : 
                       room.subscriptionType === 'Premium' ? '⭐ Premium' : 
                       '🆓 Gratuit'}
                    </p>
                    {isSubscriptionActive() ? (
                      <div className="flex items-center gap-2 text-sm">
                        <ClockIcon className="w-4 h-4 text-dark-400" />
                        <span className="text-dark-300">
                          Expire le {new Date(room.subscriptionEndDate!).toLocaleDateString('fr-FR')}
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
                  {room.subscriptionType === 'VIP' && (
                    <>
                      <span className="text-xs px-2 py-1 bg-warning/10 text-warning rounded">500 membres max</span>
                      <span className="text-xs px-2 py-1 bg-warning/10 text-warning rounded">Priorité listing</span>
                      <span className="text-xs px-2 py-1 bg-warning/10 text-warning rounded">Emojis custom</span>
                    </>
                  )}
                  {room.subscriptionType === 'Premium' && (
                    <>
                      <span className="text-xs px-2 py-1 bg-palx-500/10 text-palx-400 rounded">200 membres max</span>
                      <span className="text-xs px-2 py-1 bg-palx-500/10 text-palx-400 rounded">Diffusion vidéo</span>
                      <span className="text-xs px-2 py-1 bg-palx-500/10 text-palx-400 rounded">Upload fichiers</span>
                    </>
                  )}
                </div>
              </div>
            )}
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
                <BuildingOffice2Icon className="w-6 h-6 text-palx-400" />
                <div>
                  <h3 className="text-lg font-semibold text-white">
                    {isSubscriptionActive() ? 'Modifier l\'abonnement' : 'Attribuer un abonnement'}
                  </h3>
                  <p className="text-dark-400 text-sm">{room.name}</p>
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
                        {tier.maxMembers.toLocaleString()} membres
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
                    value={subscriptionForm.durationDays}
                    onChange={(e) => setSubscriptionForm(prev => ({ ...prev, durationDays: parseInt(e.target.value) }))}
                    className="input"
                  >
                    {subscriptionDurations.map((duration) => (
                      <option key={duration.id} value={duration.days}>
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
                  💡 L'abonnement sera attribué immédiatement. Si le salon a déjà un abonnement actif, la durée sera ajoutée.
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

export default RoomDetailPage;
