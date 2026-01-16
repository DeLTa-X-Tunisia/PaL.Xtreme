import React, { useEffect, useState } from 'react';
import { 
  BuildingOffice2Icon, 
  Cog6ToothIcon, 
  MagnifyingGlassIcon, 
  ArrowPathIcon, 
  UsersIcon, 
  CheckIcon, 
  XMarkIcon, 
  ClockIcon, 
  ExclamationCircleIcon, 
  PencilSquareIcon,
  GiftIcon,
  MicrophoneIcon, 
  VideoCameraIcon, 
  LockClosedIcon, 
  ShieldCheckIcon, 
  PaintBrushIcon, 
  StarIcon, 
  CpuChipIcon, 
  WifiIcon,
  SparklesIcon
} from '@heroicons/react/24/outline';
import { apiService } from '../services/api';

interface RoomSubscriptionTier {
  id: number;
  tier: number;
  name: string;
  description: string | null;
  color: string;
  icon: string | null;
  maxUsers: number;
  maxModerators: number;
  maxAdmins: number;
  maxMic: number;
  maxCam: number;
  canHavePassword: boolean;
  canBe18Plus: boolean;
  canHaveSubRooms: boolean;
  maxSubRooms: number;
  canCustomizeBanner: boolean;
  canCustomizeBackground: boolean;
  hasPriorityListing: boolean;
  canUseBot: boolean;
  storageLimitMB: number;
  alwaysOnline: boolean;
  monthlyPriceCents: number;
  yearlyPriceCents: number;
  isAvailable: boolean;
  createdAt: string;
  updatedAt: string | null;
  activeSubscriptions: number;
}

interface RoomSubscription {
  id: number;
  roomId: number;
  tierId: number;
  purchasedBy: number | null;
  startedAt: string | null;
  expiresAt: string | null;
  isActive: boolean;
  autoRenew: boolean;
  paymentMethod: string | null;
  transactionId: string | null;
  createdAt: string | null;
  updatedAt: string | null;
  roomName: string;
  tierName: string;
  tierColor: string;
  purchasedByUsername: string | null;
}

interface RoomSearchResult {
  id: number;
  name: string;
  ownerId: number | null;
  createdAt: string | null;
  ownerUsername: string | null;
  currentTierId: number | null;
  currentTierName: string | null;
  currentTierColor: string | null;
  hasActiveSubscription: boolean;
  subscriptionExpiresAt: string | null;
}

interface RoomSubscriptionStats {
  totalTiers: number;
  activeSubscriptions: number;
  expiringSoon: number;
  subscriptionsByTier: { tierName: string; tierColor: string; count: number }[];
}

type TabType = 'overview' | 'tiers' | 'rooms';

const RoomSubscriptionsPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<TabType>('overview');
  const [tiers, setTiers] = useState<RoomSubscriptionTier[]>([]);
  const [subscriptions, setSubscriptions] = useState<RoomSubscription[]>([]);
  const [roomSearchResults, setRoomSearchResults] = useState<RoomSearchResult[]>([]);
  const [stats, setStats] = useState<RoomSubscriptionStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editingTier, setEditingTier] = useState<number | null>(null);
  const [editForm, setEditForm] = useState<Partial<RoomSubscriptionTier>>({});
  const [roomSearch, setRoomSearch] = useState('');
  const [selectedRoom, setSelectedRoom] = useState<RoomSearchResult | null>(null);
  const [grantTierId, setGrantTierId] = useState<number | null>(null);
  const [grantDuration, setGrantDuration] = useState<number>(30);
  const [showGrantModal, setShowGrantModal] = useState(false);
  const [extendDays, setExtendDays] = useState<number>(30);
  const [showExtendModal, setShowExtendModal] = useState<number | null>(null);

  useEffect(() => {
    loadData();
  }, [activeTab]);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      if (activeTab === 'overview') {
        const [statsData, tiersData] = await Promise.all([
          apiService.getRoomSubscriptionStats(),
          apiService.getRoomSubscriptionTiers()
        ]);
        setStats(statsData);
        setTiers(tiersData);
      } else if (activeTab === 'tiers') {
        const tiersData = await apiService.getRoomSubscriptionTiers();
        setTiers(tiersData);
      } else if (activeTab === 'rooms') {
        const [subsData, tiersData] = await Promise.all([
          apiService.getRoomSubscriptions(),
          apiService.getRoomSubscriptionTiers()
        ]);
        setSubscriptions(subsData);
        setTiers(tiersData);
      }
    } catch (err: any) {
      setError(err.response?.data?.error || err.message || 'Erreur lors du chargement');
    } finally {
      setLoading(false);
    }
  };

  const handleSearchRooms = async () => {
    try {
      const results = await apiService.searchRoomsForSubscription(roomSearch || undefined, 50);
      setRoomSearchResults(results);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erreur de recherche');
    }
  };

  const handleEditTier = (tier: RoomSubscriptionTier) => {
    setEditingTier(tier.id);
    setEditForm({ ...tier });
  };

  const handleSaveTier = async () => {
    if (!editingTier || !editForm) return;
    try {
      await apiService.updateRoomSubscriptionTier(editingTier, editForm);
      setEditingTier(null);
      setEditForm({});
      await loadData();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erreur lors de la mise à jour');
    }
  };

  const handleGrantSubscription = async () => {
    if (!selectedRoom || !grantTierId) return;
    try {
      await apiService.grantRoomSubscription({
        roomId: selectedRoom.id,
        tierId: grantTierId,
        durationDays: grantDuration > 0 ? grantDuration : undefined
      });
      setShowGrantModal(false);
      setSelectedRoom(null);
      setGrantTierId(null);
      setGrantDuration(30);
      await handleSearchRooms();
      await loadData();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erreur lors de l\'attribution');
    }
  };

  const handleRevokeSubscription = async (roomId: number) => {
    if (!confirm('Êtes-vous sûr de vouloir révoquer cet abonnement ?')) return;
    try {
      await apiService.revokeRoomSubscription(roomId);
      await handleSearchRooms();
      await loadData();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erreur lors de la révocation');
    }
  };

  const handleExtendSubscription = async (roomId: number) => {
    try {
      await apiService.extendRoomSubscription(roomId, extendDays);
      setShowExtendModal(null);
      setExtendDays(30);
      await handleSearchRooms();
      await loadData();
    } catch (err: any) {
      setError(err.response?.data?.error || 'Erreur lors de l\'extension');
    }
  };

  const formatPrice = (cents: number): string => {
    return (cents / 100).toFixed(2) + ' €';
  };

  const formatDate = (dateString: string | null): string => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleDateString('fr-FR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  };

  const renderOverview = () => (
    <div className="space-y-6">
      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-sm border border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-4">
            <div className="p-3 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
              <SparklesIcon className="w-6 h-6 text-purple-600 dark:text-purple-400" />
            </div>
            <div>
              <p className="text-sm text-gray-500 dark:text-gray-400">Tiers disponibles</p>
              <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats?.totalTiers || 0}</p>
            </div>
          </div>
        </div>

        <div className="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-sm border border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-4">
            <div className="p-3 bg-green-100 dark:bg-green-900/30 rounded-lg">
              <BuildingOffice2Icon className="w-6 h-6 text-green-600 dark:text-green-400" />
            </div>
            <div>
              <p className="text-sm text-gray-500 dark:text-gray-400">Salons abonnés</p>
              <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats?.activeSubscriptions || 0}</p>
            </div>
          </div>
        </div>

        <div className="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-sm border border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-4">
            <div className="p-3 bg-orange-100 dark:bg-orange-900/30 rounded-lg">
              <ClockIcon className="w-6 h-6 text-orange-600 dark:text-orange-400" />
            </div>
            <div>
              <p className="text-sm text-gray-500 dark:text-gray-400">Expirent bientôt</p>
              <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats?.expiringSoon || 0}</p>
            </div>
          </div>
        </div>

        <div className="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-sm border border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-4">
            <div className="p-3 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
              <ExclamationCircleIcon className="w-6 h-6 text-blue-600 dark:text-blue-400" />
            </div>
            <div>
              <p className="text-sm text-gray-500 dark:text-gray-400">À renouveler</p>
              <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats?.expiringSoon || 0}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Distribution par Tier */}
      <div className="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-sm border border-gray-200 dark:border-gray-700">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Distribution des abonnements</h3>
        <div className="space-y-3">
          {stats?.subscriptionsByTier?.map((item) => (
            <div key={item.tierName} className="flex items-center gap-4">
              <div 
                className="w-4 h-4 rounded-full flex-shrink-0"
                style={{ backgroundColor: item.tierColor }}
              />
              <span className="text-sm font-medium text-gray-700 dark:text-gray-300 w-24">{item.tierName}</span>
              <div className="flex-1 bg-gray-200 dark:bg-gray-700 rounded-full h-3">
                <div 
                  className="h-3 rounded-full transition-all duration-300"
                  style={{ 
                    backgroundColor: item.tierColor,
                    width: `${stats.activeSubscriptions > 0 ? (item.count / stats.activeSubscriptions) * 100 : 0}%`
                  }}
                />
              </div>
              <span className="text-sm font-semibold text-gray-900 dark:text-white w-12 text-right">{item.count}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Liste des Tiers */}
      <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Tiers d'abonnement salon</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-gray-200 dark:border-gray-700">
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Tier</th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Capacité</th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Fonctionnalités</th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Prix mensuel</th>
                <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Prix annuel</th>
                <th className="text-center py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Actifs</th>
              </tr>
            </thead>
            <tbody>
              {tiers.map((tier) => (
                <tr key={tier.id} className="border-b border-gray-100 dark:border-gray-700/50 hover:bg-gray-50 dark:hover:bg-gray-700/30">
                  <td className="py-4 px-4">
                    <div className="flex items-center gap-3">
                      <div 
                        className="w-3 h-3 rounded-full"
                        style={{ backgroundColor: tier.color }}
                      />
                      <div>
                        <p className="font-medium text-gray-900 dark:text-white">{tier.name}</p>
                        <p className="text-xs text-gray-500 dark:text-gray-400">{tier.description}</p>
                      </div>
                    </div>
                  </td>
                  <td className="py-4 px-4">
                    <div className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
                      <UsersIcon className="w-4 h-4" />
                      <span>{tier.maxUsers}</span>
                      <MicrophoneIcon className="w-4 h-4 ml-2" />
                      <span>{tier.maxMic}</span>
                      <VideoCameraIcon className="w-4 h-4 ml-2" />
                      <span>{tier.maxCam}</span>
                    </div>
                  </td>
                  <td className="py-4 px-4">
                    <div className="flex flex-wrap gap-1">
                      {tier.canHavePassword && <span className="px-2 py-0.5 text-xs bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400 rounded">🔒 MdP</span>}
                      {tier.canBe18Plus && <span className="px-2 py-0.5 text-xs bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400 rounded">🔞 18+</span>}
                      {tier.canUseBot && <span className="px-2 py-0.5 text-xs bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400 rounded">🤖 Bot</span>}
                      {tier.alwaysOnline && <span className="px-2 py-0.5 text-xs bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 rounded">🌐 24/7</span>}
                    </div>
                  </td>
                  <td className="py-4 px-4">
                    <span className="font-semibold text-gray-900 dark:text-white">{formatPrice(tier.monthlyPriceCents)}</span>
                  </td>
                  <td className="py-4 px-4">
                    <span className="font-semibold text-gray-900 dark:text-white">{formatPrice(tier.yearlyPriceCents)}</span>
                  </td>
                  <td className="py-4 px-4 text-center">
                    <span className="px-2 py-1 text-sm font-semibold rounded-full" style={{ backgroundColor: tier.color + '20', color: tier.color }}>
                      {tier.activeSubscriptions}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );

  const renderTiers = () => (
    <div className="space-y-4">
      {tiers.map((tier) => (
        <div key={tier.id} className="bg-white dark:bg-gray-800 rounded-xl p-6 shadow-sm border border-gray-200 dark:border-gray-700">
          {editingTier === tier.id ? (
            <div className="space-y-4">
              {/* Basic Info */}
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Nom</label>
                  <input
                    type="text"
                    value={editForm.name || ''}
                    onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Couleur</label>
                  <input
                    type="color"
                    value={editForm.color || '#3498DB'}
                    onChange={(e) => setEditForm({ ...editForm, color: e.target.value })}
                    className="w-full h-10 rounded-lg cursor-pointer"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Prix mensuel (cents)</label>
                  <input
                    type="number"
                    value={editForm.monthlyPriceCents || 0}
                    onChange={(e) => setEditForm({ ...editForm, monthlyPriceCents: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Prix annuel (cents)</label>
                  <input
                    type="number"
                    value={editForm.yearlyPriceCents || 0}
                    onChange={(e) => setEditForm({ ...editForm, yearlyPriceCents: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
              </div>

              {/* Capacity */}
              <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Users</label>
                  <input
                    type="number"
                    value={editForm.maxUsers || 0}
                    onChange={(e) => setEditForm({ ...editForm, maxUsers: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Mods</label>
                  <input
                    type="number"
                    value={editForm.maxModerators || 0}
                    onChange={(e) => setEditForm({ ...editForm, maxModerators: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Admins</label>
                  <input
                    type="number"
                    value={editForm.maxAdmins || 0}
                    onChange={(e) => setEditForm({ ...editForm, maxAdmins: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Mic</label>
                  <input
                    type="number"
                    value={editForm.maxMic || 0}
                    onChange={(e) => setEditForm({ ...editForm, maxMic: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Cam</label>
                  <input
                    type="number"
                    value={editForm.maxCam || 0}
                    onChange={(e) => setEditForm({ ...editForm, maxCam: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
              </div>

              {/* Sub-rooms and storage */}
              <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Sub-Rooms</label>
                  <input
                    type="number"
                    value={editForm.maxSubRooms || 0}
                    onChange={(e) => setEditForm({ ...editForm, maxSubRooms: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Stockage (MB)</label>
                  <input
                    type="number"
                    value={editForm.storageLimitMB || 0}
                    onChange={(e) => setEditForm({ ...editForm, storageLimitMB: parseInt(e.target.value) })}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
              </div>

              {/* Features toggles */}
              <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                {[
                  { key: 'canHavePassword', label: 'Mot de passe', Icon: LockClosedIcon },
                  { key: 'canBe18Plus', label: '18+', Icon: ShieldCheckIcon },
                  { key: 'canHaveSubRooms', label: 'Sub-Rooms', Icon: BuildingOffice2Icon },
                  { key: 'canCustomizeBanner', label: 'Bannière', Icon: PaintBrushIcon },
                  { key: 'canCustomizeBackground', label: 'Fond', Icon: PaintBrushIcon },
                  { key: 'hasPriorityListing', label: 'Priorité', Icon: StarIcon },
                  { key: 'canUseBot', label: 'Bot', Icon: CpuChipIcon },
                  { key: 'alwaysOnline', label: '24/7', Icon: WifiIcon },
                  { key: 'isAvailable', label: 'Disponible', Icon: CheckIcon },
                ].map(({ key, label, Icon }) => (
                  <label key={key} className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={(editForm as any)[key] || false}
                      onChange={(e) => setEditForm({ ...editForm, [key]: e.target.checked })}
                      className="w-4 h-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                    <Icon className="w-4 h-4 text-gray-500" />
                    <span className="text-sm text-gray-700 dark:text-gray-300">{label}</span>
                  </label>
                ))}
              </div>

              {/* Actions */}
              <div className="flex gap-2 pt-4 border-t border-gray-200 dark:border-gray-700">
                <button
                  onClick={handleSaveTier}
                  className="flex items-center gap-2 px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700"
                >
                  <CheckIcon className="w-4 h-4" />
                  Enregistrer
                </button>
                <button
                  onClick={() => { setEditingTier(null); setEditForm({}); }}
                  className="flex items-center gap-2 px-4 py-2 bg-gray-500 text-white rounded-lg hover:bg-gray-600"
                >
                  <XMarkIcon className="w-4 h-4" />
                  Annuler
                </button>
              </div>
            </div>
          ) : (
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div 
                  className="w-12 h-12 rounded-xl flex items-center justify-center text-white font-bold text-xl"
                  style={{ backgroundColor: tier.color }}
                >
                  {tier.tier}
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-white">{tier.name}</h3>
                  <p className="text-sm text-gray-500 dark:text-gray-400">{tier.description}</p>
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-4">
                <div className="text-center">
                  <p className="text-xs text-gray-500 dark:text-gray-400">Capacité</p>
                  <p className="font-semibold text-gray-900 dark:text-white">{tier.maxUsers} users</p>
                </div>
                <div className="text-center">
                  <p className="text-xs text-gray-500 dark:text-gray-400">Mensuel</p>
                  <p className="font-semibold text-gray-900 dark:text-white">{formatPrice(tier.monthlyPriceCents)}</p>
                </div>
                <div className="text-center">
                  <p className="text-xs text-gray-500 dark:text-gray-400">Annuel</p>
                  <p className="font-semibold text-gray-900 dark:text-white">{formatPrice(tier.yearlyPriceCents)}</p>
                </div>
                <div className="text-center">
                  <p className="text-xs text-gray-500 dark:text-gray-400">Actifs</p>
                  <p className="font-semibold text-gray-900 dark:text-white">{tier.activeSubscriptions}</p>
                </div>
                <span className={`px-3 py-1 rounded-full text-sm font-medium ${tier.isAvailable ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400' : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'}`}>
                  {tier.isAvailable ? 'Disponible' : 'Indisponible'}
                </span>
                <button
                  onClick={() => handleEditTier(tier)}
                  className="p-2 text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/30 rounded-lg"
                >
                  <PencilSquareIcon className="w-5 h-5" />
                </button>
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );

  const renderRooms = () => (
    <div className="space-y-6">
      {/* Search bar */}
      <div className="bg-white dark:bg-gray-800 rounded-xl p-4 shadow-sm border border-gray-200 dark:border-gray-700">
        <div className="flex gap-4">
          <div className="flex-1 relative">
            <MagnifyingGlassIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-400" />
            <input
              type="text"
              placeholder="Rechercher un salon par nom ou ID..."
              value={roomSearch}
              onChange={(e) => setRoomSearch(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSearchRooms()}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          <button
            onClick={handleSearchRooms}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            <MagnifyingGlassIcon className="w-4 h-4" />
            Rechercher
          </button>
        </div>
      </div>

      {/* Search Results */}
      {roomSearchResults.length > 0 && (
        <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700">
          <div className="p-4 border-b border-gray-200 dark:border-gray-700">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Résultats de recherche</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-gray-200 dark:border-gray-700">
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Salon</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Propriétaire</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Abonnement actuel</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Expire le</th>
                  <th className="text-right py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Actions</th>
                </tr>
              </thead>
              <tbody>
                {roomSearchResults.map((room) => (
                  <tr key={room.id} className="border-b border-gray-100 dark:border-gray-700/50 hover:bg-gray-50 dark:hover:bg-gray-700/30">
                    <td className="py-4 px-4">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-white">{room.name}</p>
                        <p className="text-xs text-gray-500 dark:text-gray-400">ID: {room.id}</p>
                      </div>
                    </td>
                    <td className="py-4 px-4">
                      <span className="text-gray-600 dark:text-gray-400">{room.ownerUsername || 'N/A'}</span>
                    </td>
                    <td className="py-4 px-4">
                      {room.hasActiveSubscription ? (
                        <span 
                          className="px-2 py-1 text-sm font-medium rounded-full"
                          style={{ backgroundColor: (room.currentTierColor || '#666') + '20', color: room.currentTierColor || '#666' }}
                        >
                          {room.currentTierName}
                        </span>
                      ) : (
                        <span className="px-2 py-1 text-sm text-gray-500 dark:text-gray-400 bg-gray-100 dark:bg-gray-700 rounded-full">
                          Aucun
                        </span>
                      )}
                    </td>
                    <td className="py-4 px-4">
                      <span className="text-gray-600 dark:text-gray-400">
                        {room.subscriptionExpiresAt ? formatDate(room.subscriptionExpiresAt) : '—'}
                      </span>
                    </td>
                    <td className="py-4 px-4">
                      <div className="flex justify-end gap-2">
                        <button
                          onClick={() => { setSelectedRoom(room); setShowGrantModal(true); }}
                          className="flex items-center gap-1 px-3 py-1 text-sm bg-green-600 text-white rounded-lg hover:bg-green-700"
                        >
                          <GiftIcon className="w-4 h-4" />
                          Attribuer
                        </button>
                        {room.hasActiveSubscription && (
                          <>
                            <button
                              onClick={() => setShowExtendModal(room.id)}
                              className="flex items-center gap-1 px-3 py-1 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700"
                            >
                              <ClockIcon className="w-4 h-4" />
                              Prolonger
                            </button>
                            <button
                              onClick={() => handleRevokeSubscription(room.id)}
                              className="flex items-center gap-1 px-3 py-1 text-sm bg-red-600 text-white rounded-lg hover:bg-red-700"
                            >
                              <XMarkIcon className="w-4 h-4" />
                              Révoquer
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Active Subscriptions List */}
      <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700">
        <div className="p-4 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Abonnements actifs</h3>
        </div>
        {subscriptions.length === 0 ? (
          <div className="p-8 text-center text-gray-500 dark:text-gray-400">
            Aucun abonnement actif
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-gray-200 dark:border-gray-700">
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Salon</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Tier</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Début</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Expiration</th>
                  <th className="text-left py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Méthode</th>
                  <th className="text-center py-3 px-4 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase">Statut</th>
                </tr>
              </thead>
              <tbody>
                {subscriptions.map((sub) => (
                  <tr key={sub.id} className="border-b border-gray-100 dark:border-gray-700/50 hover:bg-gray-50 dark:hover:bg-gray-700/30">
                    <td className="py-4 px-4">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-white">{sub.roomName}</p>
                        <p className="text-xs text-gray-500 dark:text-gray-400">ID: {sub.roomId}</p>
                      </div>
                    </td>
                    <td className="py-4 px-4">
                      <span 
                        className="px-2 py-1 text-sm font-medium rounded-full"
                        style={{ backgroundColor: sub.tierColor + '20', color: sub.tierColor }}
                      >
                        {sub.tierName}
                      </span>
                    </td>
                    <td className="py-4 px-4 text-gray-600 dark:text-gray-400">
                      {formatDate(sub.startedAt)}
                    </td>
                    <td className="py-4 px-4 text-gray-600 dark:text-gray-400">
                      {sub.expiresAt ? formatDate(sub.expiresAt) : 'Illimité'}
                    </td>
                    <td className="py-4 px-4 text-gray-600 dark:text-gray-400">
                      {sub.paymentMethod || 'N/A'}
                    </td>
                    <td className="py-4 px-4 text-center">
                      {sub.isActive ? (
                        <span className="px-2 py-1 text-xs font-medium bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 rounded-full">
                          Actif
                        </span>
                      ) : (
                        <span className="px-2 py-1 text-xs font-medium bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400 rounded-full">
                          Inactif
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Grant Modal */}
      {showGrantModal && selectedRoom && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-xl p-6 w-full max-w-md shadow-xl">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Attribuer un abonnement
            </h3>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
              Salon: <strong>{selectedRoom.name}</strong>
            </p>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Tier</label>
                <select
                  value={grantTierId || ''}
                  onChange={(e) => setGrantTierId(parseInt(e.target.value))}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                >
                  <option value="">Sélectionner un tier</option>
                  {tiers.map((tier) => (
                    <option key={tier.id} value={tier.id}>{tier.name} ({formatPrice(tier.monthlyPriceCents)}/mois)</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Durée (jours, 0 = illimité)</label>
                <input
                  type="number"
                  value={grantDuration}
                  onChange={(e) => setGrantDuration(parseInt(e.target.value))}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                />
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-6">
              <button
                onClick={() => { setShowGrantModal(false); setSelectedRoom(null); }}
                className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
              >
                Annuler
              </button>
              <button
                onClick={handleGrantSubscription}
                disabled={!grantTierId}
                className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50"
              >
                Attribuer
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Extend Modal */}
      {showExtendModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-xl p-6 w-full max-w-md shadow-xl">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Prolonger l'abonnement
            </h3>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Jours à ajouter</label>
              <input
                type="number"
                value={extendDays}
                onChange={(e) => setExtendDays(parseInt(e.target.value))}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              />
            </div>
            <div className="flex justify-end gap-2 mt-6">
              <button
                onClick={() => setShowExtendModal(null)}
                className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
              >
                Annuler
              </button>
              <button
                onClick={() => handleExtendSubscription(showExtendModal)}
                disabled={extendDays <= 0}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
              >
                Prolonger
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Abonnements Salons</h1>
          <p className="text-gray-500 dark:text-gray-400">Gérez les abonnements et tiers des salons</p>
        </div>
        <button
          onClick={loadData}
          className="flex items-center gap-2 px-4 py-2 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600"
        >
          <ArrowPathIcon className="w-4 h-4" />
          Actualiser
        </button>
      </div>

      {/* Error display */}
      {error && (
        <div className="mb-4 p-4 bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400 rounded-lg flex items-center gap-2">
          <ExclamationCircleIcon className="w-5 h-5" />
          {error}
          <button onClick={() => setError(null)} className="ml-auto hover:text-red-900 dark:hover:text-red-300">
            <XMarkIcon className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 mb-6 border-b border-gray-200 dark:border-gray-700">
        {[
          { id: 'overview', label: 'Vue d\'ensemble', Icon: SparklesIcon },
          { id: 'tiers', label: 'Tiers', Icon: Cog6ToothIcon },
          { id: 'rooms', label: 'Salons', Icon: BuildingOffice2Icon },
        ].map(({ id, label, Icon }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id as TabType)}
            className={`flex items-center gap-2 px-4 py-3 border-b-2 transition-colors ${
              activeTab === id
                ? 'border-blue-600 text-blue-600 dark:text-blue-400'
                : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
            }`}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
      </div>

      {/* Content */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <ArrowPathIcon className="w-8 h-8 text-blue-600 animate-spin" />
        </div>
      ) : (
        <>
          {activeTab === 'overview' && renderOverview()}
          {activeTab === 'tiers' && renderTiers()}
          {activeTab === 'rooms' && renderRooms()}
        </>
      )}
    </div>
  );
};

export default RoomSubscriptionsPage;
