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
            <router-link to="/departments" class="bg-green-500 px-3 py-2 rounded transition-colors">
              Departments
            </router-link>
            <router-link to="/depots" class="hover:bg-green-500 px-3 py-2 rounded transition-colors">
              Depots
            </router-link>
            <router-link to="/users" class="hover:bg-green-500 px-3 py-2 rounded transition-colors">
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
      <div class="mb-6 flex justify-between items-center">
        <div>
          <h2 class="text-2xl font-bold text-gray-900">Departments</h2>
          <p class="text-gray-600 mt-2">Manage departments for organizing conversations and users</p>
        </div>
        <button @click="openCreateModal" 
          class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-md flex items-center space-x-2 transition-colors">
          <span>+</span>
          <span>Add Department</span>
        </button>
      </div>

      <div v-if="loading" class="text-center py-8">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-green-600 mx-auto"></div>
        <p class="mt-2 text-gray-600">Loading departments...</p>
      </div>

      <div v-else class="bg-white shadow-md rounded-lg overflow-hidden">
        <table class="min-w-full divide-y divide-gray-200">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Description</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Created</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
            </tr>
          </thead>
          <tbody class="bg-white divide-y divide-gray-200">
            <tr v-for="department in departments" :key="department.Id" class="hover:bg-gray-50">
              <td class="px-6 py-4 whitespace-nowrap">
                <div class="flex items-center">
                  <div class="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center mr-3">
                    <span class="text-blue-600 text-sm font-semibold">D</span>
                  </div>
                  <div class="text-sm font-medium text-gray-900">{{ department.Name }}</div>
                </div>
              </td>
              <td class="px-6 py-4 text-sm text-gray-500">
                {{ department.Description || 'No description' }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span :class="['px-2 inline-flex text-xs leading-5 font-semibold rounded-full', department.IsActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800']">
                  {{ department.IsActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                {{ formatDate(department.CreatedAt) }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                <button @click="openEditModal(department)" 
                  class="text-indigo-600 hover:text-indigo-900 bg-indigo-50 hover:bg-indigo-100 px-3 py-1 rounded-md text-sm font-medium mr-2 transition-colors">
                  Edit
                </button>
                <button @click="deleteDepartment(department.Id)" 
                  class="text-red-600 hover:text-red-900 bg-red-50 hover:bg-red-100 px-3 py-1 rounded-md text-sm font-medium transition-colors">
                  Delete
                </button>
              </td>
            </tr>
            <tr v-if="departments.length === 0">
              <td colspan="5" class="px-6 py-8 text-center text-gray-500">
                <div class="flex flex-col items-center">
                  <div class="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center mb-4">
                    <span class="text-2xl">🏢</span>
                  </div>
                  <p class="text-lg font-medium text-gray-900">No departments found</p>
                  <p class="text-gray-600 mt-1">Create your first department to get started</p>
                  <button @click="openCreateModal" 
                    class="mt-4 bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-md transition-colors">
                    + Add Department
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </main>

    <!-- Create/Edit Department Modal -->
    <div v-if="showModal" class="fixed z-10 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div class="sm:flex sm:items-start">
              <div class="mx-auto flex-shrink-0 flex items-center justify-center h-12 w-12 rounded-full bg-blue-100 sm:mx-0 sm:h-10 sm:w-10">
                <span class="text-blue-600 text-lg">🏢</span>
              </div>
              <div class="mt-3 text-center sm:mt-0 sm:ml-4 sm:text-left w-full">
                <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">
                  {{ isEditing ? 'Edit Department' : 'Create Department' }}
                </h3>
                <div class="space-y-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Name *</label>
                    <input v-model="formData.Name" type="text" required 
                      class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                      placeholder="Enter department name">
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Description</label>
                    <textarea v-model="formData.Description" rows="3" 
                      class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                      placeholder="Enter department description (optional)"></textarea>
                  </div>
                  <div v-if="isEditing">
                    <label class="block text-sm font-medium text-gray-700 mb-1">Status</label>
                    <select v-model="formData.IsActive" 
                      class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500">
                      <option :value="true">Active</option>
                      <option :value="false">Inactive</option>
                    </select>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button @click="saveDepartment" :disabled="saving" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed">
              <span v-if="saving" class="animate-spin mr-2">⏳</span>
              {{ saving ? 'Saving...' : 'Save' }}
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

interface Department {
  Id: number;
  Name: string;
  Description: string;
  IsActive: boolean;
  CreatedAt: string;
}

const router = useRouter();
const authStore = useAuthStore();

const loading = ref(false);
const saving = ref(false);
const departments = ref<Department[]>([]);
const showModal = ref(false);
const isEditing = ref(false);
const editingId = ref<number | null>(null);
const formData = ref({
  Name: '',
  Description: '',
  IsActive: true
});

const user = computed(() => authStore.user);

onMounted(() => {
  console.log('DepartmentsView mounted - loading departments');
  loadDepartments();
});

const loadDepartments = async () => {
  loading.value = true;
  try {
    console.log('Loading departments...');
    const response = await api.get('/departments');
    departments.value = response.data;
    console.log(`Loaded ${departments.value.length} departments:`, departments.value);
  } catch (error: any) {
    console.error('Error loading departments:', error);
    const errorMessage = error.response?.data?.message || error.message;
    alert(`Failed to load departments: ${errorMessage}`);
  } finally {
    loading.value = false;
  }
};

const openCreateModal = () => {
  isEditing.value = false;
  editingId.value = null;
  formData.value = {
    Name: '',
    Description: '',
    IsActive: true
  };
  showModal.value = true;
};

const openEditModal = (department: Department) => {
  isEditing.value = true;
  editingId.value = department.Id;
  formData.value = {
    Name: department.Name,
    Description: department.Description || '',
    IsActive: department.IsActive
  };
  showModal.value = true;
};

const closeModal = () => {
  showModal.value = false;
};

const saveDepartment = async () => {
  if (!formData.value.Name.trim()) {
    alert('Department name is required');
    return;
  }

  saving.value = true;
  try {
    if (isEditing.value && editingId.value) {
      console.log('Updating department:', editingId.value, formData.value);
      const response = await api.put(`/departments/${editingId.value}`, formData.value);
      departments.value = response.data; // Update with returned list
    } else {
      console.log('Creating department:', formData.value);
      const response = await api.post('/departments', {
        Name: formData.value.Name,
        Description: formData.value.Description
      });
      departments.value = response.data; // Update with returned list
    }
    
    closeModal();
    alert('Department saved successfully!');
  } catch (error: any) {
    console.error('Error saving department:', error);
    const errorMessage = error.response?.data?.message || error.message;
    alert(`Failed to save department: ${errorMessage}`);
  } finally {
    saving.value = false;
  }
};

const deleteDepartment = async (id: number) => {
  if (!confirm('Are you sure you want to delete this department? This action cannot be undone.')) return;

  try {
    console.log('Deleting department:', id);
    await api.delete(`/departments/${id}`);
    // Reload the list to reflect the deletion
    await loadDepartments();
    alert('Department deleted successfully!');
  } catch (error: any) {
    console.error('Error deleting department:', error);
    const errorMessage = error.response?.data?.message || error.message;
    alert(`Failed to delete department: ${errorMessage}`);
  }
};

const formatDate = (dateString: string) => {
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { 
      year: 'numeric', 
      month: 'short', 
      day: 'numeric' 
    });
  } catch {
    return 'Invalid date';
  }
};

const handleLogout = async () => {
  await authStore.logout();
  router.push('/login');
};
</script>