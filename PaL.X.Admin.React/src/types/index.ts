// ============================================
// Types PaL.Xtreme Admin Panel
// ============================================

// === Utilisateurs ===
export interface User {
  id: number;
  username: string;
  displayName?: string;
  firstName?: string;
  lastName?: string;
  email: string;
  role: UserRole;
  roleLevel?: number;
  roleName?: string; // Nom technique (ServerMaster)
  roleDisplayName?: string; // Nom affiché (Maître du Serveur)
  roleColor?: string; // Couleur du rôle (#FFD700)
  subscriptionType: SubscriptionType;
  subscriptionEndDate: string | null;
  isOnline: boolean;
  isBanned: boolean;
  banReason?: string;
  banExpiresAt?: string;
  createdAt: string;
  lastLoginAt?: string;
  profilePicture?: string;
  avatar?: string;
  avatarPath?: string; // Chemin vers l'avatar
  bio?: string;
  roomsCreated: number;
  messagesCount: number;
  warningsCount: number;
}

export type UserRole = 'User' | 'Moderator' | 'Admin' | 'SuperAdmin';
export type SubscriptionType = 'Free' | 'Premium' | 'VIP';

// === Roles ===
export interface Role {
  id: number;
  roleLevel: number;
  roleName: string; // Nom technique (ServerMaster)
  displayName: string; // Nom affiché (Maître du Serveur)
  icon: string; // Icône (trophy, shield, etc.)
  color: string; // Couleur (#FFD700)
  description: string; // Description du rôle
  userCount: number; // Nombre d'utilisateurs avec ce rôle
}

// === Room Roles (Rôles de Salons) ===
export interface RoomRole {
  id: number;
  roleLevel: number; // 1=Owner, 2=SuperAdmin, 3=Admin, 4=PowerUser, 5=Moderator, 6=Member
  roleName: string; // Nom technique (RoomOwner, RoomAdmin, etc.)
  displayName: string; // Nom affiché (Propriétaire du Salon)
  icon: string; // Icône (crown, shield, user, etc.)
  color: string; // Couleur hex
  description: string; // Description des permissions
  isSystem: boolean; // Rôles système non supprimables
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  permissions: RoomPermission[]; // Liste des permissions détaillées
}

export interface RoomPermission {
  id: number;
  permissionKey: string; // Technical key (manage_settings, kick_users)
  displayName: string; // Nom affiché (Modifier les paramètres)
  description?: string;
  category: string; // general, moderation, media, members, base
  isActive: boolean;
  isEnabled: boolean; // Si la permission est attribuée au rôle
}

export interface PermissionGroup {
  category: string;
  categoryDisplayName: string;
  permissions: RoomPermission[];
}

export interface CreateRoomRoleDto {
  roleLevel: number;
  roleName: string;
  displayName: string;
  description?: string;
  icon: string;
  color: string;
  permissionIds: number[];
}

export interface UpdateRoomRoleDto {
  displayName: string;
  description?: string;
  icon: string;
  color: string;
  isActive: boolean;
  permissionIds: number[];
}

export interface RoomRoleOperationResult {
  success: boolean;
  message?: string;
  role?: RoomRole;
}

// === Rooms ===
export interface Room {
  id: number;
  name: string;
  description?: string;
  ownerId: number;
  ownerUsername: string;
  ownerDisplayName?: string; // Prénom Nom du propriétaire
  createdAt: string;
  isActive: boolean;
  currentUsers: number;
  maxUsers: number;
  isPrivate: boolean;
  hasPassword: boolean;
  category: RoomCategory;
  bannedUsers: number[];
  tags: string[];
  // Subscription fields
  subscriptionType?: string;
  subscriptionEndDate?: string;
}

export type RoomCategory = 'General' | 'Gaming' | 'Music' | 'Art' | 'Tech' | 'Other';

// === Messages ===
export interface Message {
  id: number;
  content: string;
  senderId: number;
  senderUsername: string;
  roomId?: number;
  receiverId?: number;
  createdAt: string;
  isDeleted: boolean;
  isEdited: boolean;
  messageType: MessageType;
}

export type MessageType = 'Text' | 'Image' | 'Audio' | 'System';

// === Reports / Modération ===
export interface Report {
  id: number;
  reporterId: number;
  reporterUsername: string;
  reporterDisplayName?: string; // Nom complet du signaleur
  reportedUserId?: number;
  reportedUsername?: string;
  reportedDisplayName?: string; // Nom complet du signalé
  reportedMessageId?: number;
  reportedRoomId?: number;
  reason: string;
  description?: string;
  status: ReportStatus;
  createdAt: string;
  resolvedAt?: string;
  resolvedBy?: number;
  resolverUsername?: string;
  resolverDisplayName?: string; // Nom complet du résolveur
  resolution?: string;
}

export type ReportStatus = 'Pending' | 'Reviewing' | 'Resolved' | 'Dismissed';

// === Logs ===
export interface AuditLog {
  id: number;
  userId: number;
  username: string;
  displayName?: string; // Nom complet (Admin A)
  action: string;
  targetType?: string;
  targetId?: number;
  details?: string;
  ipAddress?: string;
  createdAt: string;
}

// === Badges ===
export interface Badge {
  id: number;
  name: string;
  description: string;
  iconUrl: string;
  rarity: BadgeRarity;
  createdAt: string;
  usersCount: number;
}

export type BadgeRarity = 'Common' | 'Uncommon' | 'Rare' | 'Epic' | 'Legendary';

// === Statistiques ===
export interface DashboardStats {
  totalUsers: number;
  onlineUsers: number;
  activeRooms: number;
  totalMessages: number;
  newUsersToday: number;
  pendingReports: number;
  premiumUsers: number; // Deluxe à Gold
  vipUsers: number; // Platinum à Legend
  freeUsers: number; // Member (gratuit)
  serverUptime: number;
  weeklyActivity: DailyActivity[];
  recentActivities: RecentActivity[];
  subscriptionDistribution: SubscriptionTierStats[]; // Répartition détaillée
}

export interface SubscriptionTierStats {
  tierId: number;
  name: string;
  color: string;
  count: number;
}

export interface DailyActivity {
  day: string;
  date: string;
  activeUsers: number;
  connections: number;
  messages: number;
}

export interface RecentActivity {
  type: 'user_registered' | 'report_created' | 'user_banned' | 'room_created';
  title: string;
  description: string;
  username?: string;
  displayName?: string;
  createdAt: string;
}

// === Broadcast / Annonces globales ===
export interface BroadcastRequest {
  type: 'info' | 'warning' | 'alert' | 'success';
  title: string;
  message: string;
}

export interface BroadcastHistory {
  id: number;
  sentByUserId: number;
  sentByUsername: string;
  sentByDisplayName: string;
  type: 'info' | 'warning' | 'alert' | 'success';
  title: string;
  message: string;
  sentAt: string;
}

export interface ChartData {
  labels: string[];
  data: number[];
}

export interface UserGrowthData {
  daily: ChartData;
  weekly: ChartData;
  monthly: ChartData;
}

// === Auth ===
export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  user: User;
}

export interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}

// === API Responses ===
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: string[];
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

// === Filtres ===
export interface UserFilters {
  search?: string;
  role?: UserRole;
  subscription?: SubscriptionType;
  isOnline?: boolean;
  isBanned?: boolean;
  sortBy?: 'username' | 'createdAt' | 'lastLoginAt';
  sortOrder?: 'asc' | 'desc';
}

export interface RoomFilters {
  search?: string;
  category?: RoomCategory;
  isActive?: boolean;
  isPrivate?: boolean;
  sortBy?: 'name' | 'createdAt' | 'currentUsers';
  sortOrder?: 'asc' | 'desc';
}

export interface ReportFilters {
  status?: ReportStatus;
  sortBy?: 'createdAt' | 'status';
  sortOrder?: 'asc' | 'desc';
}

// === SignalR Events ===
export interface SignalREvents {
  userConnected: (userId: number) => void;
  userDisconnected: (userId: number) => void;
  newReport: (report: Report) => void;
  userBanned: (userId: number, reason: string) => void;
  statsUpdated: (stats: DashboardStats) => void;
  messageDeleted: (messageId: number) => void;
  roomClosed: (roomId: number) => void;
}
