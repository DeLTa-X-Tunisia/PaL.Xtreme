import { Outlet, NavLink, useLocation } from 'react-router-dom';
import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useSignalR } from '../contexts/SignalRContext';
import {
  HomeIcon,
  UsersIcon,
  ChatBubbleLeftRightIcon,
  FlagIcon,
  SparklesIcon,
  DocumentTextIcon,
  Cog6ToothIcon,
  ArrowRightOnRectangleIcon,
  Bars3Icon,
  XMarkIcon,
  BellIcon,
  WifiIcon,
  ShieldCheckIcon,
  MegaphoneIcon,
  FolderIcon,
  TagIcon,
} from '@heroicons/react/24/outline';

const navigation = [
  { name: 'Dashboard', href: '/dashboard', icon: HomeIcon },
  { name: 'Utilisateurs', href: '/users', icon: UsersIcon },
  { name: 'Rôles', href: '/roles', icon: ShieldCheckIcon },
  { name: 'Diffusion', href: '/broadcast', icon: MegaphoneIcon },
  { name: 'Catégories', href: '/categories', icon: FolderIcon },
  { name: 'Sous-catégories', href: '/subcategories', icon: TagIcon },
  { name: 'Salons', href: '/rooms', icon: ChatBubbleLeftRightIcon },
  { name: 'Signalements', href: '/reports', icon: FlagIcon },
  { name: 'Badges', href: '/badges', icon: SparklesIcon },
  { name: 'Logs', href: '/logs', icon: DocumentTextIcon },
  { name: 'Paramètres', href: '/settings', icon: Cog6ToothIcon },
];

const MainLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const { isConnected } = useSignalR();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const getPageTitle = () => {
    const path = location.pathname;
    const nav = navigation.find(n => path.startsWith(n.href));
    return nav?.name || 'Admin Panel';
  };

  return (
    <div className="min-h-screen bg-dark-950 flex">
      {/* Mobile sidebar backdrop */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/60 z-40 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={`fixed inset-y-0 left-0 z-50 w-72 bg-dark-900 border-r border-dark-700/50 transform transition-transform duration-300 lg:translate-x-0 lg:static lg:z-auto ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {/* Logo */}
        <div className="flex items-center justify-between h-16 px-6 border-b border-dark-700/50">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-palx-500 to-palx-700 flex items-center justify-center shadow-glow">
              <span className="text-white font-bold text-lg">PX</span>
            </div>
            <div>
              <h1 className="text-white font-semibold text-lg">PaL.Xtreme</h1>
              <p className="text-dark-400 text-xs">Admin Panel</p>
            </div>
          </div>
          <button
            className="lg:hidden text-dark-400 hover:text-white"
            onClick={() => setSidebarOpen(false)}
          >
            <XMarkIcon className="w-6 h-6" />
          </button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 px-4 py-6 space-y-1 overflow-y-auto">
          {navigation.map((item) => (
            <NavLink
              key={item.name}
              to={item.href}
              className={({ isActive }) =>
                isActive ? 'sidebar-link-active' : 'sidebar-link'
              }
              onClick={() => setSidebarOpen(false)}
            >
              <item.icon className="w-5 h-5" />
              <span>{item.name}</span>
            </NavLink>
          ))}
        </nav>

        {/* User Info */}
        <div className="p-4 border-t border-dark-700/50">
          <div className="flex items-center gap-3 p-3 rounded-lg bg-dark-800/50">
            {(user as any)?.avatarPath ? (
              <img 
                src={`http://localhost:5145/${(user as any).avatarPath}`}
                alt={user?.displayName || user?.username}
                className="w-10 h-10 rounded-full object-cover"
                onError={(e) => {
                  e.currentTarget.style.display = 'none';
                  e.currentTarget.nextElementSibling?.classList.remove('hidden');
                }}
              />
            ) : null}
            <div className={`w-10 h-10 rounded-full bg-palx-600 flex items-center justify-center text-white font-semibold ${(user as any)?.avatarPath ? 'hidden' : ''}`}>
              {(user?.displayName || user?.username)?.charAt(0).toUpperCase() || 'A'}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-white font-medium truncate">{user?.displayName || user?.username}</p>
              <p 
                className="text-xs truncate"
                style={{ color: (user as any)?.roleColor || '#9CA3AF' }}
              >
                {(user as any)?.roleDisplayName || (user as any)?.roleName || user?.role}
              </p>
            </div>
            <button
              onClick={logout}
              className="p-2 text-dark-400 hover:text-danger hover:bg-danger/10 rounded-lg transition-colors"
              title="Déconnexion"
            >
              <ArrowRightOnRectangleIcon className="w-5 h-5" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Header */}
        <header className="h-16 bg-dark-900/50 backdrop-blur-sm border-b border-dark-700/50 flex items-center justify-between px-4 lg:px-8 sticky top-0 z-30">
          <div className="flex items-center gap-4">
            <button
              className="lg:hidden p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg"
              onClick={() => setSidebarOpen(true)}
            >
              <Bars3Icon className="w-6 h-6" />
            </button>
            <h2 className="text-xl font-semibold text-white">{getPageTitle()}</h2>
          </div>

          <div className="flex items-center gap-3">
            {/* Connection Status */}
            <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-medium ${
              isConnected 
                ? 'bg-success/10 text-success' 
                : 'bg-danger/10 text-danger'
            }`}>
              <WifiIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{isConnected ? 'Connecté' : 'Déconnecté'}</span>
            </div>

            {/* Notifications */}
            <button className="relative p-2 text-dark-400 hover:text-white hover:bg-dark-700/50 rounded-lg">
              <BellIcon className="w-6 h-6" />
              <span className="absolute top-1 right-1 w-2 h-2 bg-danger rounded-full"></span>
            </button>

            {/* User Avatar (Mobile) */}
            <div className="lg:hidden w-9 h-9 rounded-full bg-palx-600 flex items-center justify-center text-white font-semibold text-sm">
              {(user?.displayName || user?.username)?.charAt(0).toUpperCase() || 'A'}
            </div>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 p-4 lg:p-8 overflow-y-auto">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default MainLayout;
