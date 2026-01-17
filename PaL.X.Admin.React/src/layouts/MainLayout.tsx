import { Outlet, NavLink, useLocation } from 'react-router-dom';
import { useState, useEffect } from 'react';
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
  CreditCardIcon,
  BuildingOffice2Icon,
  ChevronDownIcon,
  HomeModernIcon,
} from '@heroicons/react/24/outline';

// Types pour la navigation
interface NavItem {
  name: string;
  href: string;
  icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
}

interface NavGroup {
  name: string;
  icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
  children: NavItem[];
}

type NavigationItem = NavItem | NavGroup;

// Vérifier si c'est un groupe
const isNavGroup = (item: NavigationItem): item is NavGroup => {
  return 'children' in item;
};

// Navigation structurée avec sous-menus
const navigation: NavigationItem[] = [
  { name: 'Dashboard', href: '/dashboard', icon: HomeIcon },
  {
    name: 'Utilisateurs',
    icon: UsersIcon,
    children: [
      { name: 'Gestion', href: '/users', icon: UsersIcon },
      { name: 'Abonnements', href: '/subscriptions', icon: CreditCardIcon },
      { name: 'Badges', href: '/badges', icon: SparklesIcon },
    ],
  },
  {
    name: 'Rôles',
    icon: ShieldCheckIcon,
    children: [
      { name: 'Rôles Serveur', href: '/roles', icon: ShieldCheckIcon },
      { name: 'Rôles Salons', href: '/room-roles', icon: HomeModernIcon },
    ],
  },
  { name: 'Diffusion', href: '/broadcast', icon: MegaphoneIcon },
  {
    name: 'Salons',
    icon: ChatBubbleLeftRightIcon,
    children: [
      { name: 'Gestion', href: '/rooms', icon: ChatBubbleLeftRightIcon },
      { name: 'Abonnements', href: '/room-subscriptions', icon: BuildingOffice2Icon },
    ],
  },
  {
    name: 'Catégories',
    icon: FolderIcon,
    children: [
      { name: 'Catégories', href: '/categories', icon: FolderIcon },
      { name: 'Sous-catégories', href: '/subcategories', icon: TagIcon },
    ],
  },
  { name: 'Signalements', href: '/reports', icon: FlagIcon },
  { name: 'Logs', href: '/logs', icon: DocumentTextIcon },
  { name: 'Paramètres', href: '/settings', icon: Cog6ToothIcon },
];

// Composant pour les sous-menus
const NavGroupItem: React.FC<{
  group: NavGroup;
  isOpen: boolean;
  onToggle: () => void;
  onLinkClick: () => void;
  location: ReturnType<typeof useLocation>;
}> = ({ group, isOpen, onToggle, onLinkClick, location }) => {
  // Vérifier si un enfant est actif
  const isChildActive = group.children.some(child => location.pathname.startsWith(child.href));
  
  return (
    <div className="space-y-1">
      <button
        onClick={onToggle}
        className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200 ${
          isChildActive
            ? 'bg-palx-600/20 text-palx-400'
            : 'text-dark-300 hover:text-white hover:bg-dark-700/50'
        }`}
      >
        <div className="flex items-center gap-3">
          <group.icon className="w-5 h-5" />
          <span>{group.name}</span>
        </div>
        <ChevronDownIcon
          className={`w-4 h-4 transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`}
        />
      </button>
      
      {/* Sous-menu avec animation */}
      <div
        className={`overflow-hidden transition-all duration-200 ${
          isOpen ? 'max-h-48 opacity-100' : 'max-h-0 opacity-0'
        }`}
      >
        <div className="pl-4 space-y-1 pt-1">
          {group.children.map((child) => (
            <NavLink
              key={child.href}
              to={child.href}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-all duration-200 ${
                  isActive
                    ? 'bg-palx-600/30 text-palx-300 border-l-2 border-palx-500'
                    : 'text-dark-400 hover:text-white hover:bg-dark-700/30 border-l-2 border-transparent'
                }`
              }
              onClick={onLinkClick}
            >
              <child.icon className="w-4 h-4" />
              <span>{child.name}</span>
            </NavLink>
          ))}
        </div>
      </div>
    </div>
  );
};

const MainLayout: React.FC = () => {
  const { user, logout } = useAuth();
  const { isConnected } = useSignalR();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [openGroups, setOpenGroups] = useState<string[]>([]);

  // Ouvrir automatiquement le groupe contenant la page active
  useEffect(() => {
    navigation.forEach((item) => {
      if (isNavGroup(item)) {
        const isChildActive = item.children.some(child => location.pathname.startsWith(child.href));
        if (isChildActive && !openGroups.includes(item.name)) {
          setOpenGroups(prev => [...prev, item.name]);
        }
      }
    });
  }, [location.pathname]);

  const toggleGroup = (groupName: string) => {
    setOpenGroups(prev =>
      prev.includes(groupName)
        ? prev.filter(name => name !== groupName)
        : [...prev, groupName]
    );
  };

  const getPageTitle = () => {
    const path = location.pathname;
    for (const item of navigation) {
      if (isNavGroup(item)) {
        const child = item.children.find(c => path.startsWith(c.href));
        if (child) return `${item.name} - ${child.name}`;
      } else if (path.startsWith(item.href)) {
        return item.name;
      }
    }
    return 'Admin Panel';
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
            <img 
              src="/logo.png" 
              alt="PaL.Xtreme" 
              className="w-10 h-10 object-contain"
            />
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
          {navigation.map((item) =>
            isNavGroup(item) ? (
              <NavGroupItem
                key={item.name}
                group={item}
                isOpen={openGroups.includes(item.name)}
                onToggle={() => toggleGroup(item.name)}
                onLinkClick={() => setSidebarOpen(false)}
                location={location}
              />
            ) : (
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
            )
          )}
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
