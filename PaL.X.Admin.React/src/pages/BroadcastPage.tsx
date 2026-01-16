import { useState, useEffect } from 'react';
import { 
  MegaphoneIcon,
  PaperAirplaneIcon,
  ClockIcon,
  UserCircleIcon,
  InformationCircleIcon,
  ExclamationTriangleIcon,
  ExclamationCircleIcon,
  CheckCircleIcon,
  PencilIcon,
  TrashIcon,
  ArrowPathIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { BroadcastHistory } from '../types';

type BroadcastType = 'info' | 'warning' | 'alert' | 'success';

interface BroadcastTypeOption {
  value: BroadcastType;
  label: string;
  icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
  color: string;
  bgColor: string;
  description: string;
}

const broadcastTypes: BroadcastTypeOption[] = [
  {
    value: 'info',
    label: 'Information',
    icon: InformationCircleIcon,
    color: 'text-blue-400',
    bgColor: 'bg-blue-500/20',
    description: 'Annonce générale ou mise à jour'
  },
  {
    value: 'success',
    label: 'Succès',
    icon: CheckCircleIcon,
    color: 'text-green-400',
    bgColor: 'bg-green-500/20',
    description: 'Bonne nouvelle ou confirmation'
  },
  {
    value: 'warning',
    label: 'Avertissement',
    icon: ExclamationTriangleIcon,
    color: 'text-yellow-400',
    bgColor: 'bg-yellow-500/20',
    description: 'Information importante à considérer'
  },
  {
    value: 'alert',
    label: 'Alerte',
    icon: ExclamationCircleIcon,
    color: 'text-red-400',
    bgColor: 'bg-red-500/20',
    description: 'Urgence ou problème critique'
  },
];

const BroadcastPage: React.FC = () => {
  const [type, setType] = useState<BroadcastType>('info');
  const [title, setTitle] = useState('');
  const [message, setMessage] = useState('');
  const [sending, setSending] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [history, setHistory] = useState<BroadcastHistory[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(true);
  
  // États pour l'édition
  const [editingBroadcast, setEditingBroadcast] = useState<BroadcastHistory | null>(null);
  const [editType, setEditType] = useState<BroadcastType>('info');
  const [editTitle, setEditTitle] = useState('');
  const [editMessage, setEditMessage] = useState('');
  const [editSaving, setEditSaving] = useState(false);
  
  // État pour la suppression
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState<number | null>(null);

  useEffect(() => {
    fetchHistory();
  }, []);

  const fetchHistory = async () => {
    try {
      const data = await apiService.getBroadcastHistory(1, 10);
      setHistory(data.items);
    } catch (err) {
      console.error('Failed to fetch broadcast history:', err);
    } finally {
      setLoadingHistory(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!title.trim() || !message.trim()) {
      setError('Veuillez remplir tous les champs');
      return;
    }

    setSending(true);
    setError(null);
    setSuccess(null);

    try {
      const result = await apiService.sendBroadcast({ type, title, message });
      setSuccess(result.message || 'Annonce envoyée avec succès !');
      setTitle('');
      setMessage('');
      fetchHistory(); // Refresh history
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de l\'envoi de l\'annonce');
    } finally {
      setSending(false);
    }
  };

  // Ouvrir le modal d'édition
  const handleEditClick = (item: BroadcastHistory) => {
    setEditingBroadcast(item);
    setEditType(item.type);
    setEditTitle(item.title);
    setEditMessage(item.message);
  };

  // Sauvegarder les modifications
  const handleEditSave = async () => {
    if (!editingBroadcast) return;
    
    if (!editTitle.trim() || !editMessage.trim()) {
      setError('Veuillez remplir tous les champs');
      return;
    }

    setEditSaving(true);
    setError(null);

    try {
      await apiService.updateBroadcast(editingBroadcast.id, {
        type: editType,
        title: editTitle,
        message: editMessage
      });
      setSuccess('Annonce modifiée avec succès !');
      setEditingBroadcast(null);
      fetchHistory();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de la modification');
    } finally {
      setEditSaving(false);
    }
  };

  // Supprimer une annonce
  const handleDelete = async (id: number) => {
    setDeletingId(id);
    setError(null);

    try {
      await apiService.deleteBroadcast(id);
      setSuccess('Annonce supprimée avec succès !');
      setShowDeleteConfirm(null);
      fetchHistory();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de la suppression');
    } finally {
      setDeletingId(null);
    }
  };

  // Renvoyer une annonce (pré-remplir le formulaire)
  const handleResend = (item: BroadcastHistory) => {
    setType(item.type);
    setTitle(item.title);
    setMessage(item.message);
    // Scroll vers le haut pour voir le formulaire
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const selectedType = broadcastTypes.find(t => t.value === type)!;

  const getTypeIcon = (typeValue: string) => {
    const typeOption = broadcastTypes.find(t => t.value === typeValue);
    if (!typeOption) return InformationCircleIcon;
    return typeOption.icon;
  };

  const getTypeColor = (typeValue: string) => {
    const typeOption = broadcastTypes.find(t => t.value === typeValue);
    return typeOption?.color || 'text-blue-400';
  };

  const getTypeBgColor = (typeValue: string) => {
    const typeOption = broadcastTypes.find(t => t.value === typeValue);
    return typeOption?.bgColor || 'bg-blue-500/20';
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            <MegaphoneIcon className="w-8 h-8 text-palx-400" />
            Diffusion Globale
          </h1>
          <p className="text-dark-400 mt-1">
            Envoyez une annonce à tous les utilisateurs connectés
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Formulaire d'envoi */}
        <div className="card">
          <h2 className="text-lg font-semibold text-white mb-6 flex items-center gap-2">
            <PaperAirplaneIcon className="w-5 h-5 text-palx-400" />
            Nouvelle Annonce
          </h2>

          <form onSubmit={handleSubmit} className="space-y-6">
            {/* Type de message */}
            <div>
              <label className="block text-sm font-medium text-dark-300 mb-3">
                Type d'annonce
              </label>
              <div className="grid grid-cols-2 gap-3">
                {broadcastTypes.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => setType(option.value)}
                    className={`p-4 rounded-xl border-2 transition-all text-left ${
                      type === option.value
                        ? `border-current ${option.color} ${option.bgColor}`
                        : 'border-dark-700 hover:border-dark-600 bg-dark-800/50'
                    }`}
                  >
                    <div className="flex items-center gap-3 mb-2">
                      <option.icon className={`w-5 h-5 ${type === option.value ? option.color : 'text-dark-400'}`} />
                      <span className={`font-medium ${type === option.value ? 'text-white' : 'text-dark-300'}`}>
                        {option.label}
                      </span>
                    </div>
                    <p className="text-xs text-dark-400">{option.description}</p>
                  </button>
                ))}
              </div>
            </div>

            {/* Titre */}
            <div>
              <label htmlFor="title" className="block text-sm font-medium text-dark-300 mb-2">
                Titre de l'annonce
              </label>
              <input
                type="text"
                id="title"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Ex: Maintenance prévue ce soir"
                className="input"
                maxLength={100}
              />
              <p className="text-xs text-dark-500 mt-1">{title.length}/100 caractères</p>
            </div>

            {/* Message */}
            <div>
              <label htmlFor="message" className="block text-sm font-medium text-dark-300 mb-2">
                Message
              </label>
              <textarea
                id="message"
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                placeholder="Rédigez votre message ici..."
                rows={5}
                className="input resize-none"
                maxLength={500}
              />
              <p className="text-xs text-dark-500 mt-1">{message.length}/500 caractères</p>
            </div>

            {/* Prévisualisation */}
            {(title || message) && (
              <div className={`p-4 rounded-xl ${selectedType.bgColor} border border-current/20`}>
                <p className="text-xs text-dark-400 mb-2">Prévisualisation :</p>
                <div className="flex items-start gap-3">
                  <selectedType.icon className={`w-6 h-6 ${selectedType.color} shrink-0 mt-0.5`} />
                  <div>
                    <h4 className={`font-semibold ${selectedType.color}`}>{title || 'Titre'}</h4>
                    <p className="text-dark-300 text-sm mt-1">{message || 'Message...'}</p>
                  </div>
                </div>
              </div>
            )}

            {/* Messages d'erreur/succès */}
            {error && (
              <div className="p-3 rounded-lg bg-red-500/20 border border-red-500/30 text-red-400 text-sm">
                {error}
              </div>
            )}
            {success && (
              <div className="p-3 rounded-lg bg-green-500/20 border border-green-500/30 text-green-400 text-sm flex items-center gap-2">
                <CheckCircleIcon className="w-5 h-5" />
                {success}
              </div>
            )}

            {/* Bouton d'envoi */}
            <button
              type="submit"
              disabled={sending || !title.trim() || !message.trim()}
              className="btn-primary w-full flex items-center justify-center gap-2"
            >
              {sending ? (
                <>
                  <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Envoi en cours...
                </>
              ) : (
                <>
                  <MegaphoneIcon className="w-5 h-5" />
                  Envoyer l'annonce
                </>
              )}
            </button>
          </form>
        </div>

        {/* Historique */}
        <div className="card">
          <h2 className="text-lg font-semibold text-white mb-6 flex items-center gap-2">
            <ClockIcon className="w-5 h-5 text-palx-400" />
            Historique des annonces
          </h2>

          {loadingHistory ? (
            <div className="flex items-center justify-center py-12">
              <div className="w-8 h-8 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
            </div>
          ) : history.length === 0 ? (
            <div className="text-center py-12">
              <MegaphoneIcon className="w-12 h-12 text-dark-600 mx-auto mb-3" />
              <p className="text-dark-400">Aucune annonce envoyée</p>
            </div>
          ) : (
            <div className="space-y-4 max-h-[600px] overflow-y-auto pr-2">
              {history.map((item) => {
                const TypeIcon = getTypeIcon(item.type);
                return (
                  <div 
                    key={item.id} 
                    className={`p-4 rounded-xl ${getTypeBgColor(item.type)} border border-current/10`}
                  >
                    <div className="flex items-start gap-3">
                      <TypeIcon className={`w-5 h-5 ${getTypeColor(item.type)} shrink-0 mt-0.5`} />
                      <div className="flex-1 min-w-0">
                        <h4 className={`font-semibold ${getTypeColor(item.type)}`}>{item.title}</h4>
                        <p className="text-dark-300 text-sm mt-1 line-clamp-2">{item.message}</p>
                        <div className="flex items-center justify-between mt-3">
                          <div className="flex items-center gap-4 text-xs text-dark-500">
                            <span className="flex items-center gap-1">
                              <UserCircleIcon className="w-4 h-4" />
                              {item.sentByDisplayName}
                            </span>
                            <span className="flex items-center gap-1">
                              <ClockIcon className="w-4 h-4" />
                              {new Date(item.sentAt).toLocaleString('fr-FR', {
                                day: '2-digit',
                                month: '2-digit',
                                year: 'numeric',
                                hour: '2-digit',
                                minute: '2-digit'
                              })}
                            </span>
                          </div>
                          
                          {/* Boutons d'action */}
                          <div className="flex items-center gap-2">
                            <button
                              onClick={() => handleResend(item)}
                              className="p-1.5 rounded-lg bg-dark-700/50 hover:bg-palx-500/20 text-dark-400 hover:text-palx-400 transition-colors"
                              title="Renvoyer"
                            >
                              <ArrowPathIcon className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleEditClick(item)}
                              className="p-1.5 rounded-lg bg-dark-700/50 hover:bg-blue-500/20 text-dark-400 hover:text-blue-400 transition-colors"
                              title="Modifier"
                            >
                              <PencilIcon className="w-4 h-4" />
                            </button>
                            {showDeleteConfirm === item.id ? (
                              <div className="flex items-center gap-1">
                                <button
                                  onClick={() => handleDelete(item.id)}
                                  disabled={deletingId === item.id}
                                  className="px-2 py-1 rounded text-xs bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors"
                                >
                                  {deletingId === item.id ? '...' : 'Confirmer'}
                                </button>
                                <button
                                  onClick={() => setShowDeleteConfirm(null)}
                                  className="px-2 py-1 rounded text-xs bg-dark-600 text-dark-300 hover:bg-dark-500 transition-colors"
                                >
                                  Annuler
                                </button>
                              </div>
                            ) : (
                              <button
                                onClick={() => setShowDeleteConfirm(item.id)}
                                className="p-1.5 rounded-lg bg-dark-700/50 hover:bg-red-500/20 text-dark-400 hover:text-red-400 transition-colors"
                                title="Supprimer"
                              >
                                <TrashIcon className="w-4 h-4" />
                              </button>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* Modal d'édition */}
      {editingBroadcast && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="card max-w-lg w-full max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-lg font-semibold text-white flex items-center gap-2">
                <PencilIcon className="w-5 h-5 text-palx-400" />
                Modifier l'annonce
              </h3>
              <button
                onClick={() => setEditingBroadcast(null)}
                className="p-2 rounded-lg hover:bg-dark-700 text-dark-400 hover:text-white transition-colors"
              >
                <XMarkIcon className="w-5 h-5" />
              </button>
            </div>

            {/* Type de l'annonce */}
            <div className="mb-4">
              <label className="block text-sm font-medium text-dark-300 mb-2">Type</label>
              <div className="grid grid-cols-2 gap-2">
                {broadcastTypes.map((typeOption) => (
                  <button
                    key={typeOption.value}
                    type="button"
                    onClick={() => setEditType(typeOption.value)}
                    className={`p-3 rounded-xl border transition-all ${
                      editType === typeOption.value
                        ? `${typeOption.bgColor} border-current/30`
                        : 'border-dark-700 hover:border-dark-600'
                    }`}
                  >
                    <typeOption.icon className={`w-5 h-5 mx-auto mb-1 ${typeOption.color}`} />
                    <span className={`text-xs ${editType === typeOption.value ? typeOption.color : 'text-dark-400'}`}>
                      {typeOption.label}
                    </span>
                  </button>
                ))}
              </div>
            </div>

            {/* Titre */}
            <div className="mb-4">
              <label className="block text-sm font-medium text-dark-300 mb-2">Titre</label>
              <input
                type="text"
                value={editTitle}
                onChange={(e) => setEditTitle(e.target.value)}
                className="input w-full"
                maxLength={100}
              />
            </div>

            {/* Message */}
            <div className="mb-6">
              <label className="block text-sm font-medium text-dark-300 mb-2">Message</label>
              <textarea
                value={editMessage}
                onChange={(e) => setEditMessage(e.target.value)}
                rows={4}
                className="input w-full resize-none"
                maxLength={500}
              />
              <p className="text-xs text-dark-500 mt-1">{editMessage.length}/500 caractères</p>
            </div>

            {/* Boutons */}
            <div className="flex gap-3">
              <button
                onClick={() => setEditingBroadcast(null)}
                className="btn-secondary flex-1"
              >
                Annuler
              </button>
              <button
                onClick={handleEditSave}
                disabled={editSaving || !editTitle.trim() || !editMessage.trim()}
                className="btn-primary flex-1 flex items-center justify-center gap-2"
              >
                {editSaving ? (
                  <>
                    <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                    Enregistrement...
                  </>
                ) : (
                  <>
                    <CheckCircleIcon className="w-5 h-5" />
                    Enregistrer
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

export default BroadcastPage;
