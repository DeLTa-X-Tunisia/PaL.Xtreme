import { useState, useEffect } from 'react';
import {
  CreditCardIcon,
  UserGroupIcon,
  CurrencyDollarIcon,
  ClockIcon,
  SparklesIcon,
  PencilIcon,
  CheckIcon,
  XMarkIcon,
  ArrowPathIcon,
  ChartPieIcon,
  CalendarDaysIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import toast from 'react-hot-toast';

interface SubscriptionTier {
  id: number;
  tier: number;
  name: string;
  displayName: string;
  description: string | null;
  color: string;
  icon: string;
  monthlyPriceCents: number;
  yearlyPriceCents: number;
  isAvailable: boolean;
  activeUsersCount: number;
}

interface SubscriptionDuration {
  id: number;
  name: string;
  displayName: string;
  baseDays: number;
  bonusDays: number;
  totalDays: number;
  discountPercent: number;
  isAvailable: boolean;
}

interface SubscriptionStats {
  totalActiveSubscriptions: number;
  byTier: { tierId: number; tierName: string; color: string; count: number }[];
  monthlyRevenueCents: number;
  monthlyPointsSpent: number;
  expiringThisWeek: number;
  newThisWeek: number;
}

type TabType = 'overview' | 'tiers' | 'durations';

const SubscriptionsPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<TabType>('overview');
  const [loading, setLoading] = useState(true);
  
  // Data
  const [tiers, setTiers] = useState<SubscriptionTier[]>([]);
  const [durations, setDurations] = useState<SubscriptionDuration[]>([]);
  const [stats, setStats] = useState<SubscriptionStats | null>(null);
  
  // Editing states
  const [editingTier, setEditingTier] = useState<number | null>(null);
  const [editingDuration, setEditingDuration] = useState<number | null>(null);
  
  // Form states
  const [tierForm, setTierForm] = useState<Partial<SubscriptionTier>>({});
  const [durationForm, setDurationForm] = useState<Partial<SubscriptionDuration>>({});

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    setLoading(true);
    try {
      const [tiersData, durationsData, statsData] = await Promise.all([
        apiService.getSubscriptionTiers(),
        apiService.getSubscriptionDurations(),
        apiService.getSubscriptionStats(),
      ]);
      setTiers(tiersData);
      setDurations(durationsData);
      setStats(statsData);
    } catch (error) {
      console.error('Failed to fetch subscription data:', error);
      toast.error('Erreur lors du chargement des données');
    } finally {
      setLoading(false);
    }
  };

  // ============================================
  // TIER MANAGEMENT
  // ============================================
  
  const startEditTier = (tier: SubscriptionTier) => {
    setEditingTier(tier.id);
    setTierForm({
      displayName: tier.displayName,
      description: tier.description,
      color: tier.color,
      monthlyPriceCents: tier.monthlyPriceCents,
      yearlyPriceCents: tier.yearlyPriceCents,
      isAvailable: tier.isAvailable,
    });
  };

  const saveTier = async () => {
    if (!editingTier) return;
    try {
      await apiService.updateSubscriptionTier(editingTier, tierForm);
      toast.success('Tier mis à jour');
      setEditingTier(null);
      fetchData();
    } catch (error) {
      toast.error('Erreur lors de la mise à jour');
    }
  };

  // ============================================
  // DURATION MANAGEMENT
  // ============================================
  
  const startEditDuration = (duration: SubscriptionDuration) => {
    setEditingDuration(duration.id);
    setDurationForm({
      displayName: duration.displayName,
      bonusDays: duration.bonusDays,
      discountPercent: duration.discountPercent,
      isAvailable: duration.isAvailable,
    });
  };

  const saveDuration = async () => {
    if (!editingDuration) return;
    try {
      await apiService.updateSubscriptionDuration(editingDuration, durationForm);
      toast.success('Durée mise à jour');
      setEditingDuration(null);
      fetchData();
    } catch (error) {
      toast.error('Erreur lors de la mise à jour');
    }
  };

  // ============================================
  // RENDER
  // ============================================

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            <CreditCardIcon className="w-8 h-8 text-palx-400" />
            Gestion des Abonnements
          </h1>
          <p className="text-dark-400 text-sm mt-1">Gérez les tiers et durées des abonnements</p>
        </div>
        <button onClick={fetchData} className="btn-secondary flex items-center gap-2">
          <ArrowPathIcon className="w-5 h-5" />
          Actualiser
        </button>
      </div>

      {/* Tabs */}
      <div className="flex gap-2 border-b border-dark-700 pb-2">
        {[
          { id: 'overview', label: 'Vue d\'ensemble', icon: ChartPieIcon },
          { id: 'tiers', label: 'Niveaux', icon: SparklesIcon },
          { id: 'durations', label: 'Durées', icon: CalendarDaysIcon },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id as TabType)}
            className={`px-4 py-2 rounded-lg flex items-center gap-2 transition-colors ${
              activeTab === tab.id
                ? 'bg-palx-500/20 text-palx-400'
                : 'text-dark-400 hover:text-white hover:bg-dark-700/50'
            }`}
          >
            <tab.icon className="w-5 h-5" />
            {tab.label}
          </button>
        ))}
      </div>

      {/* TAB: Overview */}
      {activeTab === 'overview' && stats && (
        <div className="space-y-6">
          {/* Stats Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="card">
              <div className="flex items-center gap-3">
                <div className="p-3 rounded-xl bg-palx-500/20">
                  <UserGroupIcon className="w-6 h-6 text-palx-400" />
                </div>
                <div>
                  <p className="text-dark-400 text-sm">Abonnés actifs</p>
                  <p className="text-2xl font-bold text-white">{stats.totalActiveSubscriptions}</p>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="flex items-center gap-3">
                <div className="p-3 rounded-xl bg-green-500/20">
                  <CurrencyDollarIcon className="w-6 h-6 text-green-400" />
                </div>
                <div>
                  <p className="text-dark-400 text-sm">Revenus du mois</p>
                  <p className="text-2xl font-bold text-white">{(stats.monthlyRevenueCents / 100).toFixed(2)}€</p>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="flex items-center gap-3">
                <div className="p-3 rounded-xl bg-yellow-500/20">
                  <SparklesIcon className="w-6 h-6 text-yellow-400" />
                </div>
                <div>
                  <p className="text-dark-400 text-sm">Nouveaux ce mois</p>
                  <p className="text-2xl font-bold text-white">{stats.newThisWeek}</p>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="flex items-center gap-3">
                <div className="p-3 rounded-xl bg-red-500/20">
                  <ClockIcon className="w-6 h-6 text-red-400" />
                </div>
                <div>
                  <p className="text-dark-400 text-sm">Expirent cette semaine</p>
                  <p className="text-2xl font-bold text-white">{stats.expiringThisWeek}</p>
                </div>
              </div>
            </div>
          </div>

          {/* Distribution by Tier */}
          <div className="card">
            <h3 className="text-lg font-semibold text-white mb-4">Répartition par niveau</h3>
            <div className="space-y-3">
              {stats.byTier.map((tier) => {
                const percentage = stats.totalActiveSubscriptions > 0 
                  ? (tier.count / stats.totalActiveSubscriptions * 100).toFixed(1)
                  : 0;
                return (
                  <div key={tier.tierId} className="flex items-center gap-4">
                    <div 
                      className="w-4 h-4 rounded-full" 
                      style={{ backgroundColor: tier.color }}
                    />
                    <span className="text-white w-24">{tier.tierName}</span>
                    <div className="flex-1 h-3 bg-dark-700 rounded-full overflow-hidden">
                      <div 
                        className="h-full rounded-full transition-all duration-500"
                        style={{ 
                          width: `${percentage}%`,
                          backgroundColor: tier.color
                        }}
                      />
                    </div>
                    <span className="text-dark-400 w-20 text-right">{tier.count} ({percentage}%)</span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* TAB: Tiers */}
      {activeTab === 'tiers' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {tiers.map((tier) => (
            <div 
              key={tier.id} 
              className="card"
              style={{ borderLeftColor: tier.color, borderLeftWidth: '4px' }}
            >
              {editingTier === tier.id ? (
                // Edit Mode
                <div className="space-y-4">
                  <div className="flex items-center justify-between">
                    <h3 className="text-lg font-semibold text-white">{tier.name}</h3>
                    <div className="flex gap-2">
                      <button onClick={() => setEditingTier(null)} className="p-2 rounded-lg bg-dark-700 text-dark-400 hover:text-white">
                        <XMarkIcon className="w-5 h-5" />
                      </button>
                      <button onClick={saveTier} className="p-2 rounded-lg bg-palx-500 text-white">
                        <CheckIcon className="w-5 h-5" />
                      </button>
                    </div>
                  </div>
                  
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="label">Nom affiché</label>
                      <input
                        type="text"
                        value={tierForm.displayName || ''}
                        onChange={(e) => setTierForm({ ...tierForm, displayName: e.target.value })}
                        className="input"
                      />
                    </div>
                    <div>
                      <label className="label">Couleur</label>
                      <input
                        type="color"
                        value={tierForm.color || '#808080'}
                        onChange={(e) => setTierForm({ ...tierForm, color: e.target.value })}
                        className="input h-10"
                      />
                    </div>
                    <div>
                      <label className="label">Prix mensuel (centimes)</label>
                      <input
                        type="number"
                        value={tierForm.monthlyPriceCents || 0}
                        onChange={(e) => setTierForm({ ...tierForm, monthlyPriceCents: parseInt(e.target.value) })}
                        className="input"
                      />
                    </div>
                    <div>
                      <label className="label">Prix annuel (centimes)</label>
                      <input
                        type="number"
                        value={tierForm.yearlyPriceCents || 0}
                        onChange={(e) => setTierForm({ ...tierForm, yearlyPriceCents: parseInt(e.target.value) })}
                        className="input"
                      />
                    </div>
                  </div>
                  
                  <div>
                    <label className="label">Description</label>
                    <textarea
                      value={tierForm.description || ''}
                      onChange={(e) => setTierForm({ ...tierForm, description: e.target.value })}
                      className="input"
                      rows={2}
                    />
                  </div>

                  <div className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      id={`tier-available-${tier.id}`}
                      checked={tierForm.isAvailable || false}
                      onChange={(e) => setTierForm({ ...tierForm, isAvailable: e.target.checked })}
                      className="rounded border-dark-600"
                    />
                    <label htmlFor={`tier-available-${tier.id}`} className="text-dark-300 text-sm">Disponible à l'achat</label>
                  </div>
                </div>
              ) : (
                // View Mode
                <>
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div 
                        className="w-10 h-10 rounded-xl flex items-center justify-center"
                        style={{ backgroundColor: `${tier.color}20` }}
                      >
                        <SparklesIcon className="w-6 h-6" style={{ color: tier.color }} />
                      </div>
                      <div>
                        <h3 className="text-lg font-semibold text-white">{tier.displayName}</h3>
                        <p className="text-dark-400 text-sm">{tier.name}</p>
                      </div>
                    </div>
                    <button 
                      onClick={() => startEditTier(tier)}
                      className="p-2 rounded-lg bg-dark-700/50 text-dark-400 hover:text-white hover:bg-dark-700"
                    >
                      <PencilIcon className="w-5 h-5" />
                    </button>
                  </div>

                  <p className="text-dark-300 text-sm mb-4">{tier.description}</p>

                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div className="flex items-center justify-between p-2 bg-dark-700/30 rounded-lg">
                      <span className="text-dark-400">Prix/mois</span>
                      <span className="text-white font-medium">{(tier.monthlyPriceCents / 100).toFixed(2)}€</span>
                    </div>
                    <div className="flex items-center justify-between p-2 bg-dark-700/30 rounded-lg">
                      <span className="text-dark-400">Prix/an</span>
                      <span className="text-white font-medium">{(tier.yearlyPriceCents / 100).toFixed(2)}€</span>
                    </div>
                  </div>

                  <div className="mt-4 pt-4 border-t border-dark-700/50 flex items-center justify-between">
                    <span className="text-dark-400 text-sm">{tier.activeUsersCount} abonnés actifs</span>
                    <span className={`px-2 py-1 rounded-full text-xs ${tier.isAvailable ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'}`}>
                      {tier.isAvailable ? 'Disponible' : 'Indisponible'}
                    </span>
                  </div>
                </>
              )}
            </div>
          ))}
        </div>
      )}

      {/* TAB: Durations */}
      {activeTab === 'durations' && (
        <div className="card overflow-hidden">
          <table className="w-full">
            <thead className="bg-dark-700/50">
              <tr>
                <th className="text-left p-4 text-dark-400 font-medium">Durée</th>
                <th className="text-center p-4 text-dark-400 font-medium">Jours de base</th>
                <th className="text-center p-4 text-dark-400 font-medium">Bonus</th>
                <th className="text-center p-4 text-dark-400 font-medium">Total</th>
                <th className="text-center p-4 text-dark-400 font-medium">Remise</th>
                <th className="text-center p-4 text-dark-400 font-medium">Statut</th>
                <th className="text-right p-4 text-dark-400 font-medium">Actions</th>
              </tr>
            </thead>
            <tbody>
              {durations.map((duration) => (
                <tr key={duration.id} className="border-t border-dark-700/50 hover:bg-dark-700/30">
                  {editingDuration === duration.id ? (
                    <>
                      <td className="p-4">
                        <input
                          type="text"
                          value={durationForm.displayName || ''}
                          onChange={(e) => setDurationForm({ ...durationForm, displayName: e.target.value })}
                          className="input w-full"
                        />
                      </td>
                      <td className="p-4 text-center text-white">{duration.baseDays}</td>
                      <td className="p-4 text-center">
                        <input
                          type="number"
                          value={durationForm.bonusDays || 0}
                          onChange={(e) => setDurationForm({ ...durationForm, bonusDays: parseInt(e.target.value) })}
                          className="input w-20 text-center"
                        />
                      </td>
                      <td className="p-4 text-center text-white">{duration.baseDays + (durationForm.bonusDays || 0)}</td>
                      <td className="p-4 text-center">
                        <input
                          type="number"
                          value={durationForm.discountPercent || 0}
                          onChange={(e) => setDurationForm({ ...durationForm, discountPercent: parseInt(e.target.value) })}
                          className="input w-20 text-center"
                        />
                      </td>
                      <td className="p-4 text-center">
                        <label className="flex items-center justify-center gap-2">
                          <input
                            type="checkbox"
                            checked={durationForm.isAvailable}
                            onChange={(e) => setDurationForm({ ...durationForm, isAvailable: e.target.checked })}
                            className="rounded border-dark-600"
                          />
                        </label>
                      </td>
                      <td className="p-4 text-right">
                        <div className="flex justify-end gap-2">
                          <button onClick={() => setEditingDuration(null)} className="p-2 rounded-lg bg-dark-700 text-dark-400 hover:text-white">
                            <XMarkIcon className="w-4 h-4" />
                          </button>
                          <button onClick={saveDuration} className="p-2 rounded-lg bg-palx-500 text-white">
                            <CheckIcon className="w-4 h-4" />
                          </button>
                        </div>
                      </td>
                    </>
                  ) : (
                    <>
                      <td className="p-4 text-white font-medium">{duration.displayName}</td>
                      <td className="p-4 text-center text-dark-300">{duration.baseDays}j</td>
                      <td className="p-4 text-center">
                        {duration.bonusDays > 0 ? (
                          <span className="text-green-400">+{duration.bonusDays}j</span>
                        ) : (
                          <span className="text-dark-500">-</span>
                        )}
                      </td>
                      <td className="p-4 text-center text-white font-medium">{duration.totalDays}j</td>
                      <td className="p-4 text-center">
                        {duration.discountPercent > 0 ? (
                          <span className="text-yellow-400">-{duration.discountPercent}%</span>
                        ) : (
                          <span className="text-dark-500">-</span>
                        )}
                      </td>
                      <td className="p-4 text-center">
                        <span className={`px-2 py-1 rounded-full text-xs ${duration.isAvailable ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'}`}>
                          {duration.isAvailable ? 'Actif' : 'Inactif'}
                        </span>
                      </td>
                      <td className="p-4 text-right">
                        <button 
                          onClick={() => startEditDuration(duration)}
                          className="p-2 rounded-lg bg-dark-700/50 text-dark-400 hover:text-white hover:bg-dark-700"
                        >
                          <PencilIcon className="w-4 h-4" />
                        </button>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default SubscriptionsPage;
