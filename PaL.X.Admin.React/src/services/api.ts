import axios, { AxiosInstance, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { ApiResponse, PaginatedResponse, User, Room, Report, Badge, AuditLog, DashboardStats, LoginRequest, LoginResponse, UserFilters, RoomFilters, ReportFilters, UserGrowthData, Role, BroadcastRequest, BroadcastHistory } from '../types';

// API Base Configuration
const API_BASE_URL = import.meta.env.VITE_API_URL || '/api';

class ApiService {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor - Add auth token
    this.client.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        // SÉCURITÉ: Utilise sessionStorage (vidé à la fermeture du navigateur)
        const token = sessionStorage.getItem('auth_token');
        if (token && config.headers) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor - Handle errors
    this.client.interceptors.response.use(
      (response) => response,
      (error: AxiosError) => {
        if (error.response?.status === 401) {
          sessionStorage.removeItem('auth_token');
          sessionStorage.removeItem('admin_user');
          window.location.href = '/login';
        }
        return Promise.reject(error);
      }
    );
  }

  // ============================================
  // Authentication
  // ============================================
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    // Utilise l'endpoint admin/login qui vérifie le RoleLevel
    const response = await this.client.post<LoginResponse>('/auth/admin/login', credentials);
    return response.data;
  }

  async validateToken(): Promise<User> {
    const response = await this.client.get<User>('/auth/validate');
    return response.data;
  }

  async logout(): Promise<void> {
    await this.client.post('/auth/logout');
  }

  // ============================================
  // Dashboard
  // ============================================
  async getDashboardStats(): Promise<DashboardStats> {
    const response = await this.client.get<DashboardStats>('/admin/dashboard/stats');
    return response.data;
  }

  async getUserGrowthData(): Promise<UserGrowthData> {
    const response = await this.client.get<UserGrowthData>('/admin/dashboard/user-growth');
    return response.data;
  }

  // ============================================
  // Roles Management
  // ============================================
  async getRoles(): Promise<Role[]> {
    const response = await this.client.get<Role[]>('/admin/roles');
    return response.data;
  }

  // ============================================
  // Room Roles Management (Rôles de Salons)
  // ============================================
  async getRoomRoles(): Promise<any[]> {
    const response = await this.client.get<any[]>('/admin/room-roles');
    return response.data;
  }

  // ============================================
  // Categories Management
  // ============================================
  async getCategories(): Promise<any[]> {
    const response = await this.client.get<any[]>('/admin/categories');
    return response.data;
  }

  async getCategoryById(id: number): Promise<any> {
    const response = await this.client.get<any>(`/admin/categories/${id}`);
    return response.data;
  }

  async createCategory(data: any): Promise<any> {
    const response = await this.client.post<any>('/admin/categories', data);
    return response.data;
  }

  async updateCategory(id: number, data: any): Promise<any> {
    const response = await this.client.put<any>(`/admin/categories/${id}`, data);
    return response.data;
  }

  async deleteCategory(id: number): Promise<any> {
    const response = await this.client.delete<any>(`/admin/categories/${id}`);
    return response.data;
  }

  // ============================================
  // SubCategories Management
  // ============================================
  async getSubCategories(categoryId?: number): Promise<any[]> {
    const params = categoryId ? { categoryId } : {};
    const response = await this.client.get<any[]>('/admin/subcategories', { params });
    return response.data;
  }

  async getSubCategoryById(id: number): Promise<any> {
    const response = await this.client.get<any>(`/admin/subcategories/${id}`);
    return response.data;
  }

  async createSubCategory(data: any): Promise<any> {
    const response = await this.client.post<any>('/admin/subcategories', data);
    return response.data;
  }

  async updateSubCategory(id: number, data: any): Promise<any> {
    const response = await this.client.put<any>(`/admin/subcategories/${id}`, data);
    return response.data;
  }

  async deleteSubCategory(id: number): Promise<any> {
    const response = await this.client.delete<any>(`/admin/subcategories/${id}`);
    return response.data;
  }

  // ============================================
  // Broadcast / Annonces globales
  // ============================================
  async sendBroadcast(data: BroadcastRequest): Promise<{ success: boolean; message: string }> {
    const response = await this.client.post<{ success: boolean; message: string }>('/admin/broadcast', data);
    return response.data;
  }

  async getBroadcastHistory(page: number = 1, pageSize: number = 20): Promise<PaginatedResponse<BroadcastHistory>> {
    const response = await this.client.get<PaginatedResponse<BroadcastHistory>>('/admin/broadcasts', { 
      params: { page, pageSize } 
    });
    return response.data;
  }

  async updateBroadcast(id: number, data: BroadcastRequest): Promise<{ success: boolean; message: string }> {
    const response = await this.client.put<{ success: boolean; message: string }>(`/admin/broadcasts/${id}`, data);
    return response.data;
  }

  async deleteBroadcast(id: number): Promise<{ success: boolean; message: string }> {
    const response = await this.client.delete<{ success: boolean; message: string }>(`/admin/broadcasts/${id}`);
    return response.data;
  }

  // ============================================
  // Users Management
  // ============================================
  async getUsers(page: number = 1, pageSize: number = 20, filters?: UserFilters): Promise<PaginatedResponse<User>> {
    const params = { page, pageSize, ...filters };
    const response = await this.client.get<PaginatedResponse<User>>('/admin/users', { params });
    return response.data;
  }

  async getUserById(id: number): Promise<User> {
    const response = await this.client.get<User>(`/admin/users/${id}`);
    return response.data;
  }

  async updateUser(id: number, data: Partial<User>): Promise<User> {
    const response = await this.client.put<User>(`/admin/users/${id}`, data);
    return response.data;
  }

  async banUser(id: number, reason: string, duration?: number): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>(`/admin/users/${id}/ban`, { reason, duration });
    return response.data;
  }

  async unbanUser(id: number): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>(`/admin/users/${id}/unban`);
    return response.data;
  }

  async deleteUser(id: number): Promise<ApiResponse<void>> {
    const response = await this.client.delete<ApiResponse<void>>(`/admin/users/${id}`);
    return response.data;
  }

  async changeUserRole(id: number, role: string): Promise<User> {
    const response = await this.client.post<User>(`/admin/users/${id}/role`, { role });
    return response.data;
  }

  async warnUser(id: number, reason: string): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>(`/admin/users/${id}/warn`, { reason });
    return response.data;
  }

  // ============================================
  // Rooms Management
  // ============================================
  async getRooms(page: number = 1, pageSize: number = 20, filters?: RoomFilters): Promise<PaginatedResponse<Room>> {
    const params = { page, pageSize, ...filters };
    const response = await this.client.get<PaginatedResponse<Room>>('/admin/rooms', { params });
    return response.data;
  }

  async getRoomById(id: number): Promise<Room> {
    const response = await this.client.get<Room>(`/admin/rooms/${id}`);
    return response.data;
  }

  async closeRoom(id: number, reason?: string): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>(`/admin/rooms/${id}/close`, { reason });
    return response.data;
  }

  async deleteRoom(id: number): Promise<ApiResponse<void>> {
    const response = await this.client.delete<ApiResponse<void>>(`/admin/rooms/${id}`);
    return response.data;
  }

  async getRoomMessages(roomId: number, page: number = 1, pageSize: number = 50): Promise<PaginatedResponse<any>> {
    const response = await this.client.get(`/admin/rooms/${roomId}/messages`, { params: { page, pageSize } });
    return response.data;
  }

  // ============================================
  // Reports / Moderation
  // ============================================
  async getReports(page: number = 1, pageSize: number = 20, filters?: ReportFilters): Promise<PaginatedResponse<Report>> {
    const params = { page, pageSize, ...filters };
    const response = await this.client.get<PaginatedResponse<Report>>('/admin/reports', { params });
    return response.data;
  }

  async getReportById(id: number): Promise<Report> {
    const response = await this.client.get<Report>(`/admin/reports/${id}`);
    return response.data;
  }

  async resolveReport(id: number, resolution: string, action?: string): Promise<Report> {
    const response = await this.client.post<Report>(`/admin/reports/${id}/resolve`, { resolution, action });
    return response.data;
  }

  async dismissReport(id: number, reason?: string): Promise<Report> {
    const response = await this.client.post<Report>(`/admin/reports/${id}/dismiss`, { reason });
    return response.data;
  }

  // ============================================
  // Badges
  // ============================================
  async getBadges(): Promise<Badge[]> {
    const response = await this.client.get<Badge[]>('/admin/badges');
    return response.data;
  }

  async createBadge(badge: Partial<Badge>): Promise<Badge> {
    const response = await this.client.post<Badge>('/admin/badges', badge);
    return response.data;
  }

  async updateBadge(id: number, data: Partial<Badge>): Promise<Badge> {
    const response = await this.client.put<Badge>(`/admin/badges/${id}`, data);
    return response.data;
  }

  async deleteBadge(id: number): Promise<ApiResponse<void>> {
    const response = await this.client.delete<ApiResponse<void>>(`/admin/badges/${id}`);
    return response.data;
  }

  async assignBadgeToUser(userId: number, badgeId: number): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>(`/admin/users/${userId}/badges`, { badgeId });
    return response.data;
  }

  async removeBadgeFromUser(userId: number, badgeId: number): Promise<ApiResponse<void>> {
    const response = await this.client.delete<ApiResponse<void>>(`/admin/users/${userId}/badges/${badgeId}`);
    return response.data;
  }

  // ============================================
  // Audit Logs
  // ============================================
  async getAuditLogs(page: number = 1, pageSize: number = 50): Promise<PaginatedResponse<AuditLog>> {
    const response = await this.client.get<PaginatedResponse<AuditLog>>('/admin/logs', { params: { page, pageSize } });
    return response.data;
  }

  async getLogsByUser(userId: number, page: number = 1, pageSize: number = 50): Promise<PaginatedResponse<AuditLog>> {
    const response = await this.client.get<PaginatedResponse<AuditLog>>(`/admin/logs/user/${userId}`, { params: { page, pageSize } });
    return response.data;
  }

  // ============================================
  // Messages
  // ============================================
  async deleteMessage(messageId: number): Promise<ApiResponse<void>> {
    const response = await this.client.delete<ApiResponse<void>>(`/admin/messages/${messageId}`);
    return response.data;
  }

  async searchMessages(query: string, page: number = 1, pageSize: number = 50): Promise<PaginatedResponse<any>> {
    const response = await this.client.get('/admin/messages/search', { params: { query, page, pageSize } });
    return response.data;
  }

  // ============================================
  // Subscriptions
  // ============================================
  async grantSubscription(userId: number, type: string, durationDays: number): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>(`/admin/users/${userId}/subscription`, { type, durationDays });
    return response.data;
  }

  async revokeSubscription(userId: number): Promise<ApiResponse<void>> {
    const response = await this.client.delete<ApiResponse<void>>(`/admin/users/${userId}/subscription`);
    return response.data;
  }

  // ============================================
  // Subscriptions Management (Full)
  // ============================================
  async getSubscriptionTiers(): Promise<any[]> {
    const response = await this.client.get('/admin/subscriptions/tiers');
    return response.data;
  }

  async updateSubscriptionTier(id: number, data: any): Promise<any> {
    const response = await this.client.put(`/admin/subscriptions/tiers/${id}`, data);
    return response.data;
  }

  async getSubscriptionDurations(): Promise<any[]> {
    const response = await this.client.get('/admin/subscriptions/durations');
    return response.data;
  }

  async updateSubscriptionDuration(id: number, data: any): Promise<any> {
    const response = await this.client.put(`/admin/subscriptions/durations/${id}`, data);
    return response.data;
  }

  async getSubscriptionPrices(): Promise<any[]> {
    const response = await this.client.get('/admin/subscriptions/prices');
    return response.data;
  }

  async setSubscriptionPrice(tierId: number, durationId: number, data: { priceCents: number; points: number }): Promise<any> {
    const response = await this.client.put(`/admin/subscriptions/prices/${tierId}/${durationId}`, data);
    return response.data;
  }

  async resetSubscriptionPrice(tierId: number, durationId: number): Promise<void> {
    await this.client.delete(`/admin/subscriptions/prices/${tierId}/${durationId}`);
  }

  async getSubscriptionStats(): Promise<any> {
    const response = await this.client.get('/admin/subscriptions/stats');
    return response.data;
  }

  async getUserSubscriptions(filters?: { status?: string; tierId?: number }): Promise<any[]> {
    const response = await this.client.get('/admin/subscriptions/users', { params: filters });
    return response.data;
  }

  async grantUserSubscription(userId: number, data: { tierId: number; durationId: number; paymentMethod: string }): Promise<any> {
    const response = await this.client.post(`/admin/subscriptions/users/${userId}/grant`, data);
    return response.data;
  }

  async revokeUserSubscription(userId: number, reason: string): Promise<void> {
    await this.client.post(`/admin/subscriptions/users/${userId}/revoke`, { reason });
  }

  async extendUserSubscription(userId: number, days: number, reason: string): Promise<void> {
    await this.client.post(`/admin/subscriptions/users/${userId}/extend`, { days, reason });
  }

  async getUserPoints(userId: number): Promise<any> {
    const response = await this.client.get(`/admin/subscriptions/users/${userId}/points`);
    return response.data;
  }

  async grantUserPoints(userId: number, amount: number, reason: string): Promise<any> {
    const response = await this.client.post(`/admin/subscriptions/users/${userId}/points/grant`, { amount, reason });
    return response.data;
  }

  async getUserPointsHistory(userId: number): Promise<any[]> {
    const response = await this.client.get(`/admin/subscriptions/users/${userId}/points/history`);
    return response.data;
  }

  async checkUserTrial(userId: number, tierId: number): Promise<{ canUseTrial: boolean; remainingDays?: number }> {
    const response = await this.client.get(`/admin/subscriptions/users/${userId}/trial/${tierId}`);
    return response.data;
  }

  async activateUserTrial(userId: number, tierId: number): Promise<any> {
    const response = await this.client.post(`/admin/subscriptions/users/${userId}/trial/${tierId}`);
    return response.data;
  }

  // ============================================
  // Room Subscriptions Management
  // ============================================
  async getRoomSubscriptionTiers(): Promise<any[]> {
    const response = await this.client.get('/admin/room-subscriptions/tiers');
    return response.data;
  }

  async updateRoomSubscriptionTier(id: number, data: any): Promise<any> {
    const response = await this.client.put(`/admin/room-subscriptions/tiers/${id}`, data);
    return response.data;
  }

  async getRoomSubscriptions(): Promise<any[]> {
    const response = await this.client.get('/admin/room-subscriptions/subscriptions');
    return response.data;
  }

  async getRoomSubscription(roomId: number): Promise<any> {
    const response = await this.client.get(`/admin/room-subscriptions/subscriptions/room/${roomId}`);
    return response.data;
  }

  async grantRoomSubscription(data: { roomId: number; tierId: number; grantedByAdminId?: number; durationDays?: number }): Promise<any> {
    const response = await this.client.post('/admin/room-subscriptions/subscriptions/grant', data);
    return response.data;
  }

  async revokeRoomSubscription(roomId: number): Promise<any> {
    const response = await this.client.post(`/admin/room-subscriptions/subscriptions/revoke/${roomId}`);
    return response.data;
  }

  async extendRoomSubscription(roomId: number, days: number): Promise<any> {
    const response = await this.client.post(`/admin/room-subscriptions/subscriptions/extend/${roomId}?days=${days}`);
    return response.data;
  }

  async searchRoomsForSubscription(query?: string, limit: number = 50): Promise<any[]> {
    const params = new URLSearchParams();
    if (query) params.append('query', query);
    params.append('limit', limit.toString());
    const response = await this.client.get(`/admin/room-subscriptions/rooms/search?${params.toString()}`);
    return response.data;
  }

  async getRoomSubscriptionStats(): Promise<any> {
    const response = await this.client.get('/admin/room-subscriptions/stats');
    return response.data;
  }

  // ============================================
  // System
  // ============================================
  async getSystemInfo(): Promise<any> {
    const response = await this.client.get('/admin/system/info');
    return response.data;
  }

  async enableMaintenanceMode(enabled: boolean, message?: string): Promise<ApiResponse<void>> {
    const response = await this.client.post<ApiResponse<void>>('/admin/system/maintenance', { enabled, message });
    return response.data;
  }
}

export const apiService = new ApiService();
export default apiService;
