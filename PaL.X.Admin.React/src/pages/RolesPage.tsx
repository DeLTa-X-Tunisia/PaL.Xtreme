import { useState, useEffect } from 'react';
import { 
  ShieldCheckIcon,
  UserGroupIcon,
  TrophyIcon,
  StarIcon,
  WrenchIcon,
  UserIcon,
  HandRaisedIcon,
  SparklesIcon,
  PencilIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { Role } from '../types';

// Map des icônes par nom
const iconMap: Record<string, React.ComponentType<React.SVGProps<SVGSVGElement>>> = {
  trophy: TrophyIcon,
  crown: StarIcon,
  shield: ShieldCheckIcon,
  cog: WrenchIcon,
  pencil: PencilIcon,
  user: UserIcon,
  handshake: HandRaisedIcon,
  star: StarIcon,
  sparkles: SparklesIcon,
};

const RolesPage: React.FC = () => {
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchRoles = async () => {
      try {
        const data = await apiService.getRoles();
        setRoles(data);
      } catch (error) {
        console.error('Failed to fetch roles:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchRoles();
  }, []);

  const getIcon = (iconName: string) => {
    const IconComponent = iconMap[iconName.toLowerCase()] || UserIcon;
    return IconComponent;
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
            <ShieldCheckIcon className="w-8 h-8 text-palx-400" />
            Hiérarchie des Rôles
          </h1>
          <p className="text-dark-400 mt-1">
            Gestion et visualisation des rôles du serveur
          </p>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-dark-400 text-sm">{roles.length} rôles définis</span>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-warning/20 flex items-center justify-center">
            <TrophyIcon className="w-6 h-6 text-warning" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Rôle le plus haut</p>
            <p className="text-lg font-bold text-white">{roles[0]?.displayName || '-'}</p>
          </div>
        </div>
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-palx-500/20 flex items-center justify-center">
            <UserGroupIcon className="w-6 h-6 text-palx-400" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Total utilisateurs</p>
            <p className="text-lg font-bold text-white">
              {roles.reduce((sum, r) => sum + r.userCount, 0)}
            </p>
          </div>
        </div>
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-success/20 flex items-center justify-center">
            <ShieldCheckIcon className="w-6 h-6 text-success" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Rôles admin</p>
            <p className="text-lg font-bold text-white">
              {roles.filter(r => r.roleLevel <= 5).length}
            </p>
          </div>
        </div>
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-info/20 flex items-center justify-center">
            <UserIcon className="w-6 h-6 text-info" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Rôle par défaut</p>
            <p className="text-lg font-bold text-white">{roles[roles.length - 1]?.displayName || '-'}</p>
          </div>
        </div>
      </div>

      {/* Roles Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {roles.map((role, index) => {
          const IconComponent = getIcon(role.icon);
          return (
            <div 
              key={role.id} 
              className="card hover:border-dark-600 transition-all duration-300 group"
              style={{ borderLeftColor: role.color, borderLeftWidth: '4px' }}
            >
              <div className="flex items-start gap-4">
                {/* Icon */}
                <div 
                  className="w-14 h-14 rounded-xl flex items-center justify-center shrink-0 transition-transform group-hover:scale-110"
                  style={{ backgroundColor: `${role.color}20` }}
                >
                  <IconComponent 
                    className="w-7 h-7" 
                    style={{ color: role.color }}
                  />
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
                    <span className="badge bg-dark-700/50 text-dark-300 text-xs">
                      Niveau {role.roleLevel}
                    </span>
                    {index === 0 && (
                      <span className="badge bg-warning/20 text-warning text-xs">
                        👑 Plus haut
                      </span>
                    )}
                  </div>

                  <p className="text-dark-300 text-sm mb-3 line-clamp-2">
                    {role.description || 'Aucune description disponible'}
                  </p>

                  <div className="flex items-center gap-4 text-xs">
                    <div className="flex items-center gap-1.5 text-dark-400">
                      <UserGroupIcon className="w-4 h-4" />
                      <span>{role.userCount} utilisateur{role.userCount > 1 ? 's' : ''}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-dark-400">
                      <span className="font-mono bg-dark-700/50 px-2 py-0.5 rounded">
                        {role.roleName}
                      </span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <span 
                        className="w-4 h-4 rounded-full border-2"
                        style={{ backgroundColor: role.color, borderColor: role.color }}
                      />
                      <span className="text-dark-400 font-mono">{role.color}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Hierarchy Visual */}
      <div className="card">
        <h3 className="text-lg font-semibold text-white mb-6 flex items-center gap-2">
          <SparklesIcon className="w-5 h-5 text-palx-400" />
          Pyramide hiérarchique
        </h3>
        <div className="space-y-2">
          {roles.map((role, index) => {
            const IconComponent = getIcon(role.icon);
            const widthPercent = 100 - (index * (60 / roles.length));
            return (
              <div 
                key={role.id}
                className="flex items-center gap-4 py-3 px-4 rounded-lg transition-all hover:bg-dark-700/30"
                style={{ 
                  width: `${widthPercent}%`,
                  marginLeft: 'auto',
                  marginRight: 'auto',
                  backgroundColor: `${role.color}10`,
                  borderLeft: `3px solid ${role.color}`
                }}
              >
                <div 
                  className="w-8 h-8 rounded-lg flex items-center justify-center"
                  style={{ backgroundColor: `${role.color}30` }}
                >
                  <IconComponent className="w-4 h-4" style={{ color: role.color }} />
                </div>
                <div className="flex-1">
                  <span className="font-medium" style={{ color: role.color }}>
                    {role.displayName}
                  </span>
                </div>
                <span className="text-dark-400 text-sm">
                  {role.userCount} membre{role.userCount > 1 ? 's' : ''}
                </span>
                <span className="badge bg-dark-700/50 text-dark-300 text-xs">
                  Niv. {role.roleLevel}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export default RolesPage;
