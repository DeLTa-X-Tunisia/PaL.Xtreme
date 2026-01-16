import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { 
  Chart as ChartJS, 
  CategoryScale, 
  LinearScale, 
  PointElement, 
  LineElement, 
  BarElement,
  Title, 
  Tooltip, 
  Legend,
  Filler,
  ArcElement 
} from 'chart.js';
import { Line, Doughnut } from 'react-chartjs-2';
import { 
  UsersIcon, 
  ChatBubbleLeftRightIcon, 
  FlagIcon,
  ArrowTrendingUpIcon,
  CurrencyDollarIcon,
  SparklesIcon,
  EyeIcon,
  ClockIcon
} from '@heroicons/react/24/outline';
import apiService from '../services/api';
import { useSignalR } from '../contexts/SignalRContext';
import { DashboardStats } from '../types';

// Register Chart.js components
ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  BarElement,
  Title,
  Tooltip,
  Legend,
  Filler,
  ArcElement
);

// Fonction pour formater le temps relatif
const formatRelativeTime = (dateString: string): string => {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return "À l'instant";
  if (diffMins < 60) return `Il y a ${diffMins} min`;
  if (diffHours < 24) return `Il y a ${diffHours}h`;
  if (diffDays < 7) return `Il y a ${diffDays}j`;
  return date.toLocaleDateString('fr-FR');
};

const DashboardPage: React.FC = () => {
  const { realtimeStats } = useSignalR();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await apiService.getDashboardStats();
        setStats(data);
      } catch (error) {
        console.error('Failed to fetch stats:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();
  }, []);

  // Use realtime stats if available
  const currentStats = realtimeStats || stats;

  // Chart data for user activity (données réelles)
  const userActivityData = {
    labels: currentStats?.weeklyActivity?.map(d => d.day) || ['Lun', 'Mar', 'Mer', 'Jeu', 'Ven', 'Sam', 'Dim'],
    datasets: [
      {
        label: 'Utilisateurs actifs',
        data: currentStats?.weeklyActivity?.map(d => d.activeUsers) || [0, 0, 0, 0, 0, 0, 0],
        fill: true,
        borderColor: '#6366f1',
        backgroundColor: 'rgba(99, 102, 241, 0.1)',
        tension: 0.4,
        pointBackgroundColor: '#6366f1',
        pointBorderColor: '#fff',
        pointBorderWidth: 2,
        pointRadius: 4,
      },
    ],
  };

  // Chart data for subscription distribution (données réelles depuis SubscriptionTiers)
  const subscriptionData = {
    labels: currentStats?.subscriptionDistribution?.map(s => s.name) || ['Member', 'Deluxe', 'Extreme'],
    datasets: [
      {
        data: currentStats?.subscriptionDistribution?.map(s => s.count) || [0, 0, 0],
        backgroundColor: currentStats?.subscriptionDistribution?.map(s => s.color) || ['#808080', '#9B59B6', '#E74C3C'],
        borderColor: currentStats?.subscriptionDistribution?.map(s => s.color) || ['#808080', '#9B59B6', '#E74C3C'],
        borderWidth: 2,
      },
    ],
  };

  const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false,
      },
    },
    scales: {
      x: {
        grid: {
          color: 'rgba(71, 85, 105, 0.3)',
        },
        ticks: {
          color: '#94a3b8',
        },
      },
      y: {
        grid: {
          color: 'rgba(71, 85, 105, 0.3)',
        },
        ticks: {
          color: '#94a3b8',
        },
      },
    },
  };

  const doughnutOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom' as const,
        labels: {
          color: '#94a3b8',
          padding: 20,
          font: {
            size: 12,
          },
        },
      },
    },
    cutout: '65%',
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-12 h-12 border-4 border-palx-500 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Stats Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 lg:gap-6">
        {/* Total Users */}
        <div className="stat-card">
          <div className="stat-icon bg-palx-500/20">
            <UsersIcon className="w-7 h-7 text-palx-400" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Total Utilisateurs</p>
            <p className="text-2xl font-bold text-white">{currentStats?.totalUsers.toLocaleString()}</p>
            <p className="text-success text-xs flex items-center gap-1 mt-1">
              <ArrowTrendingUpIcon className="w-4 h-4" />
              +{currentStats?.newUsersToday} aujourd'hui
            </p>
          </div>
        </div>

        {/* Online Users */}
        <div className="stat-card">
          <div className="stat-icon bg-success/20">
            <EyeIcon className="w-7 h-7 text-success" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">En ligne</p>
            <p className="text-2xl font-bold text-white">{currentStats?.onlineUsers}</p>
            <p className="text-dark-400 text-xs mt-1">
              {((currentStats?.onlineUsers || 0) / (currentStats?.totalUsers || 1) * 100).toFixed(1)}% du total
            </p>
          </div>
        </div>

        {/* Active Rooms */}
        <div className="stat-card">
          <div className="stat-icon bg-info/20">
            <ChatBubbleLeftRightIcon className="w-7 h-7 text-info" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Salons actifs</p>
            <p className="text-2xl font-bold text-white">{currentStats?.activeRooms}</p>
            <p className="text-dark-400 text-xs mt-1">
              {currentStats?.totalMessages.toLocaleString()} messages
            </p>
          </div>
        </div>

        {/* Pending Reports */}
        <Link to="/reports" className="stat-card group">
          <div className="stat-icon bg-warning/20">
            <FlagIcon className="w-7 h-7 text-warning" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Signalements</p>
            <p className="text-2xl font-bold text-white">{currentStats?.pendingReports}</p>
            <p className="text-warning text-xs mt-1 group-hover:underline">
              En attente de traitement
            </p>
          </div>
        </Link>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* User Activity Chart */}
        <div className="lg:col-span-2 card">
          <div className="flex items-center justify-between mb-6">
            <h3 className="text-lg font-semibold text-white">Activité des utilisateurs</h3>
            <select className="bg-dark-700 border border-dark-600 rounded-lg px-3 py-1.5 text-sm text-dark-200">
              <option>Cette semaine</option>
              <option>Ce mois</option>
              <option>Cette année</option>
            </select>
          </div>
          <div className="h-72">
            <Line data={userActivityData} options={chartOptions} />
          </div>
        </div>

        {/* Subscription Distribution */}
        <div className="card">
          <h3 className="text-lg font-semibold text-white mb-6">Répartition abonnements</h3>
          <div className="h-64">
            <Doughnut data={subscriptionData} options={doughnutOptions} />
          </div>
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 lg:gap-6">
        {/* Premium Users */}
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-palx-500/20 flex items-center justify-center">
            <SparklesIcon className="w-6 h-6 text-palx-400" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Utilisateurs Premium</p>
            <p className="text-xl font-bold text-white">{currentStats?.premiumUsers}</p>
          </div>
        </div>

        {/* VIP Users */}
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-warning/20 flex items-center justify-center">
            <CurrencyDollarIcon className="w-6 h-6 text-warning" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Utilisateurs VIP</p>
            <p className="text-xl font-bold text-white">{currentStats?.vipUsers}</p>
          </div>
        </div>

        {/* Server Uptime */}
        <div className="card flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-success/20 flex items-center justify-center">
            <ClockIcon className="w-6 h-6 text-success" />
          </div>
          <div>
            <p className="text-dark-400 text-sm">Uptime serveur</p>
            <p className="text-xl font-bold text-white">{currentStats?.serverUptime || 99.9}%</p>
          </div>
        </div>
      </div>

      {/* Recent Activity */}
      <div className="card">
        <div className="flex items-center justify-between mb-6">
          <h3 className="text-lg font-semibold text-white">Activité récente</h3>
          <Link to="/logs" className="text-palx-400 hover:text-palx-300 text-sm">
            Voir tout →
          </Link>
        </div>
        <div className="space-y-3">
          {currentStats?.recentActivities && currentStats.recentActivities.length > 0 ? (
            currentStats.recentActivities.map((activity, index) => (
              <div
                key={index}
                className="flex items-center justify-between py-3 border-b border-dark-700/50 last:border-0"
              >
                <div className="flex items-center gap-3">
                  <div className={`w-2 h-2 rounded-full ${
                    activity.type === 'user_registered' ? 'bg-success' :
                    activity.type === 'report_created' ? 'bg-warning' :
                    activity.type === 'user_banned' ? 'bg-danger' :
                    activity.type === 'room_created' ? 'bg-info' :
                    'bg-palx-500'
                  }`} />
                  <div>
                    <p className="text-white text-sm">{activity.title}</p>
                    <p className="text-dark-400 text-xs">{activity.description}</p>
                  </div>
                </div>
                <span className="text-dark-400 text-xs">{formatRelativeTime(activity.createdAt)}</span>
              </div>
            ))
          ) : (
            <p className="text-dark-400 text-sm text-center py-4">Aucune activité récente</p>
          )}
        </div>
      </div>
    </div>
  );
};

export default DashboardPage;
