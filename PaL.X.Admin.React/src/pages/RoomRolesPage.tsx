import { useState, useEffect } from 'react';
import { 
  HomeModernIcon,
  CrownIcon,
  ShieldCheckIcon,
  ShieldExclamationIcon,
  BoltIcon,
  UserIcon,
  UsersIcon,
  KeyIcon,
  ChatBubbleLeftRightIcon,
  MicrophoneIcon,
  VideoCameraIcon,
  TrashIcon,
  NoSymbolIcon,
  PencilSquareIcon,
  EyeIcon,
  CheckCircleIcon,
  StarIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { RoomRole } from '../types';

// Définition statique des rôles de salons avec leurs permissions
const defaultRoomRoles: RoomRole[] = [
  {
    id: 1,
    roleLevel: 1,
    roleName: 'RoomOwner',
    displayName: 'Propriétaire du Salon',
    icon: 'crown',
    color: '#FFD700',
    description: 'Contrôle total sur le salon. Peut modifier tous les paramètres, gérer les rôles et supprimer le salon.',
    permissions: [
      'Modifier les paramètres du salon',
      'Supprimer le salon',
      'Attribuer tous les rôles',
      'Gérer les abonnements',
      'Configurer le bot',
      'Accès complet au studio',
      'Kicket et bannir',
      'Muter les utilisateurs',
      'Modérer les messages',
    ],
  },
  {
    id: 2,
    roleLevel: 2,
    roleName: 'RoomSuperAdmin',
    displayName: 'Super Administrateur',
    icon: 'shield-check',
    color: '#E74C3C',
    description: 'Pouvoirs étendus de gestion. Peut attribuer les rôles Admin et inférieurs.',
    permissions: [
      'Modifier les paramètres du salon',
      'Attribuer les rôles Admin et inférieurs',
      'Gérer la modération',
      'Configurer le bot',
      'Accès au studio',
      'Kicker et bannir',
      'Muter les utilisateurs',
      'Modérer les messages',
    ],
  },
  {
    id: 3,
    roleLevel: 3,
    roleName: 'RoomAdmin',
    displayName: 'Administrateur',
    icon: 'shield',
    color: '#9B59B6',
    description: 'Gère la modération et les membres. Peut attribuer les rôles Modérateur et inférieurs.',
    permissions: [
      'Attribuer les rôles Mod et inférieurs',
      'Gérer la modération',
      'Kicker et bannir',
      'Muter les utilisateurs',
      'Modérer les messages',
      'Inviter des membres',
    ],
  },
  {
    id: 4,
    roleLevel: 4,
    roleName: 'PowerUser',
    displayName: 'Utilisateur Avancé',
    icon: 'bolt',
    color: '#3498DB',
    description: 'Utilisateur de confiance avec des privilèges étendus comme le partage vidéo prioritaire.',
    permissions: [
      'Priorité micro/caméra',
      'Inviter des membres',
      'Voir la liste des membres',
      'Accès aux statistiques basiques',
      'Partage de fichiers',
    ],
  },
  {
    id: 5,
    roleLevel: 5,
    roleName: 'RoomModerator',
    displayName: 'Modérateur',
    icon: 'eye',
    color: '#2ECC71',
    description: 'Surveille le chat et peut avertir ou muter les utilisateurs problématiques.',
    permissions: [
      'Muter les utilisateurs',
      'Avertir les utilisateurs',
      'Supprimer des messages',
      'Signaler au propriétaire',
      'Voir la liste des membres',
    ],
  },
  {
    id: 6,
    roleLevel: 6,
    roleName: 'RoomMember',
    displayName: 'Membre',
    icon: 'user',
    color: '#95A5A6',
    description: 'Membre standard du salon avec les permissions de base.',
    permissions: [
      'Envoyer des messages',
      'Voir le chat',
      'Demander le micro',
      'Demander la caméra',
      'Voir les membres en ligne',
    ],
  },
];

// Map des icônes
const iconMap: Record<string, React.ComponentType<React.SVGProps<SVGSVGElement>>> = {
  'crown': StarIcon,
  'shield-check': ShieldCheckIcon,
  'shield': ShieldExclamationIcon,
  'bolt': BoltIcon,
  'eye': EyeIcon,
  'user': UserIcon,
  'users': UsersIcon,
  'home': HomeModernIcon,
};

// Map des icônes de permissions
const permissionIconMap: Record<string, React.ComponentType<React.SVGProps<SVGSVGElement>>> = {
  'Modifier': PencilSquareIcon,
  'Supprimer': TrashIcon,
  'Attribuer': KeyIcon,
  'Gérer': ShieldCheckIcon,
  'Configurer': PencilSquareIcon,
  'Accès': EyeIcon,
  'Kicker': NoSymbolIcon,
  'Muter': MicrophoneIcon,
  'Modérer': ChatBubbleLeftRightIcon,
  'Inviter': UsersIcon,
  'Priorité': BoltIcon,
  'Voir': EyeIcon,
  'Partage': VideoCameraIcon,
  'Envoyer': ChatBubbleLeftRightIcon,
  'Demander': MicrophoneIcon,
  'Avertir': ShieldExclamationIcon,
  'Signaler': ShieldExclamationIcon,
};

const getPermissionIcon = (permission: string) => {
  for (const [key, Icon] of Object.entries(permissionIconMap)) {
    if (permission.startsWith(key)) {
      return Icon;
    }
  }
  return CheckCircleIcon;
};

const RoomRolesPage: React.FC = () => {
  const [roles, setRoles] = useState<RoomRole[]>(defaultRoomRoles);
  const [loading, setLoading] = useState(true);
  const [selectedRole, setSelectedRole] = useState<RoomRole | null>(null);

  useEffect(() => {
    const fetchRoles = async () => {
      try {
        // Essayer de récupérer depuis l'API, sinon utiliser les rôles par défaut
        const data = await apiService.getRoomRoles();
        if (data && data.length > 0) {
          setRoles(data);
        }
      } catch (error) {
        console.log('Using default room roles');
        // Garder les rôles par défaut
      } finally {
        setLoading(false);
      }
    };

    fetchRoles();
  }, []);

  const getIcon = (iconName: string) => {
    return iconMap[iconName.toLowerCase()] || UserIcon;
  };

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
            <HomeModernIcon className="w-8 h-8 text-palx-400" />
            Rôles des Salons
          </h1>
          <p className="text-dark-400 mt-1">
            Hiérarchie et permissions des rôles dans les salons
          </p>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-dark-400 text-sm">{roles.length} rôles définis</span>
        </div>
      </div>

      {/* Info Banner */}
      <div className="bg-gradient-to-r from-palx-600/20 to-palx-500/10 border border-palx-500/30 rounded-xl p-4">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 rounded-lg bg-palx-500/20 flex items-center justify-center shrink-0">
            <ShieldCheckIcon className="w-5 h-5 text-palx-400" />
          </div>
          <div>
            <h3 className="text-white font-semibold">Système de Hiérarchie</h3>
            <p className="text-dark-300 text-sm mt-1">
              Chaque salon possède sa propre hiérarchie de rôles. Un utilisateur ne peut attribuer que les rôles 
              inférieurs au sien. Le propriétaire a le contrôle total sur son salon.
            </p>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Roles List */}
        <div className="lg:col-span-2 space-y-4">
          {roles.map((role, index) => {
            const IconComponent = getIcon(role.icon);
            const isSelected = selectedRole?.id === role.id;
            
            return (
              <div 
                key={role.id} 
                className={`card hover:border-dark-600 transition-all duration-300 cursor-pointer group ${
                  isSelected ? 'ring-2 ring-palx-500 border-palx-500' : ''
                }`}
                style={{ borderLeftColor: role.color, borderLeftWidth: '4px' }}
                onClick={() => setSelectedRole(role)}
              >
                <div className="flex items-start gap-4">
                  {/* Icon & Level */}
                  <div className="flex flex-col items-center gap-2">
                    <div 
                      className="w-14 h-14 rounded-xl flex items-center justify-center shrink-0 transition-transform group-hover:scale-110"
                      style={{ backgroundColor: `${role.color}20` }}
                    >
                      <IconComponent 
                        className="w-7 h-7" 
                        style={{ color: role.color }}
                      />
                    </div>
                    <span 
                      className="text-xs font-bold px-2 py-0.5 rounded-full"
                      style={{ backgroundColor: `${role.color}20`, color: role.color }}
                    >
                      Nv. {role.roleLevel}
                    </span>
                  </div>

                  {/* Content */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 mb-2">
                      <h3 
                        className="text-lg font-bold"
                        style={{ color: role.color }}
                      >
                        {role.displayName}
                      </h3>
                      <span className="text-xs text-dark-500 bg-dark-800 px-2 py-0.5 rounded-full font-mono">
                        {role.roleName}
                      </span>
                    </div>
                    
                    <p className="text-dark-400 text-sm mb-3">
                      {role.description}
                    </p>

                    {/* Permissions Preview */}
                    <div className="flex flex-wrap gap-2">
                      {role.permissions.slice(0, 4).map((perm, idx) => (
                        <span 
                          key={idx}
                          className="text-xs px-2 py-1 rounded-lg bg-dark-800 text-dark-300 flex items-center gap-1"
                        >
                          {React.createElement(getPermissionIcon(perm), { className: 'w-3 h-3' })}
                          {perm}
                        </span>
                      ))}
                      {role.permissions.length > 4 && (
                        <span className="text-xs px-2 py-1 rounded-lg bg-dark-700 text-dark-400">
                          +{role.permissions.length - 4} autres
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Arrow indicator */}
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center transition-all ${
                    isSelected ? 'bg-palx-500 text-white' : 'bg-dark-800 text-dark-400 group-hover:bg-dark-700'
                  }`}>
                    <EyeIcon className="w-4 h-4" />
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Detail Panel */}
        <div className="lg:col-span-1">
          <div className="card sticky top-6">
            {selectedRole ? (
              <div className="space-y-4">
                {/* Header */}
                <div className="text-center pb-4 border-b border-dark-700">
                  <div 
                    className="w-20 h-20 rounded-2xl flex items-center justify-center mx-auto mb-3"
                    style={{ backgroundColor: `${selectedRole.color}20` }}
                  >
                    {React.createElement(getIcon(selectedRole.icon), {
                      className: 'w-10 h-10',
                      style: { color: selectedRole.color }
                    })}
                  </div>
                  <h2 
                    className="text-xl font-bold"
                    style={{ color: selectedRole.color }}
                  >
                    {selectedRole.displayName}
                  </h2>
                  <p className="text-dark-400 text-sm mt-1">
                    Niveau {selectedRole.roleLevel}
                  </p>
                </div>

                {/* Description */}
                <div>
                  <h4 className="text-white font-semibold text-sm mb-2">Description</h4>
                  <p className="text-dark-400 text-sm">
                    {selectedRole.description}
                  </p>
                </div>

                {/* Permissions */}
                <div>
                  <h4 className="text-white font-semibold text-sm mb-3">
                    Permissions ({selectedRole.permissions.length})
                  </h4>
                  <div className="space-y-2">
                    {selectedRole.permissions.map((perm, idx) => {
                      const PermIcon = getPermissionIcon(perm);
                      return (
                        <div 
                          key={idx}
                          className="flex items-center gap-3 p-2 rounded-lg bg-dark-800/50 hover:bg-dark-800 transition-colors"
                        >
                          <div 
                            className="w-8 h-8 rounded-lg flex items-center justify-center"
                            style={{ backgroundColor: `${selectedRole.color}15` }}
                          >
                            <PermIcon 
                              className="w-4 h-4" 
                              style={{ color: selectedRole.color }}
                            />
                          </div>
                          <span className="text-dark-300 text-sm">{perm}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Can Assign */}
                {selectedRole.roleLevel < 6 && (
                  <div className="pt-4 border-t border-dark-700">
                    <h4 className="text-white font-semibold text-sm mb-2">Peut attribuer</h4>
                    <div className="flex flex-wrap gap-2">
                      {roles
                        .filter(r => r.roleLevel > selectedRole.roleLevel)
                        .map(r => (
                          <span 
                            key={r.id}
                            className="text-xs px-2 py-1 rounded-lg"
                            style={{ backgroundColor: `${r.color}20`, color: r.color }}
                          >
                            {r.displayName}
                          </span>
                        ))
                      }
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <div className="text-center py-8">
                <div className="w-16 h-16 rounded-2xl bg-dark-800 flex items-center justify-center mx-auto mb-4">
                  <HomeModernIcon className="w-8 h-8 text-dark-500" />
                </div>
                <h3 className="text-white font-semibold mb-2">Sélectionnez un rôle</h3>
                <p className="text-dark-400 text-sm">
                  Cliquez sur un rôle pour voir ses détails et permissions
                </p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Hierarchy Visual */}
      <div className="card">
        <h3 className="text-white font-semibold mb-4 flex items-center gap-2">
          <ShieldCheckIcon className="w-5 h-5 text-palx-400" />
          Visualisation de la Hiérarchie
        </h3>
        <div className="flex items-center justify-between overflow-x-auto pb-2">
          {roles.map((role, index) => {
            const IconComponent = getIcon(role.icon);
            return (
              <div key={role.id} className="flex items-center">
                <div 
                  className="flex flex-col items-center min-w-[100px] cursor-pointer hover:scale-105 transition-transform"
                  onClick={() => setSelectedRole(role)}
                >
                  <div 
                    className="w-12 h-12 rounded-xl flex items-center justify-center mb-2"
                    style={{ backgroundColor: `${role.color}20` }}
                  >
                    <IconComponent className="w-6 h-6" style={{ color: role.color }} />
                  </div>
                  <span 
                    className="text-xs font-medium text-center"
                    style={{ color: role.color }}
                  >
                    {role.displayName.split(' ')[0]}
                  </span>
                  <span className="text-xs text-dark-500">Nv. {role.roleLevel}</span>
                </div>
                {index < roles.length - 1 && (
                  <div className="w-8 h-0.5 bg-gradient-to-r from-dark-600 to-dark-700 mx-2" />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export default RoomRolesPage;
