import { useState, useEffect } from 'react';
import {
  TagIcon,
  PlusIcon,
  PencilIcon,
  TrashIcon,
  EyeIcon,
  EyeSlashIcon,
  XMarkIcon,
  FunnelIcon,
} from '@heroicons/react/24/outline';
import apiService from '../services/api';

interface Category {
  id: number;
  name: string;
  color: string;
}

interface SubCategory {
  id: number;
  categoryId: number;
  categoryName: string;
  name: string;
  description: string | null;
  icon: string | null;
  color: string;
  textColor: string;
  displayOrder: number;
  isVisible: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  roomsCount: number;
}

interface SubCategoryFormData {
  categoryId: number;
  name: string;
  description: string;
  icon: string;
  color: string;
  textColor: string;
  displayOrder: number;
  isVisible: boolean;
  isActive: boolean;
}

const defaultFormData: SubCategoryFormData = {
  categoryId: 0,
  name: '',
  description: '',
  icon: 'chat',
  color: '#6C757D',
  textColor: '#FFFFFF',
  displayOrder: 0,
  isVisible: true,
  isActive: true,
};

const SubCategoriesPage: React.FC = () => {
  const [subCategories, setSubCategories] = useState<SubCategory[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [filterCategoryId, setFilterCategoryId] = useState<number | null>(null);

  // Dialog state
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState<SubCategoryFormData>(defaultFormData);
  const [submitting, setSubmitting] = useState(false);

  // Delete confirmation
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deletingSubCategory, setDeletingSubCategory] = useState<SubCategory | null>(null);

  const fetchCategories = async () => {
    try {
      const response = await apiService.getCategories();
      setCategories(response);
    } catch (err: any) {
      console.error('Erreur lors du chargement des catégories:', err);
    }
  };

  const fetchSubCategories = async () => {
    try {
      setLoading(true);
      const response = await apiService.getSubCategories(filterCategoryId || undefined);
      setSubCategories(response);
      setError(null);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors du chargement des sous-catégories');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategories();
  }, []);

  useEffect(() => {
    fetchSubCategories();
  }, [filterCategoryId]);

  const handleOpenCreate = () => {
    setFormData({
      ...defaultFormData,
      categoryId: filterCategoryId || (categories[0]?.id || 0),
    });
    setDialogMode('create');
    setEditingId(null);
    setDialogOpen(true);
  };

  const handleOpenEdit = (subCategory: SubCategory) => {
    setFormData({
      categoryId: subCategory.categoryId,
      name: subCategory.name,
      description: subCategory.description || '',
      icon: subCategory.icon || 'chat',
      color: subCategory.color,
      textColor: subCategory.textColor,
      displayOrder: subCategory.displayOrder,
      isVisible: subCategory.isVisible,
      isActive: subCategory.isActive,
    });
    setDialogMode('edit');
    setEditingId(subCategory.id);
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
    if (!formData.categoryId) {
      setError('La catégorie parente est requise');
      return;
    }

    try {
      setSubmitting(true);
      if (dialogMode === 'create') {
        await apiService.createSubCategory(formData);
        setSuccess('Sous-catégorie créée avec succès');
      } else {
        await apiService.updateSubCategory(editingId!, formData);
        setSuccess('Sous-catégorie modifiée avec succès');
      }
      handleCloseDialog();
      fetchSubCategories();
      fetchCategories();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de la sauvegarde');
    } finally {
      setSubmitting(false);
    }
  };

  const handleOpenDelete = (subCategory: SubCategory) => {
    setDeletingSubCategory(subCategory);
    setDeleteDialogOpen(true);
  };

  const handleCloseDelete = () => {
    setDeleteDialogOpen(false);
    setDeletingSubCategory(null);
  };

  const handleDelete = async () => {
    if (!deletingSubCategory) return;

    try {
      setSubmitting(true);
      await apiService.deleteSubCategory(deletingSubCategory.id);
      setSuccess('Sous-catégorie supprimée avec succès');
      handleCloseDelete();
      fetchSubCategories();
      fetchCategories();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Erreur lors de la suppression');
    } finally {
      setSubmitting(false);
    }
  };

  const getCategoryById = (id: number) => categories.find(c => c.id === id);

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

  if (loading && categories.length === 0) {
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
            <TagIcon className="w-8 h-8 text-purple-400" />
            Sous-Catégories
          </h1>
          <p className="text-dark-400 mt-1">
            Gérez les sous-catégories de salons
          </p>
        </div>
        <button
          onClick={handleOpenCreate}
          disabled={categories.length === 0}
          className="btn-primary flex items-center gap-2 disabled:opacity-50"
        >
          <PlusIcon className="w-5 h-5" />
          Nouvelle Sous-Catégorie
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

      {/* Filter */}
      <div className="card flex items-center gap-4">
        <FunnelIcon className="w-5 h-5 text-dark-400" />
        <select
          value={filterCategoryId || ''}
          onChange={(e) => setFilterCategoryId(e.target.value ? parseInt(e.target.value) : null)}
          className="input min-w-[250px]"
        >
          <option value="">Toutes les catégories</option>
          {categories.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.name}
            </option>
          ))}
        </select>
        <span className="text-dark-400 text-sm">
          {subCategories.length} sous-catégorie{subCategories.length > 1 ? 's' : ''} trouvée{subCategories.length > 1 ? 's' : ''}
        </span>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="card bg-gradient-to-br from-purple-600/20 to-purple-800/20 border-purple-500/30">
          <p className="text-dark-400 text-sm">Sous-catégories {filterCategoryId ? 'filtrées' : 'totales'}</p>
          <p className="text-3xl font-bold text-white mt-1">{subCategories.length}</p>
        </div>
        <div className="card bg-gradient-to-br from-green-600/20 to-green-800/20 border-green-500/30">
          <p className="text-dark-400 text-sm">Actives</p>
          <p className="text-3xl font-bold text-white mt-1">
            {subCategories.filter(sc => sc.isActive).length}
          </p>
        </div>
        <div className="card bg-gradient-to-br from-blue-600/20 to-blue-800/20 border-blue-500/30">
          <p className="text-dark-400 text-sm">Salons</p>
          <p className="text-3xl font-bold text-white mt-1">
            {subCategories.reduce((sum, sc) => sum + sc.roomsCount, 0)}
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
                <th className="text-left py-3 px-4 text-dark-400 font-medium text-sm">Sous-Catégorie</th>
                <th className="text-left py-3 px-4 text-dark-400 font-medium text-sm">Catégorie</th>
                <th className="text-left py-3 px-4 text-dark-400 font-medium text-sm">Description</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Couleurs</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Salons</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Statut</th>
                <th className="text-center py-3 px-4 text-dark-400 font-medium text-sm">Visibilité</th>
                <th className="text-right py-3 px-4 text-dark-400 font-medium text-sm">Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={9} className="py-8 text-center">
                    <div className="w-8 h-8 border-4 border-palx-500 border-t-transparent rounded-full animate-spin mx-auto"></div>
                  </td>
                </tr>
              ) : (
                <>
                  {subCategories.map((subCategory) => {
                    const parentCategory = getCategoryById(subCategory.categoryId);
                    return (
                      <tr key={subCategory.id} className="border-b border-dark-700/50 hover:bg-dark-800/50 transition-colors">
                        <td className="py-3 px-4">
                          <span className="text-white font-bold">#{subCategory.displayOrder}</span>
                        </td>
                        <td className="py-3 px-4">
                          <div className="flex items-center gap-3">
                            <div
                              className="w-9 h-9 rounded-lg flex items-center justify-center text-xs font-bold"
                              style={{ backgroundColor: subCategory.color, color: subCategory.textColor }}
                            >
                              {subCategory.icon ? subCategory.icon.substring(0, 2).toUpperCase() : 'SC'}
                            </div>
                            <div>
                              <p className="text-white font-medium">{subCategory.name}</p>
                              <p className="text-dark-500 text-xs">ID: {subCategory.id} • {subCategory.icon || 'N/A'}</p>
                            </div>
                          </div>
                        </td>
                        <td className="py-3 px-4">
                          <span
                            className="inline-flex items-center px-2 py-1 text-xs font-medium rounded-full text-white"
                            style={{ backgroundColor: parentCategory?.color || '#6C757D' }}
                          >
                            {subCategory.categoryName}
                          </span>
                        </td>
                        <td className="py-3 px-4">
                          <p className="text-dark-300 text-sm max-w-[180px] truncate">
                            {subCategory.description || '-'}
                          </p>
                        </td>
                        <td className="py-3 px-4">
                          <div className="flex justify-center gap-1">
                            <div
                              className="w-5 h-5 rounded border border-dark-600"
                              style={{ backgroundColor: subCategory.color }}
                              title={`Fond: ${subCategory.color}`}
                            />
                            <div
                              className="w-5 h-5 rounded border border-dark-600"
                              style={{ backgroundColor: subCategory.textColor }}
                              title={`Texte: ${subCategory.textColor}`}
                            />
                          </div>
                        </td>
                        <td className="py-3 px-4 text-center">
                          <span className="inline-flex items-center justify-center px-2 py-1 text-xs font-medium rounded-full bg-blue-500/20 text-blue-400 border border-blue-500/30">
                            {subCategory.roomsCount}
                          </span>
                        </td>
                        <td className="py-3 px-4 text-center">
                          <span className={`inline-flex items-center justify-center px-2 py-1 text-xs font-medium rounded-full ${
                            subCategory.isActive
                              ? 'bg-success/20 text-success border border-success/30'
                              : 'bg-dark-600/50 text-dark-400 border border-dark-500/30'
                          }`}>
                            {subCategory.isActive ? 'Actif' : 'Inactif'}
                          </span>
                        </td>
                        <td className="py-3 px-4 text-center">
                          {subCategory.isVisible ? (
                            <EyeIcon className="w-5 h-5 text-success mx-auto" />
                          ) : (
                            <EyeSlashIcon className="w-5 h-5 text-dark-500 mx-auto" />
                          )}
                        </td>
                        <td className="py-3 px-4 text-right">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              onClick={() => handleOpenEdit(subCategory)}
                              className="p-2 rounded-lg hover:bg-dark-700 text-palx-400 hover:text-palx-300 transition-colors"
                              title="Modifier"
                            >
                              <PencilIcon className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleOpenDelete(subCategory)}
                              disabled={subCategory.roomsCount > 0}
                              className="p-2 rounded-lg hover:bg-dark-700 text-danger hover:text-red-400 transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                              title={subCategory.roomsCount > 0 ? 'Des salons utilisent cette sous-catégorie' : 'Supprimer'}
                            >
                              <TrashIcon className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                  {subCategories.length === 0 && (
                    <tr>
                      <td colSpan={9} className="py-8 text-center text-dark-400">
                        {filterCategoryId
                          ? 'Aucune sous-catégorie dans cette catégorie'
                          : 'Aucune sous-catégorie trouvée'}
                      </td>
                    </tr>
                  )}
                </>
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
                {dialogMode === 'create' ? 'Nouvelle Sous-Catégorie' : 'Modifier la Sous-Catégorie'}
              </h2>
              <button onClick={handleCloseDialog} className="text-dark-400 hover:text-white">
                <XMarkIcon className="w-6 h-6" />
              </button>
            </div>
            <div className="p-4 space-y-4">
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Catégorie parente *</label>
                <select
                  value={formData.categoryId || ''}
                  onChange={(e) => setFormData({ ...formData, categoryId: parseInt(e.target.value) })}
                  className="input w-full"
                >
                  <option value="">Sélectionner une catégorie</option>
                  {categories.map((cat) => (
                    <option key={cat.id} value={cat.id}>
                      {cat.name}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Nom *</label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="input w-full"
                  placeholder="Nom de la sous-catégorie"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Description</label>
                <textarea
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                  className="input w-full"
                  rows={2}
                  placeholder="Description de la sous-catégorie"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-dark-300 mb-1">Icône</label>
                <input
                  type="text"
                  value={formData.icon}
                  onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
                  className="input w-full"
                  placeholder="chat, music, gamepad, etc."
                />
                <p className="text-xs text-dark-500 mt-1">Ex: chat, music, gamepad, film</p>
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
                  value={formData.displayOrder}
                  onChange={(e) => setFormData({ ...formData, displayOrder: parseInt(e.target.value) || 0 })}
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
                    className="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-sm"
                    style={{ backgroundColor: formData.color, color: formData.textColor }}
                  >
                    {formData.icon ? formData.icon.substring(0, 2).toUpperCase() : 'SC'}
                  </div>
                  <div>
                    <span className="text-white font-medium">
                      {formData.name || 'Nom de la sous-catégorie'}
                    </span>
                    <p className="text-dark-500 text-xs">
                      {getCategoryById(formData.categoryId)?.name || 'Catégorie parente'}
                    </p>
                  </div>
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
                disabled={submitting || !formData.name.trim() || !formData.categoryId}
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
      {deleteDialogOpen && deletingSubCategory && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
          <div className="bg-dark-900 rounded-xl border border-dark-700 w-full max-w-md">
            <div className="p-4 border-b border-dark-700">
              <h2 className="text-xl font-bold text-white">Confirmer la suppression</h2>
            </div>
            <div className="p-4">
              <p className="text-dark-300">
                Êtes-vous sûr de vouloir supprimer la sous-catégorie <strong className="text-white">"{deletingSubCategory.name}"</strong> ?
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

export default SubCategoriesPage;
