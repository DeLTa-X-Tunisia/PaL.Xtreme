import React, { useState, useEffect, useCallback } from 'react';
import { 
  ShieldCheckIcon,
  ShieldExclamationIcon,
  BoltIcon,
  UserIcon,
  UsersIcon,
  KeyIcon,
  ChatBubbleLeftRightIcon,
  VideoCameraIcon,
  TrashIcon,
  PencilSquareIcon,
  EyeIcon,
  CheckCircleIcon,
  StarIcon,
  PlusIcon,
  XMarkIcon,
  ArrowPathIcon,
  ExclamationTriangleIcon,
  LockClosedIcon,
  Cog6ToothIcon,
  HomeModernIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { RoomRole, RoomPermission, PermissionGroup, CreateRoomRoleDto, UpdateRoomRoleDto } from '../types';

// Map des icônes pour les rôles
const iconMap: Record<string, React.ComponentType<React.SVGProps<SVGSVGElement>>> = {
  'crown': StarIcon,
  'shield-check': ShieldCheckIcon,
  'shield': ShieldExclamationIcon,
  'bolt': BoltIcon,
  'eye': EyeIcon,
  'user': UserIcon,
  'users': UsersIcon,
  'key': KeyIcon,
  'cog': Cog6ToothIcon,
};

// Icônes disponibles pour la sélection
const availableIcons = [
  { name: 'crown', label: 'Couronne', Icon: StarIcon },
  { name: 'shield-check', label: 'Bouclier Check', Icon: ShieldCheckIcon },
  { name: 'shield', label: 'Bouclier', Icon: ShieldExclamationIcon },
  { name: 'bolt', label: 'Éclair', Icon: BoltIcon },
  { name: 'eye', label: 'Œil', Icon: EyeIcon },
  { name: 'user', label: 'Utilisateur', Icon: UserIcon },
  { name: 'users', label: 'Groupe', Icon: UsersIcon },
  { name: 'key', label: 'Clé', Icon: KeyIcon },
  { name: 'cog', label: 'Engrenage', Icon: Cog6ToothIcon },
];

// Couleurs disponibles
const availableColors = [
  { value: '#FFD700', name: 'Or' },
  { value: '#E74C3C', name: 'Rouge' },
  { value: '#9B59B6', name: 'Violet' },
  { value: '#3498DB', name: 'Bleu' },
  { value: '#2ECC71', name: 'Vert' },
  { value: '#95A5A6', name: 'Gris' },
  { value: '#F39C12', name: 'Orange' },
  { value: '#1ABC9C', name: 'Turquoise' },
  { value: '#E91E63', name: 'Rose' },
  { value: '#34495E', name: 'Anthracite' },
];

// Map des icônes de catégories de permissions
const categoryIcons: Record<string, React.ComponentType<React.SVGProps<SVGSVGElement>>> = {
  general: Cog6ToothIcon,
  roles: KeyIcon,
  moderation: ShieldExclamationIcon,
  members: UsersIcon,
  media: VideoCameraIcon,
  base: ChatBubbleLeftRightIcon,
};

const RoomRolesPage: React.FC = () => {
  // State principal
  const [roles, setRoles] = useState<RoomRole[]>([]);
  const [permissions, setPermissions] = useState<PermissionGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedRole, setSelectedRole] = useState<RoomRole | null>(null);
  
  // Modal states
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [roleToDelete, setRoleToDelete] = useState<RoomRole | null>(null);
  
  // Form state
  const [formData, setFormData] = useState<{
    roleLevel: number;
    roleName: string;
    displayName: string;
    description: string;
    icon: string;
    color: string;
    isActive: boolean;
    permissionIds: number[];
  }>({
    roleLevel: 7,
    roleName: '',
    displayName: '',
    description: '',
    icon: 'user',
    color: '#95A5A6',
    isActive: true,
    permissionIds: [],
  });
  
  // Feedback
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Charger les données
  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [rolesData, permsData] = await Promise.all([
        apiService.getRoomRoles(),
        apiService.getRoomPermissions(),
      ]);
      setRoles(rolesData);
      setPermissions(permsData);
      if (rolesData.length > 0 && !selectedRole) {
        setSelectedRole(rolesData[0]);
      }
    } catch (err: any) {
      setError(err.message || 'Erreur lors du chargement des données');
      console.error('Error fetching data:', err);
    } finally {
      setLoading(false);
    }
  }, [selectedRole]);

  useEffect(() => {
    fetchData();
  }, []);

  // Helpers
  const getIcon = (iconName: string) => {
    return iconMap[iconName?.toLowerCase()] || UserIcon;
  };

  // Handlers - Create
  const handleOpenCreate = () => {
    setFormData({
      roleLevel: Math.max(...roles.map(r => r.roleLevel), 0) + 1,
      roleName: '',
      displayName: '',
      description: '',
      icon: 'user',
      color: '#95A5A6',
      isActive: true,
      permissionIds: [],
    });
    setShowCreateModal(true);
    setError(null);
    setSuccess(null);
  };

  const handleCreate = async () => {
    if (!formData.roleName || !formData.displayName) {
      setError('Le nom technique et le nom affiché sont obligatoires');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const dto: CreateRoomRoleDto = {
        roleLevel: formData.roleLevel,
        roleName: formData.roleName,
        displayName: formData.displayName,
        description: formData.description || undefined,
        icon: formData.icon,
        color: formData.color,
        permissionIds: formData.permissionIds,
      };
      const result = await apiService.createRoomRole(dto);
      if (result.success) {
        setSuccess('Rôle créé avec succès!');
        setShowCreateModal(false);
        await fetchData();
        if (result.role) {
          setSelectedRole(result.role);
        }
      } else {
        setError(result.message || 'Erreur lors de la création');
      }
    } catch (err: any) {
      setError(err.message || 'Erreur lors de la création');
    } finally {
      setSubmitting(false);
    }
  };

  // Handlers - Edit
  const handleOpenEdit = (role: RoomRole) => {
    setFormData({
      roleLevel: role.roleLevel,
      roleName: role.roleName,
      displayName: role.displayName,
      description: role.description || '',
      icon: role.icon,
      color: role.color,
      isActive: role.isActive,
      permissionIds: role.permissions.map(p => p.id),
    });
    setSelectedRole(role);
    setShowEditModal(true);
    setError(null);
    setSuccess(null);
  };

  const handleUpdate = async () => {
    if (!selectedRole) return;

    setSubmitting(true);
    setError(null);
    try {
      const dto: UpdateRoomRoleDto = {
        displayName: formData.displayName,
        description: formData.description || undefined,
        icon: formData.icon,
        color: formData.color,
        isActive: formData.isActive,
        permissionIds: formData.permissionIds,
      };
      const result = await apiService.updateRoomRole(selectedRole.id, dto);
      if (result.success) {
        setSuccess('Rôle mis à jour avec succès!');
        setShowEditModal(false);
        await fetchData();
        if (result.role) {
          setSelectedRole(result.role);
        }
      } else {
        setError(result.message || 'Erreur lors de la mise à jour');
      }
    } catch (err: any) {
      setError(err.message || 'Erreur lors de la mise à jour');
    } finally {
      setSubmitting(false);
    }
  };

  // Handlers - Delete
  const handleOpenDelete = (role: RoomRole) => {
    setRoleToDelete(role);
    setShowDeleteConfirm(true);
    setError(null);
  };

  const handleDelete = async () => {
    if (!roleToDelete) return;

    setSubmitting(true);
    setError(null);
    try {
      const result = await apiService.deleteRoomRole(roleToDelete.id);
      if (result.success) {
        setSuccess('Rôle supprimé avec succès!');
        setShowDeleteConfirm(false);
        setRoleToDelete(null);
        await fetchData();
        setSelectedRole(roles[0] || null);
      } else {
        setError(result.message || 'Erreur lors de la suppression');
      }
    } catch (err: any) {
      setError(err.message || 'Erreur lors de la suppression');
    } finally {
      setSubmitting(false);
    }
  };

  // Render
  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <ArrowPathIcon className="w-8 h-8 animate-spin text-palx-500" />
        <span className="ml-2 text-dark-400">Chargement des rôles...</span>
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
            Gestion des Rôles de Salons
          </h1>
          <p className="text-dark-400 mt-1">
            Configurez les rôles et permissions disponibles dans les salons
          </p>
        </div>
        <button
          onClick={handleOpenCreate}
          className="flex items-center gap-2 px-4 py-2 bg-palx-600 hover:bg-palx-700 text-white rounded-lg transition-colors"
        >
          <PlusIcon className="w-5 h-5" />
          Nouveau Rôle
        </button>
      </div>

      {/* Feedback messages */}
      {error && (
        <div className="bg-red-900/50 border border-red-600 text-red-200 px-4 py-3 rounded-lg flex items-center gap-2">
          <ExclamationTriangleIcon className="w-5 h-5" />
          {error}
          <button onClick={() => setError(null)} className="ml-auto">
            <XMarkIcon className="w-4 h-4" />
          </button>
        </div>
      )}
      {success && (
        <div className="bg-green-900/50 border border-green-600 text-green-200 px-4 py-3 rounded-lg flex items-center gap-2">
          <CheckCircleIcon className="w-5 h-5" />
          {success}
          <button onClick={() => setSuccess(null)} className="ml-auto">
            <XMarkIcon className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Hiérarchie visuelle */}
      <div className="card">
        <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
          <ShieldCheckIcon className="w-5 h-5 text-palx-400" />
          Hiérarchie des Rôles
        </h2>
        <div className="flex flex-wrap items-center gap-2">
          {roles.map((role, index) => {
            const Icon = getIcon(role.icon);
            return (
              <React.Fragment key={role.id}>
                <button
                  onClick={() => setSelectedRole(role)}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-all ${
                    selectedRole?.id === role.id
                      ? 'ring-2 ring-palx-500 bg-dark-700'
                      : 'bg-dark-800 hover:bg-dark-700'
                  }`}
                  style={{ borderLeft: `3px solid ${role.color}` }}
                >
                  <Icon className="w-5 h-5" style={{ color: role.color }} />
                  <span className="text-white font-medium">{role.displayName}</span>
                  {role.isSystem && (
                    <LockClosedIcon className="w-4 h-4 text-dark-500" title="Rôle système" />
                  )}
                </button>
                {index < roles.length - 1 && (
                  <span className="text-dark-500">→</span>
                )}
              </React.Fragment>
            );
          })}
        </div>
      </div>

      {/* Détails du rôle sélectionné */}
      {selectedRole && (
        <div className="card overflow-hidden">
          {/* Header du rôle */}
          <div 
            className="p-6 border-b border-dark-700"
            style={{ background: `linear-gradient(135deg, ${selectedRole.color}20, transparent)` }}
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-4">
                {React.createElement(getIcon(selectedRole.icon), {
                  className: "w-12 h-12",
                  style: { color: selectedRole.color },
                })}
                <div>
                  <h2 className="text-2xl font-bold text-white">{selectedRole.displayName}</h2>
                  <p className="text-dark-400">
                    Niveau {selectedRole.roleLevel} · <code className="text-sm bg-dark-700 px-2 py-0.5 rounded">{selectedRole.roleName}</code>
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => handleOpenEdit(selectedRole)}
                  className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                >
                  <PencilSquareIcon className="w-5 h-5" />
                  Modifier
                </button>
                {!selectedRole.isSystem && (
                  <button
                    onClick={() => handleOpenDelete(selectedRole)}
                    className="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors"
                  >
                    <TrashIcon className="w-5 h-5" />
                    Supprimer
                  </button>
                )}
              </div>
            </div>
            <p className="mt-4 text-dark-300">{selectedRole.description}</p>
          </div>

          {/* Permissions */}
          <div className="p-6">
            <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
              <KeyIcon className="w-5 h-5 text-palx-500" />
              Permissions ({selectedRole.permissions.length})
            </h3>
            
            {selectedRole.permissions.length > 0 ? (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                {selectedRole.permissions.map(perm => (
                  <div
                    key={perm.id}
                    className="flex items-center gap-3 p-3 bg-dark-800 rounded-lg"
                  >
                    <CheckCircleIcon className="w-5 h-5 text-green-500 flex-shrink-0" />
                    <div>
                      <p className="text-white text-sm font-medium">{perm.displayName}</p>
                      <p className="text-dark-500 text-xs capitalize">{perm.category}</p>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-dark-500">Aucune permission attribuée</p>
            )}
          </div>
        </div>
      )}

      {/* Modal Création */}
      {showCreateModal && (
        <Modal title="Créer un nouveau rôle" onClose={() => setShowCreateModal(false)}>
          <RoleForm
            formData={formData}
            setFormData={setFormData}
            permissions={permissions}
            onSubmit={handleCreate}
            onCancel={() => setShowCreateModal(false)}
            submitting={submitting}
            submitLabel="Créer le rôle"
            error={error}
          />
        </Modal>
      )}

      {/* Modal Édition */}
      {showEditModal && selectedRole && (
        <Modal title={`Modifier: ${selectedRole.displayName}`} onClose={() => setShowEditModal(false)}>
          <RoleForm
            formData={formData}
            setFormData={setFormData}
            permissions={permissions}
            onSubmit={handleUpdate}
            onCancel={() => setShowEditModal(false)}
            submitting={submitting}
            submitLabel="Enregistrer"
            error={error}
            isEdit
            isSystem={selectedRole.isSystem}
          />
        </Modal>
      )}

      {/* Modal Confirmation suppression */}
      {showDeleteConfirm && roleToDelete && (
        <Modal title="Confirmer la suppression" onClose={() => setShowDeleteConfirm(false)}>
          <div className="space-y-4">
            <p className="text-dark-300">
              Êtes-vous sûr de vouloir supprimer le rôle <strong className="text-white">{roleToDelete.displayName}</strong> ?
            </p>
            <p className="text-yellow-500 text-sm flex items-center gap-2">
              <ExclamationTriangleIcon className="w-5 h-5" />
              Cette action est irréversible.
            </p>
            {error && (
              <div className="bg-red-900/50 border border-red-600 text-red-200 px-3 py-2 rounded text-sm">
                {error}
              </div>
            )}
            <div className="flex justify-end gap-3 pt-4">
              <button
                onClick={() => setShowDeleteConfirm(false)}
                className="px-4 py-2 bg-dark-600 hover:bg-dark-500 text-white rounded-lg"
              >
                Annuler
              </button>
              <button
                onClick={handleDelete}
                disabled={submitting}
                className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg disabled:opacity-50 flex items-center gap-2"
              >
                {submitting && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
                Supprimer
              </button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
};

// Composant Modal
const Modal: React.FC<{
  title: string;
  onClose: () => void;
  children: React.ReactNode;
}> = ({ title, onClose, children }) => (
  <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4">
    <div className="bg-dark-900 rounded-xl border border-dark-700 w-full max-w-2xl max-h-[90vh] overflow-hidden flex flex-col">
      <div className="flex items-center justify-between p-4 border-b border-dark-700">
        <h2 className="text-xl font-semibold text-white">{title}</h2>
        <button onClick={onClose} className="text-dark-400 hover:text-white">
          <XMarkIcon className="w-6 h-6" />
        </button>
      </div>
      <div className="p-4 overflow-y-auto flex-1">
        {children}
      </div>
    </div>
  </div>
);

// Composant Formulaire
const RoleForm: React.FC<{
  formData: any;
  setFormData: (fn: (prev: any) => any) => void;
  permissions: PermissionGroup[];
  onSubmit: () => void;
  onCancel: () => void;
  submitting: boolean;
  submitLabel: string;
  error: string | null;
  isEdit?: boolean;
  isSystem?: boolean;
}> = ({ formData, setFormData, permissions, onSubmit, onCancel, submitting, submitLabel, error, isEdit, isSystem }) => {
  const togglePermission = (permId: number) => {
    setFormData((prev: any) => ({
      ...prev,
      permissionIds: prev.permissionIds.includes(permId)
        ? prev.permissionIds.filter((id: number) => id !== permId)
        : [...prev.permissionIds, permId],
    }));
  };

  const toggleCategory = (perms: RoomPermission[]) => {
    const permIds = perms.map(p => p.id);
    const allSelected = permIds.every(id => formData.permissionIds.includes(id));
    
    setFormData((prev: any) => ({
      ...prev,
      permissionIds: allSelected
        ? prev.permissionIds.filter((id: number) => !permIds.includes(id))
        : [...new Set([...prev.permissionIds, ...permIds])],
    }));
  };

  return (
    <div className="space-y-6">
      {error && (
        <div className="bg-red-900/50 border border-red-600 text-red-200 px-3 py-2 rounded text-sm">
          {error}
        </div>
      )}

      {/* Infos de base */}
      <div className="grid grid-cols-2 gap-4">
        {!isEdit && (
          <>
            <div>
              <label className="block text-sm font-medium text-dark-300 mb-1">Niveau</label>
              <input
                type="number"
                value={formData.roleLevel}
                onChange={e => setFormData((p: any) => ({ ...p, roleLevel: parseInt(e.target.value) || 0 }))}
                className="w-full px-3 py-2 bg-dark-800 border border-dark-700 rounded-lg text-white focus:ring-palx-500 focus:border-palx-500"
                min={1}
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-dark-300 mb-1">Nom technique</label>
              <input
                type="text"
                value={formData.roleName}
                onChange={e => setFormData((p: any) => ({ ...p, roleName: e.target.value }))}
                placeholder="CustomRole"
                className="w-full px-3 py-2 bg-dark-800 border border-dark-700 rounded-lg text-white focus:ring-palx-500 focus:border-palx-500"
              />
            </div>
          </>
        )}
        <div className={isEdit ? 'col-span-2' : ''}>
          <label className="block text-sm font-medium text-dark-300 mb-1">Nom affiché</label>
          <input
            type="text"
            value={formData.displayName}
            onChange={e => setFormData((p: any) => ({ ...p, displayName: e.target.value }))}
            placeholder="Mon Rôle Personnalisé"
            className="w-full px-3 py-2 bg-dark-800 border border-dark-700 rounded-lg text-white focus:ring-palx-500 focus:border-palx-500"
          />
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium text-dark-300 mb-1">Description</label>
        <textarea
          value={formData.description}
          onChange={e => setFormData((p: any) => ({ ...p, description: e.target.value }))}
          rows={2}
          className="w-full px-3 py-2 bg-dark-800 border border-dark-700 rounded-lg text-white resize-none focus:ring-palx-500 focus:border-palx-500"
        />
      </div>

      {/* Icône et couleur */}
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium text-dark-300 mb-2">Icône</label>
          <div className="flex flex-wrap gap-2">
            {availableIcons.map(({ name, Icon }) => (
              <button
                key={name}
                type="button"
                onClick={() => setFormData((p: any) => ({ ...p, icon: name }))}
                className={`p-2 rounded-lg transition-colors ${
                  formData.icon === name
                    ? 'bg-palx-600 text-white'
                    : 'bg-dark-800 text-dark-400 hover:text-white'
                }`}
                title={name}
              >
                <Icon className="w-5 h-5" />
              </button>
            ))}
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-dark-300 mb-2">Couleur</label>
          <div className="flex flex-wrap gap-2">
            {availableColors.map(({ value, name }) => (
              <button
                key={value}
                type="button"
                onClick={() => setFormData((p: any) => ({ ...p, color: value }))}
                className={`w-8 h-8 rounded-lg transition-transform ${
                  formData.color === value ? 'ring-2 ring-white scale-110' : ''
                }`}
                style={{ backgroundColor: value }}
                title={name}
              />
            ))}
          </div>
        </div>
      </div>

      {/* Aperçu */}
      <div className="bg-dark-800 rounded-lg p-4">
        <span className="text-sm text-dark-400">Aperçu:</span>
        <div className="flex items-center gap-3 mt-2">
          {React.createElement(iconMap[formData.icon] || UserIcon, {
            className: "w-8 h-8",
            style: { color: formData.color },
          })}
          <span className="text-xl font-bold" style={{ color: formData.color }}>
            {formData.displayName || 'Nom du rôle'}
          </span>
        </div>
      </div>

      {/* Permissions */}
      <div>
        <label className="block text-sm font-medium text-dark-300 mb-3">
          Permissions ({formData.permissionIds.length} sélectionnées)
        </label>
        <div className="space-y-4 max-h-64 overflow-y-auto pr-2">
          {permissions.map(group => {
            const CategoryIcon = categoryIcons[group.category] || KeyIcon;
            const groupPermIds = group.permissions.map(p => p.id);
            const allSelected = groupPermIds.every(id => formData.permissionIds.includes(id));
            const someSelected = groupPermIds.some(id => formData.permissionIds.includes(id));
            
            return (
              <div key={group.category} className="bg-dark-800 rounded-lg p-3">
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <CategoryIcon className="w-4 h-4 text-palx-500" />
                    <span className="font-medium text-white">{group.categoryDisplayName}</span>
                  </div>
                  <button
                    type="button"
                    onClick={() => toggleCategory(group.permissions)}
                    className={`text-xs px-2 py-1 rounded ${
                      allSelected 
                        ? 'bg-palx-600 text-white' 
                        : someSelected 
                          ? 'bg-palx-600/50 text-white'
                          : 'bg-dark-700 text-dark-400'
                    }`}
                  >
                    {allSelected ? 'Tout décocher' : 'Tout cocher'}
                  </button>
                </div>
                <div className="grid grid-cols-1 gap-1">
                  {group.permissions.map(perm => (
                    <label
                      key={perm.id}
                      className="flex items-center gap-2 p-2 rounded hover:bg-dark-700 cursor-pointer"
                    >
                      <input
                        type="checkbox"
                        checked={formData.permissionIds.includes(perm.id)}
                        onChange={() => togglePermission(perm.id)}
                        className="w-4 h-4 rounded border-dark-600 text-palx-600 focus:ring-palx-500 bg-dark-700"
                      />
                      <span className="text-sm text-dark-300">{perm.displayName}</span>
                    </label>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 pt-4 border-t border-dark-700">
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2 bg-dark-700 hover:bg-dark-600 text-white rounded-lg"
        >
          Annuler
        </button>
        <button
          type="button"
          onClick={onSubmit}
          disabled={submitting}
          className="px-4 py-2 bg-palx-600 hover:bg-palx-700 text-white rounded-lg disabled:opacity-50 flex items-center gap-2"
        >
          {submitting && <ArrowPathIcon className="w-4 h-4 animate-spin" />}
          {submitLabel}
        </button>
      </div>
    </div>
  );
};

export default RoomRolesPage;
