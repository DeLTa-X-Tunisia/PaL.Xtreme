import { useState, useEffect } from 'react';
import { 
  FlagIcon, 
  CheckCircleIcon, 
  XCircleIcon,
  ClockIcon,
  EyeIcon,
  UserIcon,
  ChatBubbleLeftIcon,
  ExclamationTriangleIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { Report, ReportStatus, ReportFilters } from '../types';
import toast from 'react-hot-toast';

const ReportsPage: React.FC = () => {
  const [reports, setReports] = useState<Report[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedReport, setSelectedReport] = useState<Report | null>(null);
  const [showResolveModal, setShowResolveModal] = useState(false);
  const [resolution, setResolution] = useState('');
  const [selectedTab, setSelectedTab] = useState<ReportStatus | 'all'>('Pending');

  useEffect(() => {
    fetchReports();
  }, [selectedTab]);

  const fetchReports = async () => {
    setLoading(true);
    try {
      const filters: ReportFilters = selectedTab !== 'all' ? { status: selectedTab } : {};
      const response = await apiService.getReports(1, 50, filters);
      setReports(response.items);
    } catch (error) {
      console.error('Failed to fetch reports:', error);
      // Mock data
      setReports([
        { id: 1, reporterId: 10, reporterUsername: 'User1', reportedUserId: 15, reportedUsername: 'ToxicUser', reason: 'Harcèlement', description: 'Cet utilisateur m\'a envoyé des messages insultants à plusieurs reprises.', status: 'Pending', createdAt: '2025-01-15T10:30:00Z' },
        { id: 2, reporterId: 22, reporterUsername: 'Player_XYZ', reportedUserId: 18, reportedUsername: 'Spammer', reason: 'Spam', description: 'Spam de messages promotionnels dans le chat principal.', status: 'Pending', createdAt: '2025-01-14T16:45:00Z' },
        { id: 3, reporterId: 8, reporterUsername: 'ModHelper', reportedUserId: 30, reportedUsername: 'FakeAccount', reason: 'Compte suspect', description: 'Ce compte semble être un bot ou un faux compte.', status: 'Reviewing', createdAt: '2025-01-13T09:15:00Z', resolvedBy: 2, resolverUsername: 'Moderator1' },
        { id: 4, reporterId: 5, reporterUsername: 'VIP_Member', reportedUserId: 45, reportedUsername: 'Cheater', reason: 'Triche', description: 'Utilisation de logiciel tiers pour tricher.', status: 'Resolved', createdAt: '2025-01-10T14:20:00Z', resolvedAt: '2025-01-11T10:00:00Z', resolvedBy: 1, resolverUsername: 'Admin', resolution: 'Utilisateur banni pour 30 jours' },
        { id: 5, reporterId: 12, reporterUsername: 'Newbie', reportedMessageId: 12345, reason: 'Message inapproprié', description: 'Message contenant du contenu offensant.', status: 'Dismissed', createdAt: '2025-01-09T11:30:00Z', resolvedAt: '2025-01-09T12:00:00Z', resolvedBy: 2, resolverUsername: 'Moderator1', resolution: 'Faux signalement' },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleResolve = async () => {
    if (!selectedReport || !resolution.trim()) return;

    try {
      await apiService.resolveReport(selectedReport.id, resolution);
      toast.success('Signalement résolu');
      setShowResolveModal(false);
      setResolution('');
      setSelectedReport(null);
      fetchReports();
    } catch (error) {
      toast.error('Échec de la résolution');
    }
  };

  const handleDismiss = async (report: Report) => {
    const reason = prompt('Raison du rejet (optionnel):');
    try {
      await apiService.dismissReport(report.id, reason || undefined);
      toast.success('Signalement rejeté');
      fetchReports();
    } catch (error) {
      toast.error('Échec du rejet');
    }
  };

  const handleBanReportedUser = async (report: Report) => {
    if (!report.reportedUserId) return;
    const reason = prompt('Raison du bannissement:');
    if (!reason) return;

    try {
      await apiService.banUser(report.reportedUserId, reason);
      await apiService.resolveReport(report.id, `Utilisateur banni: ${reason}`);
      toast.success('Utilisateur banni et signalement résolu');
      fetchReports();
    } catch (error) {
      toast.error('Échec de l\'action');
    }
  };

  const getStatusConfig = (status: ReportStatus) => {
    switch (status) {
      case 'Pending':
        return { icon: ClockIcon, color: 'text-warning', bg: 'bg-warning/10', label: 'En attente' };
      case 'Reviewing':
        return { icon: EyeIcon, color: 'text-info', bg: 'bg-info/10', label: 'En cours' };
      case 'Resolved':
        return { icon: CheckCircleIcon, color: 'text-success', bg: 'bg-success/10', label: 'Résolu' };
      case 'Dismissed':
        return { icon: XCircleIcon, color: 'text-dark-400', bg: 'bg-dark-600/50', label: 'Rejeté' };
    }
  };

  const tabs = [
    { id: 'Pending', label: 'En attente', count: reports.filter(r => r.status === 'Pending').length },
    { id: 'Reviewing', label: 'En cours', count: reports.filter(r => r.status === 'Reviewing').length },
    { id: 'Resolved', label: 'Résolus', count: reports.filter(r => r.status === 'Resolved').length },
    { id: 'Dismissed', label: 'Rejetés', count: reports.filter(r => r.status === 'Dismissed').length },
    { id: 'all', label: 'Tous', count: reports.length },
  ];

  const filteredReports = selectedTab === 'all' 
    ? reports 
    : reports.filter(r => r.status === selectedTab);

  return (
    <div className="space-y-6 animate-fade-in">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white">Signalements</h1>
        <p className="text-dark-400 text-sm mt-1">Gérez les signalements des utilisateurs</p>
      </div>

      {/* Tabs */}
      <div className="flex gap-2 overflow-x-auto pb-2">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setSelectedTab(tab.id as ReportStatus | 'all')}
            className={`px-4 py-2 rounded-lg font-medium text-sm whitespace-nowrap transition-all ${
              selectedTab === tab.id
                ? 'bg-palx-600 text-white'
                : 'bg-dark-800 text-dark-300 hover:bg-dark-700'
            }`}
          >
            {tab.label}
            {tab.count > 0 && (
              <span className={`ml-2 px-2 py-0.5 rounded-full text-xs ${
                selectedTab === tab.id ? 'bg-white/20' : 'bg-dark-600'
              }`}>
                {tab.count}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Reports List */}
      <div className="space-y-4">
        {loading ? (
          <div className="flex justify-center py-12">
            <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
          </div>
        ) : filteredReports.length === 0 ? (
          <div className="card text-center py-12">
            <FlagIcon className="w-16 h-16 text-dark-600 mx-auto mb-4" />
            <p className="text-dark-400">Aucun signalement dans cette catégorie</p>
          </div>
        ) : (
          filteredReports.map((report) => {
            const statusConfig = getStatusConfig(report.status);
            const StatusIcon = statusConfig.icon;

            return (
              <div key={report.id} className="card-hover">
                <div className="flex items-start gap-4">
                  {/* Status Icon */}
                  <div className={`p-3 rounded-xl ${statusConfig.bg}`}>
                    <StatusIcon className={`w-6 h-6 ${statusConfig.color}`} />
                  </div>

                  {/* Content */}
                  <div className="flex-1 min-w-0">
                    {/* Header */}
                    <div className="flex items-start justify-between gap-4 mb-2">
                      <div>
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="text-lg font-semibold text-white">{report.reason}</span>
                          <span className={`badge ${statusConfig.bg} ${statusConfig.color}`}>
                            {statusConfig.label}
                          </span>
                        </div>
                        <p className="text-dark-400 text-sm mt-1">
                          Signalé par <span className="text-white">{report.reporterDisplayName || report.reporterUsername}</span>
                          {report.reportedUsername && (
                            <> • Contre <span className="text-danger">{report.reportedDisplayName || report.reportedUsername}</span></>
                          )}
                        </p>
                      </div>
                      <span className="text-dark-400 text-sm whitespace-nowrap">
                        {new Date(report.createdAt).toLocaleString('fr-FR')}
                      </span>
                    </div>

                    {/* Description */}
                    {report.description && (
                      <p className="text-dark-300 text-sm mb-4 p-3 bg-dark-700/30 rounded-lg">
                        "{report.description}"
                      </p>
                    )}

                    {/* Resolution info */}
                    {report.resolution && (
                      <div className="mb-4 p-3 bg-dark-700/30 rounded-lg border-l-4 border-palx-500">
                        <p className="text-dark-400 text-xs mb-1">Résolution par {report.resolverUsername}</p>
                        <p className="text-white text-sm">{report.resolution}</p>
                      </div>
                    )}

                    {/* Actions */}
                    {report.status === 'Pending' || report.status === 'Reviewing' ? (
                      <div className="flex flex-wrap gap-2">
                        <button
                          onClick={() => {
                            setSelectedReport(report);
                            setShowResolveModal(true);
                          }}
                          className="btn-success py-2 text-sm"
                        >
                          <CheckCircleIcon className="w-4 h-4" />
                          Résoudre
                        </button>
                        {report.reportedUserId && (
                          <button
                            onClick={() => handleBanReportedUser(report)}
                            className="btn-danger py-2 text-sm"
                          >
                            <ExclamationTriangleIcon className="w-4 h-4" />
                            Bannir l'utilisateur
                          </button>
                        )}
                        <button
                          onClick={() => handleDismiss(report)}
                          className="btn-ghost py-2 text-sm"
                        >
                          <XCircleIcon className="w-4 h-4" />
                          Rejeter
                        </button>
                      </div>
                    ) : null}
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Resolve Modal */}
      {showResolveModal && selectedReport && (
        <div className="modal-overlay" onClick={() => setShowResolveModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="text-lg font-semibold text-white">Résoudre le signalement</h3>
              <button
                onClick={() => setShowResolveModal(false)}
                className="p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
              >
                <XCircleIcon className="w-5 h-5" />
              </button>
            </div>
            <div className="modal-body">
              <div className="mb-4 p-3 bg-dark-700/30 rounded-lg">
                <p className="text-dark-400 text-sm">Signalement #{selectedReport.id}</p>
                <p className="text-white">{selectedReport.reason}</p>
              </div>
              <div>
                <label className="label">Résolution *</label>
                <textarea
                  value={resolution}
                  onChange={(e) => setResolution(e.target.value)}
                  className="input min-h-32"
                  placeholder="Décrivez les actions prises pour résoudre ce signalement..."
                />
              </div>
            </div>
            <div className="modal-footer">
              <button onClick={() => setShowResolveModal(false)} className="btn-secondary">
                Annuler
              </button>
              <button
                onClick={handleResolve}
                disabled={!resolution.trim()}
                className="btn-primary"
              >
                <CheckCircleIcon className="w-5 h-5" />
                Marquer comme résolu
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ReportsPage;
