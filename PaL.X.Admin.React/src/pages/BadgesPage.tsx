import { useState, useEffect } from 'react';
import { 
  SparklesIcon, 
  PlusIcon, 
  PencilIcon, 
  TrashIcon,
  XMarkIcon,
  StarIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { Badge, BadgeRarity } from '../types';
import toast from 'react-hot-toast';

const BadgesPage: React.FC = () => {
  const [badges, setBadges] = useState<Badge[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingBadge, setEditingBadge] = useState<Badge | null>(null);
  const [form, setForm] = useState({
    name: '',
    description: '',
    iconUrl: '',
    rarity: 'Common' as BadgeRarity,
  });

  useEffect(() => {
    fetchBadges();
  }, []);

  const fetchBadges = async () => {
    setLoading(true);
    try {
      const data = await apiService.getBadges();
      setBadges(data);
    } catch (error) {
      console.error('Failed to fetch badges:', error);
      // Mock data
      setBadges([
        { id: 1, name: 'Pionnier', description: 'Parmi les 100 premiers utilisateurs', iconUrl: '🏆', rarity: 'Legendary', createdAt: '2024-01-01', usersCount: 85 },
        { id: 2, name: 'VIP', description: 'Membre VIP actif', iconUrl: '⭐', rarity: 'Epic', createdAt: '2024-01-15', usersCount: 42 },
        { id: 3, name: 'Bavard', description: 'Plus de 1000 messages envoyés', iconUrl: '💬', rarity: 'Rare', createdAt: '2024-02-01', usersCount: 256 },
        { id: 4, name: 'Créateur', description: 'A créé plus de 5 salons', iconUrl: '🏠', rarity: 'Uncommon', createdAt: '2024-02-15', usersCount: 180 },
        { id: 5, name: 'Nouveau', description: 'Bienvenue sur PaL.Xtreme!', iconUrl: '🌟', rarity: 'Common', createdAt: '2024-01-01', usersCount: 1247 },
        { id: 6, name: 'Modérateur', description: 'Membre de l\'équipe de modération', iconUrl: '🛡️', rarity: 'Epic', createdAt: '2024-01-10', usersCount: 8 },
        { id: 7, name: 'Support', description: 'A aidé la communauté', iconUrl: '❤️', rarity: 'Rare', createdAt: '2024-03-01', usersCount: 45 },
        { id: 8, name: 'Nuit blanche', description: 'Connecté après minuit', iconUrl: '🌙', rarity: 'Common', createdAt: '2024-04-01', usersCount: 890 },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleOpenModal = (badge?: Badge) => {
    if (badge) {
      setEditingBadge(badge);
      setForm({
        name: badge.name,
        description: badge.description,
        iconUrl: badge.iconUrl,
        rarity: badge.rarity,
      });
    } else {
      setEditingBadge(null);
      setForm({ name: '', description: '', iconUrl: '', rarity: 'Common' });
    }
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!form.name.trim() || !form.description.trim()) {
      toast.error('Veuillez remplir tous les champs');
      return;
    }

    try {
      if (editingBadge) {
        await apiService.updateBadge(editingBadge.id, form);
        toast.success('Badge modifié');
      } else {
        await apiService.createBadge(form);
        toast.success('Badge créé');
      }
      setShowModal(false);
      fetchBadges();
    } catch (error) {
      toast.error('Échec de l\'opération');
    }
  };

  const handleDelete = async (badge: Badge) => {
    if (!confirm(`Supprimer le badge "${badge.name}" ?`)) return;

    try {
      await apiService.deleteBadge(badge.id);
      toast.success('Badge supprimé');
      fetchBadges();
    } catch (error) {
      toast.error('Échec de la suppression');
    }
  };

  const getRarityConfig = (rarity: BadgeRarity) => {
    switch (rarity) {
      case 'Legendary':
        return { color: 'text-yellow-400', bg: 'bg-yellow-400/10', border: 'border-yellow-400/30', glow: 'shadow-yellow-400/20' };
      case 'Epic':
        return { color: 'text-purple-400', bg: 'bg-purple-400/10', border: 'border-purple-400/30', glow: 'shadow-purple-400/20' };
      case 'Rare':
        return { color: 'text-blue-400', bg: 'bg-blue-400/10', border: 'border-blue-400/30', glow: 'shadow-blue-400/20' };
      case 'Uncommon':
        return { color: 'text-green-400', bg: 'bg-green-400/10', border: 'border-green-400/30', glow: 'shadow-green-400/20' };
      default:
        return { color: 'text-gray-400', bg: 'bg-gray-400/10', border: 'border-gray-400/30', glow: '' };
    }
  };

  const rarityOrder: BadgeRarity[] = ['Legendary', 'Epic', 'Rare', 'Uncommon', 'Common'];
  const sortedBadges = [...badges].sort((a, b) => 
    rarityOrder.indexOf(a.rarity) - rarityOrder.indexOf(b.rarity)
  );

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white">Gestion des badges</h1>
          <p className="text-dark-400 text-sm mt-1">{badges.length} badges disponibles</p>
        </div>
        <button onClick={() => handleOpenModal()} className="btn-primary">
          <PlusIcon className="w-5 h-5" />
          Nouveau badge
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
        {rarityOrder.map((rarity) => {
          const config = getRarityConfig(rarity);
          const count = badges.filter(b => b.rarity === rarity).length;
          return (
            <div key={rarity} className={`card ${config.bg} border ${config.border}`}>
              <p className={`text-2xl font-bold ${config.color}`}>{count}</p>
              <p className="text-dark-400 text-sm">{rarity}</p>
            </div>
          );
        })}
      </div>

      {/* Badges Grid */}
      {loading ? (
        <div className="flex justify-center py-12">
          <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {sortedBadges.map((badge) => {
            const config = getRarityConfig(badge.rarity);
            return (
              <div 
                key={badge.id} 
                className={`card border ${config.border} hover:shadow-lg ${config.glow} transition-all`}
              >
                {/* Icon */}
                <div className="text-center mb-4">
                  <div className={`w-16 h-16 mx-auto rounded-2xl ${config.bg} flex items-center justify-center text-4xl`}>
                    {badge.iconUrl.startsWith('http') ? (
                      <img src={badge.iconUrl} alt={badge.name} className="w-10 h-10 object-contain" />
                    ) : (
                      badge.iconUrl
                    )}
                  </div>
                </div>

                {/* Info */}
                <div className="text-center mb-4">
                  <h3 className="text-lg font-semibold text-white mb-1">{badge.name}</h3>
                  <span className={`badge ${config.bg} ${config.color}`}>
                    {badge.rarity}
                  </span>
                  <p className="text-dark-400 text-sm mt-2">{badge.description}</p>
                </div>

                {/* Stats */}
                <div className="flex items-center justify-center gap-2 text-dark-400 text-sm mb-4">
                  <SparklesIcon className="w-4 h-4" />
                  <span>{badge.usersCount} utilisateurs</span>
                </div>

                {/* Actions */}
                <div className="flex items-center justify-center gap-2 pt-4 border-t border-dark-700/50">
                  <button
                    onClick={() => handleOpenModal(badge)}
                    className="btn-ghost py-2 text-sm"
                  >
                    <PencilIcon className="w-4 h-4" />
                    Modifier
                  </button>
                  <button
                    onClick={() => handleDelete(badge)}
                    className="btn-ghost py-2 text-sm text-danger hover:text-danger"
                  >
                    <TrashIcon className="w-4 h-4" />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create/Edit Modal */}
      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="text-lg font-semibold text-white">
                {editingBadge ? 'Modifier le badge' : 'Nouveau badge'}
              </h3>
              <button
                onClick={() => setShowModal(false)}
                className="p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
              >
                <XMarkIcon className="w-5 h-5" />
              </button>
            </div>
            <div className="modal-body space-y-4">
              <div>
                <label className="label">Nom du badge *</label>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => setForm(prev => ({ ...prev, name: e.target.value }))}
                  className="input"
                  placeholder="Ex: Champion"
                />
              </div>
              <div>
                <label className="label">Description *</label>
                <textarea
                  value={form.description}
                  onChange={(e) => setForm(prev => ({ ...prev, description: e.target.value }))}
                  className="input min-h-20"
                  placeholder="Décrivez comment obtenir ce badge..."
                />
              </div>
              <div>
                <label className="label">Icône (emoji ou URL)</label>
                <input
                  type="text"
                  value={form.iconUrl}
                  onChange={(e) => setForm(prev => ({ ...prev, iconUrl: e.target.value }))}
                  className="input"
                  placeholder="🏆 ou https://..."
                />
              </div>
              <div>
                <label className="label">Rareté</label>
                <select
                  value={form.rarity}
                  onChange={(e) => setForm(prev => ({ ...prev, rarity: e.target.value as BadgeRarity }))}
                  className="input"
                >
                  <option value="Common">Common (Commun)</option>
                  <option value="Uncommon">Uncommon (Peu commun)</option>
                  <option value="Rare">Rare</option>
                  <option value="Epic">Epic (Épique)</option>
                  <option value="Legendary">Legendary (Légendaire)</option>
                </select>
              </div>

              {/* Preview */}
              {form.name && (
                <div className="p-4 bg-dark-700/30 rounded-lg">
                  <p className="text-dark-400 text-xs mb-2">Aperçu</p>
                  <div className="flex items-center gap-3">
                    <div className={`w-12 h-12 rounded-xl ${getRarityConfig(form.rarity).bg} flex items-center justify-center text-2xl`}>
                      {form.iconUrl || '❓'}
                    </div>
                    <div>
                      <p className="text-white font-medium">{form.name}</p>
                      <span className={`badge ${getRarityConfig(form.rarity).bg} ${getRarityConfig(form.rarity).color}`}>
                        {form.rarity}
                      </span>
                    </div>
                  </div>
                </div>
              )}
            </div>
            <div className="modal-footer">
              <button onClick={() => setShowModal(false)} className="btn-secondary">
                Annuler
              </button>
              <button onClick={handleSave} className="btn-primary">
                <SparklesIcon className="w-5 h-5" />
                {editingBadge ? 'Enregistrer' : 'Créer'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default BadgesPage;
