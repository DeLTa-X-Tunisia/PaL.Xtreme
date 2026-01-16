import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useSignalR } from '../contexts/SignalRContext';
import { 
  Cog6ToothIcon, 
  ShieldCheckIcon,
  MegaphoneIcon,
  WrenchScrewdriverIcon,
  ServerIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import toast from 'react-hot-toast';

const SettingsPage: React.FC = () => {
  const { user } = useAuth();
  const { isConnected } = useSignalR();
  const [broadcastMessage, setBroadcastMessage] = useState('');
  const [maintenanceMode, setMaintenanceMode] = useState(false);
  const [maintenanceMessage, setMaintenanceMessage] = useState('');

  const handleSendBroadcast = async () => {
    if (!broadcastMessage.trim()) {
      toast.error('Veuillez entrer un message');
      return;
    }

    try {
      await apiService.sendBroadcast({
        type: 'info',
        title: 'Annonce système',
        message: broadcastMessage
      });
      toast.success('Message broadcast envoyé');
      setBroadcastMessage('');
    } catch (error) {
      toast.error('Échec de l\'envoi');
    }
  };

  const handleToggleMaintenance = async () => {
    try {
      await apiService.enableMaintenanceMode(!maintenanceMode, maintenanceMessage || undefined);
      setMaintenanceMode(!maintenanceMode);
      toast.success(`Mode maintenance ${!maintenanceMode ? 'activé' : 'désactivé'}`);
    } catch (error) {
      toast.error('Échec de l\'opération');
    }
  };

  return (
    <div className="space-y-6 animate-fade-in max-w-4xl">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white">Paramètres</h1>
        <p className="text-dark-400 text-sm mt-1">Configuration du panneau d'administration</p>
      </div>

      {/* Profile Section */}
      <div className="card">
        <div className="flex items-center gap-3 mb-6">
          <Cog6ToothIcon className="w-6 h-6 text-palx-400" />
          <h2 className="text-lg font-semibold text-white">Profil Administrateur</h2>
        </div>
        
        <div className="flex items-center gap-4 p-4 bg-dark-700/30 rounded-lg">
          {(user as any)?.avatarPath ? (
            <img 
              src={`http://localhost:5145/${(user as any).avatarPath}`}
              alt={user?.displayName || user?.username}
              className="w-16 h-16 rounded-full object-cover"
              onError={(e) => {
                e.currentTarget.style.display = 'none';
                e.currentTarget.nextElementSibling?.classList.remove('hidden');
              }}
            />
          ) : null}
          <div className={`w-16 h-16 rounded-full bg-gradient-to-br from-palx-500 to-palx-700 flex items-center justify-center text-white text-2xl font-bold ${(user as any)?.avatarPath ? 'hidden' : ''}`}>
            {(user?.displayName || user?.username)?.charAt(0).toUpperCase()}
          </div>
          <div>
            <p className="text-xl font-bold text-white">{user?.displayName || user?.username}</p>
            <p className="text-dark-400">@{user?.username}</p>
            <span 
              className="badge mt-2"
              style={{ 
                backgroundColor: (user as any)?.roleColor ? `${(user as any).roleColor}20` : undefined,
                color: (user as any)?.roleColor || undefined,
                borderColor: (user as any)?.roleColor || undefined
              }}
            >
              {(user as any)?.roleDisplayName || (user as any)?.roleName || user?.role}
            </span>
          </div>
        </div>
      </div>

      {/* System Status */}
      <div className="card">
        <div className="flex items-center gap-3 mb-6">
          <ServerIcon className="w-6 h-6 text-palx-400" />
          <h2 className="text-lg font-semibold text-white">État du système</h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="p-4 bg-dark-700/30 rounded-lg">
            <p className="text-dark-400 text-sm mb-1">Connexion SignalR</p>
            <div className="flex items-center gap-2">
              <span className={`w-3 h-3 rounded-full ${isConnected ? 'bg-success animate-pulse' : 'bg-danger'}`}></span>
              <span className={isConnected ? 'text-success' : 'text-danger'}>
                {isConnected ? 'Connecté' : 'Déconnecté'}
              </span>
            </div>
          </div>
          <div className="p-4 bg-dark-700/30 rounded-lg">
            <p className="text-dark-400 text-sm mb-1">Mode maintenance</p>
            <div className="flex items-center gap-2">
              <span className={`w-3 h-3 rounded-full ${maintenanceMode ? 'bg-warning' : 'bg-success'}`}></span>
              <span className={maintenanceMode ? 'text-warning' : 'text-success'}>
                {maintenanceMode ? 'Activé' : 'Désactivé'}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Broadcast Message */}
      <div className="card">
        <div className="flex items-center gap-3 mb-6">
          <MegaphoneIcon className="w-6 h-6 text-palx-400" />
          <h2 className="text-lg font-semibold text-white">Message Broadcast</h2>
        </div>
        
        <p className="text-dark-400 text-sm mb-4">
          Envoyez un message à tous les utilisateurs connectés en temps réel.
        </p>

        <div className="space-y-4">
          <textarea
            value={broadcastMessage}
            onChange={(e) => setBroadcastMessage(e.target.value)}
            className="input min-h-24"
            placeholder="Tapez votre message ici..."
          />
          <button 
            onClick={handleSendBroadcast}
            disabled={!broadcastMessage.trim()}
            className="btn-primary"
          >
            <MegaphoneIcon className="w-5 h-5" />
            Envoyer le broadcast
          </button>
        </div>
      </div>

      {/* Maintenance Mode */}
      <div className="card">
        <div className="flex items-center gap-3 mb-6">
          <WrenchScrewdriverIcon className="w-6 h-6 text-palx-400" />
          <h2 className="text-lg font-semibold text-white">Mode Maintenance</h2>
        </div>

        <div className="p-4 bg-warning/10 border border-warning/30 rounded-lg mb-4">
          <div className="flex items-start gap-3">
            <ExclamationTriangleIcon className="w-6 h-6 text-warning flex-shrink-0" />
            <div>
              <p className="text-warning font-medium">Attention</p>
              <p className="text-dark-300 text-sm mt-1">
                Activer le mode maintenance empêchera tous les utilisateurs de se connecter à l'application.
                Seuls les administrateurs pourront y accéder.
              </p>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <div>
            <label className="label">Message de maintenance (optionnel)</label>
            <input
              type="text"
              value={maintenanceMessage}
              onChange={(e) => setMaintenanceMessage(e.target.value)}
              className="input"
              placeholder="Ex: Maintenance en cours, retour prévu à 14h..."
            />
          </div>

          <button
            onClick={handleToggleMaintenance}
            className={maintenanceMode ? 'btn-success' : 'btn-danger'}
          >
            <WrenchScrewdriverIcon className="w-5 h-5" />
            {maintenanceMode ? 'Désactiver la maintenance' : 'Activer la maintenance'}
          </button>
        </div>
      </div>

      {/* Security */}
      <div className="card">
        <div className="flex items-center gap-3 mb-6">
          <ShieldCheckIcon className="w-6 h-6 text-palx-400" />
          <h2 className="text-lg font-semibold text-white">Sécurité</h2>
        </div>

        <div className="space-y-4">
          <div className="flex items-center justify-between p-4 bg-dark-700/30 rounded-lg">
            <div>
              <p className="text-white font-medium">Authentification à deux facteurs</p>
              <p className="text-dark-400 text-sm">Sécurisez votre compte admin</p>
            </div>
            <span className="badge badge-success">Bientôt</span>
          </div>
          <div className="flex items-center justify-between p-4 bg-dark-700/30 rounded-lg">
            <div>
              <p className="text-white font-medium">Sessions actives</p>
              <p className="text-dark-400 text-sm">Gérez vos sessions de connexion</p>
            </div>
            <span className="badge badge-primary">1 session</span>
          </div>
          <div className="flex items-center justify-between p-4 bg-dark-700/30 rounded-lg">
            <div>
              <p className="text-white font-medium">Dernière connexion</p>
              <p className="text-dark-400 text-sm">Historique des accès</p>
            </div>
            <span className="text-dark-300 text-sm">Maintenant</span>
          </div>
        </div>
      </div>

      {/* App Info */}
      <div className="card">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-white font-medium">PaL.Xtreme Admin Panel</p>
            <p className="text-dark-400 text-sm">Version 1.0.0</p>
          </div>
          <div className="text-right">
            <p className="text-dark-400 text-sm">© 2025 PaL.Xtreme</p>
            <p className="text-dark-500 text-xs">Tous droits réservés</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SettingsPage;
