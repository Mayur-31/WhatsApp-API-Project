// DriverConnectApp.Frontend/src/stores/auth.ts
import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import api from '@/axios';
import { useRouter } from 'vue-router';

interface User {
  Id: string;
  FullName: string;
  Email: string;
  Roles: string[];
  DepartmentId?: number;
  DepotId?: number;
  TeamId?: number;
  TeamRole?: string;
  Team?: any;
  IsActive: boolean;
  CreatedAt: string;
  LastLoginAt?: string;
  IsSuperAdmin?: boolean;
  IsAdmin?: boolean;
}

interface LoginCredentials {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export const useAuthStore = defineStore('auth', () => {
  const router = useRouter();
  
  const user = ref<User | null>(null);
  const token = ref<string | null>(localStorage.getItem('authToken'));
  const isAuthenticated = ref<boolean>(false);
  const currentTeam = ref<any>(null);
  const availableTeams = ref<any[]>([]);

  // Helper function to ensure roles is always an array and normalize case
  const ensureRolesArray = (roles: any): string[] => {
    let rolesArray: string[] = [];
    
    if (Array.isArray(roles)) {
      rolesArray = roles;
    } else if (typeof roles === 'string') {
      rolesArray = [roles];
    } else if (roles) {
      rolesArray = [String(roles)];
    }
    
    // Convert to uppercase for consistent comparison
    return rolesArray.map(role => role.toUpperCase());
  };

  // Case-insensitive role checking
  const isAdmin = computed(() => {
    if (!user.value) return false;
    const roles = ensureRolesArray(user.value.Roles);
    const hasAdmin = roles.includes('ADMIN') || roles.includes('SUPERADMIN');
    console.log('🛡️ isAdmin check:', hasAdmin, 'Roles:', roles);
    return hasAdmin;
  });

  const isSuperAdmin = computed(() => {
    if (!user.value) return false;
    const roles = ensureRolesArray(user.value.Roles);
    const hasSuperAdmin = roles.includes('SUPERADMIN');
    console.log('👑 isSuperAdmin check:', hasSuperAdmin, 'Roles:', roles);
    return hasSuperAdmin;
  });

  const isManager = computed(() => {
    if (!user.value) return false;
    const roles = ensureRolesArray(user.value.Roles);
    const hasManager = roles.includes('MANAGER');
    console.log('👔 isManager check:', hasManager, 'Roles:', roles);
    return hasManager;
  });

  const isAdminOrManager = computed(() => {
    const result = isAdmin.value || isManager.value;
    console.log('🔑 isAdminOrManager check:', result, '(Admin:', isAdmin.value, 'Manager:', isManager.value, ')');
    return result;
  });

  // Get user's team ID
  const userTeamId = computed(() => {
    return user.value?.TeamId || 0;
  });

  // Check if user has a team assigned
  const hasTeam = computed(() => {
    return !!user.value?.TeamId;
  });

  // Initialize from localStorage
  const initialize = () => {
    const storedUser = localStorage.getItem('user');
    const storedToken = localStorage.getItem('authToken');
    
    if (storedUser && storedToken) {
      try {
        const userData = JSON.parse(storedUser);
        // Ensure roles is an array when loading from localStorage
        userData.Roles = ensureRolesArray(userData.Roles);
        user.value = userData;
        token.value = storedToken;
        isAuthenticated.value = true;
        
        // Set current team if user has one
        if (userData.Team) {
          currentTeam.value = userData.Team;
        }
        
        console.log('✅ Auth initialized from localStorage', user.value);
        console.log('🔑 Initialized roles:', userData.Roles);
        console.log('🏢 User team ID:', userData.TeamId);
        console.log('👑 Is SuperAdmin:', isSuperAdmin.value);
      } catch (error) {
        console.error('Error parsing stored user data:', error);
        clearAuthData();
      }
    }
  };

  const setUser = (userData: User) => {
    // Ensure roles is always an array and normalized
    userData.Roles = ensureRolesArray(userData.Roles);
    user.value = userData;
    localStorage.setItem('user', JSON.stringify(userData));
    
    // Set current team
    if (userData.Team) {
      currentTeam.value = userData.Team;
    }
    
    console.log('✅ User set with roles:', userData.Roles, 'team:', userData.TeamId, 'team object:', userData.Team);
  };

  const setToken = (newToken: string) => {
    token.value = newToken;
    localStorage.setItem('authToken', newToken);
    api.defaults.headers.common['Authorization'] = `Bearer ${newToken}`;
  };

  const clearTeamCache = () => {
    availableTeams.value = [];
    currentTeam.value = null;
  };
  
  const clearAuthData = () => {
    user.value = null;
    token.value = null;
    isAuthenticated.value = false;
    currentTeam.value = null;
    availableTeams.value = [];
    localStorage.removeItem('user');
    localStorage.removeItem('authToken');
    delete api.defaults.headers.common['Authorization'];
  };

  const login = async (credentials: LoginCredentials) => {
    try {
      console.log('🔐 Attempting login for:', credentials.email);
      
      const response = await api.post('/auth/login', {
        Email: credentials.email,
        Password: credentials.password,
        RememberMe: credentials.rememberMe || false
      });

      console.log('✅ Login response:', response.data);

      if (response.data.success) {
        setUser(response.data.user);
        setToken(response.data.token || 'authenticated');
        isAuthenticated.value = true;
        
        // Load available teams for admin users
        if (isSuperAdmin.value || isAdmin.value) {
          await loadAvailableTeams();
        }
        
        console.log('✅ Login successful, user:', response.data.user);
        console.log('✅ User roles (normalized):', ensureRolesArray(response.data.user.Roles));
        console.log('✅ User team ID:', response.data.user.TeamId);
        console.log('✅ User team:', response.data.user.Team);
        console.log('✅ Is SuperAdmin:', isSuperAdmin.value);
        console.log('✅ Is Admin:', isAdmin.value);
        console.log('✅ Is Manager:', isManager.value);

        return { success: true, user: response.data.user };
      } else {
        return { success: false, error: response.data.message };
      }
    } catch (error: any) {
      console.error('❌ Login error:', error);
      const errorMessage = error.response?.data?.message || error.message || 'Login failed';
      return { success: false, error: errorMessage };
    }
  };

  const loadAvailableTeams = async (forceRefresh = false) => {
    try {
    // Add cache busting parameter if force refresh
      const url = forceRefresh ? `/api/teams?t=${Date.now()}` : '/api/teams';
      const response = await api.get(url);
      availableTeams.value = response.data;
      console.log('✅ Loaded available teams:', availableTeams.value.length);
    } catch (error) {
      console.error('Error loading teams:', error);
      availableTeams.value = [];
    }
  };

  const switchTeam = async (teamId: number | null) => {
    try {
      if (teamId) {
        const response = await api.get(`/api/teams/${teamId}`);
        currentTeam.value = response.data;
      } else {
        currentTeam.value = null; // All teams view
      }
      
      // Notify other components about team change
      window.dispatchEvent(new CustomEvent('team-changed', { 
        detail: { teamId } 
      }));
      
      console.log('✅ Switched team to:', currentTeam.value?.Name || 'All Teams');
    } catch (error) {
      console.error('Error switching team:', error);
      throw error;
    }
  };

  const register = async (userData: any) => {
    try {
      console.log('📝 Attempting registration for:', userData.email);
      
      const response = await api.post('/auth/register', {
        FullName: userData.fullName,
        Email: userData.email,
        Password: userData.password
      });

      console.log('✅ Registration response:', response.data);

      if (response.data.success) {
        return { success: true, message: response.data.message };
      } else {
        return { success: false, error: response.data.message };
      }
    } catch (error: any) {
      console.error('❌ Registration error:', error);
      const errorMessage = error.response?.data?.message || error.message || 'Registration failed';
      return { success: false, error: errorMessage };
    }
  };

  const logout = async () => {
    try {
      await api.post('/auth/logout');
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      clearAuthData();
      clearTeamCache();
      router.push('/login');
    }
  };

  const checkAuth = async () => {
    try {
      const response = await api.get('/auth/me');
      setUser(response.data);
      isAuthenticated.value = true;
      
      // Load available teams for admin users
      if (isSuperAdmin.value || isAdmin.value) {
        await loadAvailableTeams();
      }
      
      return true;
    } catch (error) {
      console.error('Auth check failed:', error);
      clearAuthData();
      return false;
    }
  };

  // Debug method to check current user roles
  const debugRoles = () => {
    console.log('🔍 DEBUG ROLES:');
    console.log('User:', user.value);
    console.log('Raw Roles:', user.value?.Roles);
    console.log('Normalized Roles:', ensureRolesArray(user.value?.Roles));
    console.log('isSuperAdmin:', isSuperAdmin.value);
    console.log('isAdmin:', isAdmin.value);
    console.log('isManager:', isManager.value);
    console.log('isAdminOrManager:', isAdminOrManager.value);
    console.log('Team ID:', user.value?.TeamId);
    console.log('Team:', user.value?.Team);
    console.log('Current Team:', currentTeam.value);
    console.log('Available Teams:', availableTeams.value);
  };

  // Initialize on import
  initialize();

  return {
    user,
    token,
    isAuthenticated,
    isSuperAdmin,
    isAdmin,
    isManager,
    isAdminOrManager,
    userTeamId,
    hasTeam,
    currentTeam,
    availableTeams,
    login,
    register,
    logout,
    checkAuth,
    setUser,
    setToken,
    clearAuthData,
    loadAvailableTeams,
    switchTeam,
    debugRoles
  };
});