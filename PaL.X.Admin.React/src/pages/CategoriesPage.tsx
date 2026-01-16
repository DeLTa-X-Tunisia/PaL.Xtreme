import { useState, useEffect } from 'react';
import {
  FolderIcon,
  PlusIcon,
  PencilIcon,
  TrashIcon,
  EyeIcon,
  EyeSlashIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';

interface Category {
  id: number;
  name: string;
  description: string | null;
  icon: string | null;
  color: string;
  textColor: string;
  order: number;
  isVisible: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  subCategoriesCount: number;
  roomsCount: number;
}

interface CategoryFormData {
  name: string;
  description: string;
  icon: string;
  color: string;
  textColor: string;
  order: number;
  isVisible: boolean;
  isActive: boolean;
}

const defaultFormData: CategoryFormData = {
  name: '',
  description: '',
  icon: 'folder',
  color: '#3498DB',
  textColor: '#FFFFFF',
  order: 0,
  isVisible: true,
  isActive: true,
};

const CategoriesPage: React.FC = () => {
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Dialog state
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState<CategoryFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);

  // Delete confirmation
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deletingCategory, setDeletingCategory] = useState<Category | null>(null);

  const fetchCategories = async () => {
    try {
      setLoading(true);
      const response = await apiService.getCategories();
      setCategories(response);
      setError(null);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors du chargement des catégories');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategories();
  }, []);

  const handleOpenCreate = () => {
    setFormData(defaultFormData);
    setDialogMode('create');
    setEditingId(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (category: Category) => {
    setFormData({
      name: category.name,
      description: category.description || '',
      icon: category.icon || 'folder',
      color: category.color,
      textColor: category.textColor,
      order: category.order,
      isVisible: category.isVisible,
      isActive: category.isActive,
    });
    setDialogMode('edit');
    setEditingId(category.id);
    setDialogOpen(true);
  };

  const handleCloseDialog = () => {
    setDialogOpen(false);
    setFormData(defaultFormData);
    setEditingId(null);
  };

  const handleSubmit = async () => {
    if (!formData.name.trim()) {
      setError('Le nom est requis');
      return;
    }

    try {
      setSubmitting(true);
      if (dialogMode === 'create') {
        await apiService.createCategory(formData);
        setSuccess('Catégorie créée avec succès');
      } else {
        await apiService.updateCategory(editingId!, formData);
        setSuccess('Catégorie modifiée avec succès');
      }
      handleCloseDialog();
      fetchCategories();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de la sauvegarde');
    } finally {
      setSubmitting(false);
    }
  };

  const handleOpenDelete = (category: Category) => {
    setDeletingCategory(category);
    setDeleteDialogOpen(true);
  };

  const handleCloseDelete = () => {
    setDeleteDialogOpen(false);
    setDeletingCategory(null);
  };

  const handleDelete = async () => {
    if (!deletingCategory) return;

    try {
      setSubmitting(true);
      await apiService.deleteCategory(deletingCategory.id);
      setSuccess('Catégorie supprimée avec succès');
      handleCloseDelete();
      fetchCategories();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de la suppression');
    } finally {
      setSubmitting(false);
    }
  };

  // Auto-dismiss alerts
  useEffect(() => {
    if (success) {
      const timer = setTimeout(() => setSuccess(null), 3000);
      return () => clearTimeout(timer);
    }
  }, [success]);

  useEffect(() => {
    if (error) {
      const timer = setTimeout(() => setError(null), 5000);
      return () => clearTimeout(timer);
    }
  }, [error]);

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
            <FolderIcon className="w-8 h-8 text-palx-400" />
            Catégories
          </h1>
          <p className="text-dark-400 mt-1">
            Gérez les catégories de salons
          </p>
        </div>
        <button
          onClick={handleOpenCreate}
          className="btn-primary flex items-center gap-2"
        >
          <PlusIcon className="w-5 h-5" />
          Nouvelle Catégorie
        </button>
      </div>

      {/* Alerts */}
      {error && (
        <div className="bg-danger/20 border border-danger/50 text-danger px-4 py-3 rounded-lg flex items-center justify-between">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-danger hover:text-white">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>
      )}
      {success && (
        <div className="bg-success/20 border border-success/50 text-success px-4 py-3 rounded-lg flex items-center justify-between">
          <span>{success}</span>
          <button onClick={() => setSuccess(null)} className="text-success hover:text-white">
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>
      )}

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="card bg-gradient-to-br from-purple-600/20 to-purple-800/20 border-purple-500/30">
          <p className="text-dark-400 text-sm">Catégories totales</p>
          <p className="text-3xl font-bold text-white mt-1">{categories.length}</p>
        </div>
        <div className="card bg-gradient-to-br from-green-600/20 to-green-800/20 border-green-500/30">
          <p className="text-dark-400 text-sm">Actives</p>
          <p className="text-3xl font-bold text-white mt-1">
            {categories.filter(c => c.isActive).length}
          </p>
        </div>
        <div className="card bg-gradient-to-br from-pink-600/20 to-pink-800/20 border-pink-500/30">
          <p className="text-dark-400 text-sm">Sous-catégories</p>
          <p className="text-3xl font-bold text-white mt-1">
            {categories.reduce((sum, c) => sum + c.subCategoriesCount, 0)}
          </p>
        </div>
        <div className="card bg-gradient-to-br from-blue-600/20 to-blue-800/20 border-blue-500/30">
          <p className="text-dark-400 text-sm">Salons</p>
          <p className="text-3xl font-bold text-white mt-1">
            {categories.reduce((sum, c) => sum + c.roomsCount, 0)}
          </p>
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-dark-700">
                <th className="text-left py-3 px-4 text-dark-400 font-medium text-sm">Ordre</th>
                <th className="text-left py-3 px-4 text-dark-400 font-medium text-sm">Catégorie</th>
                <th className="text-left py-3 px-4 text-dark-400 font-medium text-sm">Description</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Couleurs</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Sous-cat.</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Salons</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Statut</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Visibilité</th>
                <th className="text-right py-3 px-4 text-dark-400 font-medium text-sm">Actions</th>
              </tr>
            </thead>
            <tbody>
              {categories.map((category) => (
                <tr key={category.id} className="border-b border-dark-700/50 hover:bg-dark-800/50 transition-colors">
                  <td className="py-3 px-4">
                    <span className="text-white font-bold">#{category.order}</span>
                  </td>
                  <td className="py-3 px-4">
                    <div className="flex items-center gap-3">
                      <div
                        className="w-10 h-10 rounded-lg flex items-center justify-center text-xs font-bold"
                        style={{ backgroundColor: category.color, color: category.textColor }}
                      >
                        {category.icon ? category.icon.substring(0, 2).toUpperCase() : 'CA'}
                      </div>
                      <div>
                        <p className="text-white font-medium">{category.name}</p>
                        <p className="text-dark-500 text-xs">ID: {category.id} • {category.icon || 'N/A'}</p>
                      </div>
                    </div>
                  </td>
                  <td className="py-3 px-4">
                    <p className="text-dark-300 text-sm max-w-[200px] truncate">
                      {category.description || '-'}
                    </p>
                  </td>
                  <td className="py-3 px-4">
                    <div className="flex justify-center gap-1">
                      <div
                        className="w-6 h-6 rounded border border-dark-600"
                        style={{ backgroundColor: category.color }}
                        title={`Fond: ${category.color}`}
                      />
                      <div
                        className="w-6 h-6 rounded border border-dark-600"
                        style={{ backgroundColor: category.textColor }}
                        title={`Texte: ${category.textColor}`}
                      />
                    </div>
                  </td>
                  <td className="py-3 px-4 text-center">
                    <span className="inline-flex items-center justify-center px-2 py-1 text-xs font-medium rounded-full bg-purple-500/20 text-purple-400 border border-purple-500/30">
                      {category.subCategoriesCount}
                    </span>
                  </td>
                  <td className="py-3 px-4 text-center">
                    <span className="inline-flex items-center justify-center px-2 py-1 text-xs font-medium rounded-full bg-blue-500/20 text-blue-400 border border-blue-500/30">
                      {category.roomsCount}
                    </span>
                  </td>
                  <td className="py-3 px-4 text-center">
                    <span className={`inline-flex items-center justify-center px-2 py-1 text-xs font-medium rounded-full ${
                      category.isActive
                        ? 'bg-success/20 text-success border border-success/30'
                        : 'bg-dark-600/50 text-dark-400 border border-dark-500/30'
                    }`}>
                      {category.isActive ? 'Actif' : 'Inactif'}
                    </span>
                  </td>
                  <td className="py-3 px-4 text-center">
                    {category.isVisible ? (
                      <EyeIcon className="w-5 h-5 text-success mx-auto" />
                    ) : (
                      <EyeSlashIcon className="w-5 h-5 text-dark-500 mx-auto" />
                    )}
                  </td>
                  <td className="py-3 px-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      <button
                        onClick={() => handleOpenEdit(category)}
                        className="p-2 rounded-lg hover:bg-dark-700 text-palx-400 hover:text-palx-300 transition-colors"
                        title="Modifier"
                      >
                        <PencilIcon className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleOpenDelete(category)}
                        disabled={category.roomsCount > 0 || category.subCategoriesCount > 0}
                        className="p-2 rounded-lg hover:bg-dark-700 text-danger hover:text-red-400 transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                        title={category.roomsCount > 0 || category.subCategoriesCount > 0 ? 'Impossible de supprimer' : 'Supprimer'}
                      >
                        <TrashIcon className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {categories.length === 0 && (
                <tr>
                  <td colSpan={9} className="py-8 text-center text-dark-400">
                    Aucune catégorie trouvée
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Create/Edit Dialog */}
      {dialogOpen && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
          <div className="bg-dark-900 rounded-xl border border-dark-700 w-full max-w-lg max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between p-4 border-b border-dark-700">
              <h2 className="text-xl font-bold text-white">
                {dialogMode === 'create' ? 'Nouvelle Catégorie' : 'Modifier la Catégorie'}
              </h2>
              <button onClick={handleCloseDialog} className="text-dark-400 hover:text-white">
                <XMarkIcon className="w-6 h-6" />
              </button>
            </div>
            <div className="p-4 space-y-4">
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Nom *</label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="input w-full"
                  placeholder="Nom de la catégorie"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Description</label>
                <textarea
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  className="input w-full"
                  rows={2}
                  placeholder="Description de la catégorie"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Icône</label>
                <input
                  type="text"
                  value={formData.icon}
                  onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
                  className="input w-full"
                  placeholder="folder, chat, music, etc."
                />
                <p className="text-xs text-dark-500 mt-1">Ex: folder, chat, music, gamepad</p>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-dark-300 mb-1">Couleur de fond</label>
                  <div className="flex items-center gap-2">
                    <input
                      type="color"
                      value={formData.color}
                      onChange={(e) => setFormData({ ...formData, color: e.target.value })}
                      className="w-10 h-10 rounded cursor-pointer border-0"
                    />
                    <input
                      type="text"
                      value={formData.color}
                      onChange={(e) => setFormData({ ...formData, color: e.target.value })}
                      className="input flex-1"
                    />
                  </div>
                </div>
                <div>
                  <label className="block text-sm font-medium text-dark-300 mb-1">Couleur du texte</label>
                  <div className="flex items-center gap-2">
                    <input
                      type="color"
                      value={formData.textColor}
                      onChange={(e) => setFormData({ ...formData, textColor: e.target.value })}
                      className="w-10 h-10 rounded cursor-pointer border-0"
                    />
                    <input
                      type="text"
                      value={formData.textColor}
                      onChange={(e) => setFormData({ ...formData, textColor: e.target.value })}
                      className="input flex-1"
                    />
                  </div>
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Ordre d'affichage</label>
                <input
                  type="number"
                  value={formData.order}
                  onChange={(e) => setFormData({ ...formData, order: parseInt(e.target.value) || 0 })}
                  className="input w-full"
                />
              </div>
              <div className="flex items-center gap-6">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.isVisible}
                    onChange={(e) => setFormData({ ...formData, isVisible: e.target.checked })}
                    className="w-4 h-4 rounded border-dark-600 text-palx-500 focus:ring-palx-500"
                  />
                  <span className="text-dark-300">Visible</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.isActive}
                    onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                    className="w-4 h-4 rounded border-dark-600 text-palx-500 focus:ring-palx-500"
                  />
                  <span className="text-dark-300">Actif</span>
                </label>
              </div>
              {/* Preview */}
              <div className="bg-dark-800 rounded-lg p-4">
                <p className="text-xs text-dark-500 mb-2">Aperçu :</p>
                <div className="flex items-center gap-3">
                  <div
                    className="w-12 h-12 rounded-xl flex items-center justify-center font-bold"
                    style={{ backgroundColor: formData.color, color: formData.textColor }}
                  >
                    {formData.icon ? formData.icon.substring(0, 2).toUpperCase() : 'CA'}
                  </div>
                  <span className="text-white font-medium">
                    {formData.name || 'Nom de la catégorie'}
                  </span>
                </div>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 p-4 border-t border-dark-700">
              <button
                onClick={handleCloseDialog}
                disabled={submitting}
                className="btn-secondary"
              >
                Annuler
              </button>
              <button
                onClick={handleSubmit}
                disabled={submitting || !formData.name.trim()}
                className="btn-primary"
              >
                {submitting ? (
                  <span className="flex items-center gap-2">
                    <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                    Enregistrement...
                  </span>
                ) : (
                  dialogMode === 'create' ? 'Créer' : 'Enregistrer'
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Dialog */}
      {deleteDialogOpen && deletingCategory && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
          <div className="bg-dark-900 rounded-xl border border-dark-700 w-full max-w-md">
            <div className="p-4 border-b border-dark-700">
              <h2 className="text-xl font-bold text-white">Confirmer la suppression</h2>
            </div>
            <div className="p-4">
              <p className="text-dark-300">
                Êtes-vous sûr de vouloir supprimer la catégorie <strong className="text-white">"{deletingCategory.name}"</strong> ?
              </p>
              <p className="text-dark-500 text-sm mt-2">
                Cette action est irréversible.
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 p-4 border-t border-dark-700">
              <button
                onClick={handleCloseDelete}
                disabled={submitting}
                className="btn-secondary"
              >
                Annuler
              </button>
              <button
                onClick={handleDelete}
                disabled={submitting}
                className="bg-danger hover:bg-red-600 text-white px-4 py-2 rounded-lg font-medium transition-colors"
              >
                {submitting ? 'Suppression...' : 'Supprimer'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default CategoriesPage;
