<template>
  <div class="min-h-screen bg-gray-50">
    <!-- Navigation Header -->
    <nav class="bg-green-600 text-white shadow-lg">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center py-4">
          <div class="flex items-center space-x-4">
            <h1 class="text-xl font-bold">DriverConnect</h1>
            <router-link to="/home" class="hover:bg-green-500 px-3 py-2 rounded transition-colors">
              Chat
            </router-link>
            <router-link to="/departments" class="hover:bg-green-500 px-3 py-2 rounded transition-colors">
              Departments
            </router-link>
            <router-link to="/depots" class="hover:bg-green-500 px-3 py-2 rounded transition-colors">
              Depots
            </router-link>
            <router-link to="/users" class="bg-green-500 px-3 py-2 rounded transition-colors">
              Users
            </router-link>
          </div>
          <div class="flex items-center space-x-4">
            <span class="text-sm">Welcome, {{ user?.FullName }}</span>
            <button @click="handleLogout" class="bg-red-500 hover:bg-red-600 px-3 py-2 rounded text-sm transition-colors">
              Logout
            </button>
          </div>
        </div>
      </div>
    </nav>

    <main class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
      <div class="mb-6">
        <h2 class="text-2xl font-bold text-gray-900">Users & Assignments</h2>
        <p class="text-gray-600 mt-2">Manage user assignments to departments and depots</p>
      </div>

      <div v-if="loading" class="text-center py-8">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-green-600 mx-auto"></div>
        <p class="mt-2 text-gray-600">Loading users...</p>
      </div>

      <div v-else class="bg-white shadow-md rounded-lg overflow-hidden">
        <table class="min-w-full divide-y divide-gray-200">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Email</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Department</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Depot</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Roles</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
            </tr>
          </thead>
          <tbody class="bg-white divide-y divide-gray-200">
            <tr v-for="user in users" :key="user.Id" class="hover:bg-gray-50">
              <td class="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                <div class="flex items-center">
                  <div class="w-8 h-8 bg-green-100 rounded-full flex items-center justify-center mr-3">
                    <span class="text-green-600 text-sm font-semibold">{{ getInitials(user.FullName) }}</span>
                  </div>
                  {{ user.FullName }}
                </div>
              </td>
              <td class="px-6 py-4 text-sm text-gray-500">{{ user.Email }}</td>
              <td class="px-6 py-4 text-sm text-gray-500">
                <span v-if="user.DepartmentName" class="inline-flex items-center px-2 py-1 rounded text-xs font-medium bg-blue-100 text-blue-800">
                  {{ user.DepartmentName }}
                </span>
                <span v-else class="text-gray-400">Not assigned</span>
              </td>
              <td class="px-6 py-4 text-sm text-gray-500">
                <span v-if="user.DepotName" class="inline-flex items-center px-2 py-1 rounded text-xs font-medium bg-purple-100 text-purple-800">
                  {{ user.DepotName }}
                </span>
                <span v-else class="text-gray-400">Not assigned</span>
              </td>
              <td class="px-6 py-4 text-sm text-gray-500">
                <div class="flex flex-wrap gap-1">
                  <span v-for="role in user.Roles" :key="role" 
                    :class="getRoleBadgeClass(role)"
                    class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium">
                    {{ role }}
                  </span>
                </div>
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span :class="['px-2 inline-flex text-xs leading-5 font-semibold rounded-full', user.IsActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800']">
                  {{ user.IsActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                <button @click="openAssignModal(user)" 
                  class="text-indigo-600 hover:text-indigo-900 bg-indigo-50 hover:bg-indigo-100 px-3 py-1 rounded-md text-sm font-medium transition-colors">
                  Assign
                </button>
              </td>
            </tr>
            <tr v-if="users.length === 0">
              <td colspan="7" class="px-6 py-8 text-center text-gray-500">
                <div class="flex flex-col items-center">
                  <div class="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mb-4">
                    <span class="text-2xl">👥</span>
                  </div>
                  <p class="text-lg font-medium text-gray-900">No users found</p>
                  <p class="text-gray-600 mt-1">Users will appear here once registered in the system</p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </main>

    <!-- Assignment Modal -->
    <div v-if="showModal" class="fixed z-10 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div class="sm:flex sm:items-start">
              <div class="mx-auto flex-shrink-0 flex items-center justify-center h-12 w-12 rounded-full bg-indigo-100 sm:mx-0 sm:h-10 sm:w-10">
                <span class="text-indigo-600 text-lg">👤</span>
              </div>
              <div class="mt-3 text-center sm:mt-0 sm:ml-4 sm:text-left w-full">
                <h3 class="text-lg leading-6 font-medium text-gray-900 mb-2">
                  Assign User: {{ selectedUser?.FullName }}
                </h3>
                <p class="text-sm text-gray-500 mb-4">
                  Assign this user to a department and/or depot
                </p>
                <div class="space-y-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Department</label>
                    <select v-model="assignmentData.DepartmentId" 
                      class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500">
                      <option :value="null">No Department</option>
                      <option v-for="dept in departments" :key="dept.Id" :value="dept.Id">
                        {{ dept.Name }} {{ !dept.IsActive ? '(Inactive)' : '' }}
                      </option>
                    </select>
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Depot</label>
                    <select v-model="assignmentData.DepotId" 
                      class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500">
                      <option :value="null">No Depot</option>
                      <option v-for="depot in depots" :key="depot.Id" :value="depot.Id">
                        {{ depot.Name }} {{ !depot.IsActive ? '(Inactive)' : '' }}
                      </option>
                    </select>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button @click="saveAssignment" :disabled="saving" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed">
              <span v-if="saving" class="animate-spin mr-2">⏳</span>
              {{ saving ? 'Saving...' : 'Save Assignment' }}
            </button>
            <button @click="closeModal" 
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm">
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import api from '@/axios';

interface User {
  Id: string;
  FullName: string;
  Email: string;
  PhoneNumber: string | null;
  IsActive: boolean;
  CreatedAt: string;
  LastLoginAt: string | null;
  DepartmentId: number | null;
  DepartmentName: string | null;
  DepotId: number | null;
  DepotName: string | null;
  Roles: string[];
}

interface Department {
  Id: number;
  Name: string;
  Description: string;
  IsActive: boolean;
  CreatedAt: string;
}

interface Depot {
  Id: number;
  Name: string;
  Location: string;
  City: string;
  Address: string;
  PostalCode: string;
  IsActive: boolean;
  CreatedAt: string;
}

const router = useRouter();
const authStore = useAuthStore();

const loading = ref(false);
const saving = ref(false);
const users = ref<User[]>([]);
const departments = ref<Department[]>([]);
const depots = ref<Depot[]>([]);
const showModal = ref(false);
const selectedUser = ref<User | null>(null);
const assignmentData = ref({
  DepartmentId: null as number | null,
  DepotId: null as number | null
});

const user = computed(() => authStore.user);

onMounted(async () => {
  console.log('UsersView mounted - loading data');
  await Promise.all([loadUsers(), loadDepartments(), loadDepots()]);
});

const loadUsers = async () => {
  loading.value = true;
  try {
    console.log('Loading users...');
    const response = await api.get('/users');
    users.value = response.data;
    console.log(`Loaded ${users.value.length} users:`, users.value);
  } catch (error: any) {
    console.error('Error loading users:', error);
    const errorMessage = error.response?.data?.message || error.message;
    alert(`Failed to load users: ${errorMessage}`);
  } finally {
    loading.value = false;
  }
};

const loadDepartments = async () => {
  try {
    console.log('Loading departments...');
    const response = await api.get('/departments');
    departments.value = response.data;
    console.log(`Loaded ${departments.value.length} departments`);
  } catch (error: any) {
    console.error('Error loading departments:', error);
  }
};

const loadDepots = async () => {
  try {
    console.log('Loading depots...');
    const response = await api.get('/depots');
    depots.value = response.data;
    console.log(`Loaded ${depots.value.length} depots`);
  } catch (error: any) {
    console.error('Error loading depots:', error);
  }
};

const openAssignModal = (user: User) => {
  console.log('Opening assign modal for user:', user);
  console.log('User ID:', user.Id);
  
  if (!user.Id || user.Id === 'undefined') {
    console.error('Invalid user ID:', user.Id);
    alert('Error: User ID is invalid. Please refresh the page and try again.');
    return;
  }

  selectedUser.value = user;
  assignmentData.value = {
    DepartmentId: user.DepartmentId,
    DepotId: user.DepotId
  };
  showModal.value = true;
};

const closeModal = () => {
  showModal.value = false;
  selectedUser.value = null;
  assignmentData.value = {
    DepartmentId: null,
    DepotId: null
  };
};

const saveAssignment = async () => {
  if (!selectedUser.value) {
    alert('No user selected');
    return;
  }

  const userId = selectedUser.value.Id;
  
  if (!userId || userId === 'undefined') {
    alert('Error: Invalid user ID');
    return;
  }

  console.log('Saving assignment for user ID:', userId);
  console.log('Assignment data:', assignmentData.value);

  saving.value = true;
  try {
    await api.put(`/users/${userId}/assignment`, assignmentData.value);
    
    // Reload users to see the updated assignments
    await loadUsers();
    
    closeModal();
    alert('User assignment updated successfully!');
  } catch (error: any) {
    console.error('Error saving assignment:', error);
    const errorMessage = error.response?.data?.message || error.message;
    alert(`Failed to save assignment: ${errorMessage}`);
  } finally {
    saving.value = false;
  }
};

const getInitials = (name: string) => {
  return name.split(' ').map(n => n[0]).join('').toUpperCase();
};

const getRoleBadgeClass = (role: string) => {
  switch (role.toLowerCase()) {
    case 'admin':
      return 'bg-red-100 text-red-800';
    case 'manager':
      return 'bg-blue-100 text-blue-800';
    case 'driver':
      return 'bg-green-100 text-green-800';
    default:
      return 'bg-gray-100 text-gray-800';
  }
};

const handleLogout = async () => {
  await authStore.logout();
  router.push('/login');
};
</script>