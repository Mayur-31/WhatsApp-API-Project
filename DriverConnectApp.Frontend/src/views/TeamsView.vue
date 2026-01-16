<!-- Updated TeamsView.vue with fixed team deletion logic -->
<template>
  <div class="min-h-screen bg-gray-50 py-8">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <!-- Header -->
      <div class="mb-8">
        <h1 class="text-3xl font-bold text-gray-900">Team Management</h1>
        <p class="text-gray-600 mt-2">Manage teams and their WhatsApp configurations</p>
      </div>

      <!-- Create Team Button -->
      <div class="mb-6 flex justify-between items-center">
        <div class="flex space-x-4">
          <button 
            @click="showCreateModal = true"
            class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg flex items-center space-x-2 transition-colors"
          >
            <span>+</span>
            <span>Create New Team</span>
          </button>
        </div>
      </div>

      <!-- Teams Grid -->
      <div v-if="loading" class="flex justify-center py-12">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-green-600"></div>
      </div>

      <div v-else-if="teams.length === 0" class="text-center py-12 bg-white rounded-lg shadow">
        <div class="text-6xl mb-4">😴</div>
        <h3 class="text-xl font-semibold text-gray-900 mb-2">No Teams Created</h3>
        <p class="text-gray-600 mb-6">Get started by creating your first team</p>
        <button 
          @click="showCreateModal = true"
          class="bg-green-600 hover:bg-green-700 text-white px-6 py-3 rounded-lg transition-colors"
        >
          Create Your First Team
        </button>
      </div>

      <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div 
          v-for="team in teams" 
          :key="team.id"
          class="bg-white rounded-lg shadow-md border border-gray-200 hover:shadow-lg transition-shadow duration-300"
        >
          <div class="p-6">
            <!-- Team Header -->
            <div class="flex justify-between items-start mb-4">
              <div class="flex-1 min-w-0">
                <h3 class="text-lg font-semibold text-gray-900 truncate">{{ team.name || 'Unnamed Team' }}</h3>
                <p class="text-sm text-gray-500 mt-1 line-clamp-2">{{ team.description || 'No description' }}</p>
              </div>
              <span 
                :class="[
                  'px-2 py-1 text-xs rounded-full whitespace-nowrap ml-2',
                  team.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                ]"
              >
                {{ team.isActive ? 'Active' : 'Inactive' }}
              </span>
            </div>

            <!-- Team Stats -->
            <div class="grid grid-cols-4 gap-2 mb-4 text-center">
              <div>
                <p class="text-xl font-bold text-blue-600">{{ team.userCount || 0 }}</p>
                <p class="text-xs text-gray-500">Users</p>
              </div>
              <div>
                <p class="text-xl font-bold text-green-600">{{ team.contactCount || 0 }}</p>
                <p class="text-xs text-gray-500">Contacts</p>
              </div>
              <div>
                <p class="text-xl font-bold text-purple-600">{{ team.chatCount || 0 }}</p>
                <p class="text-xs text-gray-500">Chats</p>
              </div>
              <div>
                <p class="text-xl font-bold text-orange-600">{{ team.groupCount || 0 }}</p>
                <p class="text-xs text-gray-500">Groups</p>
              </div>
            </div>

            <!-- WhatsApp Info -->
            <div class="mb-4 p-3 bg-gray-50 rounded-lg">
              <p class="text-sm font-medium text-gray-700 mb-1">WhatsApp Business</p>
              <p class="text-xs text-gray-600 truncate" :title="team.whatsAppPhoneNumber || 'Not set'">
                Phone: {{ team.whatsAppPhoneNumber || 'Not set' }}
              </p>
              <p class="text-xs text-gray-600 truncate" :title="team.whatsAppPhoneNumberId || 'Not set'">
                ID: {{ team.whatsAppPhoneNumberId || 'Not set' }}
              </p>
            </div>

            <!-- Actions -->
            <div class="flex space-x-2">
              <button 
                @click="editTeam(team)"
                class="flex-1 bg-blue-600 hover:bg-blue-700 text-white py-2 px-3 rounded text-sm transition-colors"
              >
                Edit
              </button>
              <button 
                @click="manageTeamUsers(team)"
                class="flex-1 bg-green-600 hover:bg-green-700 text-white py-2 px-3 rounded text-sm transition-colors"
              >
                Users
              </button>
              <button 
                @click="deleteTeam(team)"
                :disabled="team.userCount > 0"
                :class="[
                  'flex-1 py-2 px-3 rounded text-sm transition-colors',
                  team.userCount > 0
                    ? 'bg-gray-300 text-gray-500 cursor-not-allowed'
                    : 'bg-red-600 hover:bg-red-700 text-white'
                ]"
                :title="getDeleteButtonTitle(team)"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create/Edit Team Modal -->
    <div v-if="showCreateModal || showEditModal" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div class="bg-white rounded-lg max-w-md w-full max-h-[90vh] overflow-y-auto">
        <div class="p-6">
          <h2 class="text-xl font-semibold mb-4">
            {{ showEditModal ? 'Edit Team' : 'Create New Team' }}
          </h2>

          <form @submit.prevent="saveTeam">
            <div class="space-y-4">
              <!-- Team Name -->
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Team Name *</label>
                <input 
                  v-model="teamForm.name"
                  type="text" 
                  required
                  class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                  placeholder="Enter team name"
                />
              </div>

              <!-- Description -->
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Description</label>
                <textarea 
                  v-model="teamForm.description"
                  rows="3"
                  class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                  placeholder="Team description (optional)"
                ></textarea>
              </div>

              <!-- WhatsApp Configuration -->
              <div class="border-t pt-4">
                <h3 class="text-lg font-medium text-gray-900 mb-3">WhatsApp Business Configuration</h3>
                
                <!-- WhatsApp Phone Number ID -->
                <div class="mb-3">
                  <label class="block text-sm font-medium text-gray-700 mb-1">WhatsApp Phone Number ID</label>
                  <input 
                    v-model="teamForm.whatsAppPhoneNumberId"
                    type="text" 
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                    placeholder="e.g., 123456789012345"
                  />
                  <p class="text-xs text-gray-500 mt-1">Optional for WhatsApp integration</p>
                </div>

                <!-- WhatsApp Access Token -->
                <div class="mb-3">
                  <label class="block text-sm font-medium text-gray-700 mb-1">WhatsApp Access Token</label>
                  <input 
                    v-model="teamForm.whatsAppAccessToken"
                    type="password" 
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                    placeholder="Enter access token"
                  />
                  <p class="text-xs text-gray-500 mt-1">Optional for WhatsApp integration</p>
                </div>

                <!-- WhatsApp Business Account ID -->
                <div class="mb-3">
                  <label class="block text-sm font-medium text-gray-700 mb-1">WhatsApp Business Account ID</label>
                  <input 
                    v-model="teamForm.whatsAppBusinessAccountId"
                    type="text" 
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                    placeholder="e.g., 123456789012345"
                  />
                  <p class="text-xs text-gray-500 mt-1">Optional for WhatsApp integration</p>
                </div>

                <!-- WhatsApp Phone Number -->
                <div class="mb-3">
                  <label class="block text-sm font-medium text-gray-700 mb-1">WhatsApp Phone Number</label>
                  <input 
                    v-model="teamForm.whatsAppPhoneNumber"
                    type="text" 
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                    placeholder="e.g., +1234567890"
                  />
                </div>

                <!-- API Version -->
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">API Version</label>
                  <input 
                    v-model="teamForm.apiVersion"
                    type="text" 
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                    placeholder="e.g., 18.0"
                  />
                </div>
              </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Country Code</label>
                  <select 
                    v-model="teamForm.countryCode"
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
                  >
                    <option value="44">🇬🇧 UK (+44)</option>
                    <option value="91">🇮🇳 India (+91)</option>
                    <option value="1">🇺🇸 USA/Canada (+1)</option>
                    <option value="61">🇦🇺 Australia (+61)</option>
                    <option value="33">🇫🇷 France (+33)</option>
                    <option value="49">🇩🇪 Germany (+49)</option>
                  </select>
                  <p class="text-xs text-gray-500 mt-1">Used to normalize phone numbers for driver lookup</p>
                </div>
              <!-- Active Status (only for edit) -->
              <div v-if="showEditModal" class="flex items-center">
                <input 
                  v-model="teamForm.isActive"
                  type="checkbox" 
                  id="isActive"
                  class="h-4 w-4 text-green-600 focus:ring-green-500 border-gray-300 rounded transition-colors"
                />
                <label for="isActive" class="ml-2 block text-sm text-gray-700">
                  Team is active
                </label>
              </div>
            </div>

            <!-- Actions -->
            <div class="flex justify-end space-x-3 mt-6">
              <button 
                type="button"
                @click="closeModal"
                class="px-4 py-2 text-gray-600 hover:text-gray-800 transition-colors"
              >
                Cancel
              </button>
              <button 
                type="submit"
                :disabled="saving"
                class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
              >
                {{ saving ? 'Saving...' : (showEditModal ? 'Update Team' : 'Create Team') }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Manage Team Users Modal -->
    <div v-if="showUsersModal && selectedTeam" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div class="bg-white rounded-lg max-w-2xl w-full max-h-[90vh] overflow-y-auto">
        <div class="p-6">
          <div class="flex justify-between items-center mb-4">
            <h2 class="text-xl font-semibold">
              Manage Users: {{ selectedTeam.name || 'Unnamed Team' }}
            </h2>
            <button 
              @click="closeUsersModal"
              class="text-gray-400 hover:text-gray-600 transition-colors"
            >
              <span class="text-2xl">×</span>
            </button>
          </div>

          <!-- Users List -->
          <div class="mb-6">
            <h3 class="text-lg font-medium text-gray-900 mb-3">Team Users ({{ teamUsers.length }})</h3>
            
            <div v-if="teamUsersLoading" class="flex justify-center py-8">
              <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-green-600"></div>
            </div>

            <div v-else class="space-y-3">
              <div 
                v-for="user in teamUsers" 
                :key="user.id"
                class="flex items-center justify-between p-3 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
              >
                <div class="flex-1 min-w-0">
                  <p class="font-medium text-gray-900 truncate">{{ user.fullName }}</p>
                  <p class="text-sm text-gray-500 truncate">{{ user.email }}</p>
                  <div class="flex items-center space-x-2 mt-1">
                    <span class="text-xs bg-blue-100 text-blue-800 px-2 py-1 rounded">
                      {{ user.teamRole || 'TeamMember' }}
                    </span>
                    <span v-if="user.departmentName" class="text-xs bg-gray-100 text-gray-800 px-2 py-1 rounded">
                      {{ user.departmentName }}
                    </span>
                    <span v-if="user.depotName" class="text-xs bg-gray-100 text-gray-800 px-2 py-1 rounded">
                      {{ user.depotName }}
                    </span>
                  </div>
                </div>
                <button 
                  @click="removeUserFromTeam(user)"
                  class="text-red-600 hover:text-red-800 text-sm font-medium ml-4 transition-colors"
                  title="Remove from team"
                >
                  Remove
                </button>
              </div>

              <div v-if="teamUsers.length === 0" class="text-center py-8 text-gray-500 bg-gray-50 rounded-lg">
                <div class="text-4xl mb-2">👤</div>
                <p>No users assigned to this team</p>
              </div>
            </div>
          </div>

          <!-- Assign User Section -->
          <div class="border-t pt-6">
            <h3 class="text-lg font-medium text-gray-900 mb-3">Assign User to Team</h3>
            <div class="flex space-x-3">
              <select 
                v-model="selectedUserId"
                class="flex-1 border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-colors"
              >
                <option value="">Select a user...</option>
                <option 
                  v-for="user in availableUsers" 
                  :key="user.id"
                  :value="user.id"
                >
                  {{ user.fullName }} ({{ user.email }})
                </option>
              </select>
              <button 
                @click="assignUserToTeam"
                :disabled="!selectedUserId"
                class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors whitespace-nowrap"
              >
                Assign User
              </button>
            </div>
            <p v-if="availableUsers.length === 0" class="text-sm text-gray-500 mt-2">
              No available users to assign. All users are already assigned to teams.
            </p>
          </div>

          <!-- Close Button -->
          <div class="flex justify-end mt-6">
            <button 
              @click="closeUsersModal"
              class="bg-gray-600 hover:bg-gray-700 text-white px-4 py-2 rounded-lg transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import api from '@/axios';

interface Team {
  id: number;
  name: string;
  description?: string;
  whatsAppPhoneNumberId?: string;
  whatsAppAccessToken?: string;
  whatsAppBusinessAccountId?: string;
  whatsAppPhoneNumber?: string;
  apiVersion?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  userCount?: number;
  contactCount?: number;
  chatCount?: number;
  groupCount?: number;
}

interface TeamUser {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  teamRole?: string;
  isActive: boolean;
  lastLoginAt?: string;
  createdAt: string;
  departmentId?: number;
  depotId?: number;
  departmentName?: string;
  depotName?: string;
  teamName?: string;
}

interface User {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  teamId?: number;
  isActive: boolean;
}

const router = useRouter();
const authStore = useAuthStore();

// State
const teams = ref<Team[]>([]);
const loading = ref(false);
const saving = ref(false);
const showCreateModal = ref(false);
const showEditModal = ref(false);
const showUsersModal = ref(false);
const selectedTeam = ref<Team | null>(null);
const teamUsers = ref<TeamUser[]>([]);
const teamUsersLoading = ref(false);
const availableUsers = ref<User[]>([]);
const selectedUserId = ref('');

// Team Form
const teamForm = ref({
  name: '',
  description: '',
  whatsAppPhoneNumberId: '',
  whatsAppAccessToken: '',
  whatsAppBusinessAccountId: '',
  countryCode: '44',
  whatsAppPhoneNumber: '',
  apiVersion: '18.0',
  isActive: true
});

// FIXED: Allow deletion of all teams (removed isSeededTeam check)
const getDeleteButtonTitle = (team: Team) => {
  if (team.userCount && team.userCount > 0) {
    return `Cannot delete team with ${team.userCount} user(s) assigned`;
  }
  return 'Delete team';
};

// Load teams with proper caching
const loadTeams = async () => {
  loading.value = true;
  try {
    // Clear cache by adding timestamp
    const response = await api.get(`/teams?t=${Date.now()}`);
    console.log('✅ Raw teams response:', response.data);
    
    teams.value = response.data.map((team: any) => ({
      id: team.Id || team.id,
      name: team.Name || team.name || `Team ${team.Id || team.id}`,
      description: team.Description || team.description || 'No description',
      whatsAppPhoneNumberId: team.WhatsAppPhoneNumberId || team.whatsAppPhoneNumberId,
      whatsAppAccessToken: team.WhatsAppAccessToken || team.whatsAppAccessToken,
      whatsAppBusinessAccountId: team.WhatsAppBusinessAccountId || team.whatsAppBusinessAccountId,
      whatsAppPhoneNumber: team.WhatsAppPhoneNumber || team.whatsAppPhoneNumber || 'Not set',
      apiVersion: team.ApiVersion || team.apiVersion || '18.0',
      isActive: team.IsActive ?? team.isActive ?? true,
      createdAt: team.CreatedAt || team.createdAt,
      userCount: team.UserCount || team.userCount || 0,
      contactCount: team.ContactCount || team.contactCount || 0,
      chatCount: team.ChatCount || team.chatCount || 0,
      groupCount: team.GroupCount || team.groupCount || 0
    }));
    
    console.log('✅ Processed teams:', teams.value);
  } catch (error: any) {
    console.error('❌ Error loading teams:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Failed to load teams';
    alert(`Failed to load teams: ${errorMessage}`);
  } finally {
    loading.value = false;
  }
};

// Load available users with proper data mapping
const loadAvailableUsers = async () => {
  try {
    const response = await api.get('/users');
    console.log('📋 Raw users response:', response.data);
    
    // Map users properly
    availableUsers.value = response.data
      .filter((user: any) => {
        // Filter users who are not in the current team or have no team
        const userTeamId = user.TeamId || user.teamId;
        return !userTeamId || userTeamId !== selectedTeam.value?.id;
      })
      .map((user: any) => ({
        id: user.Id || user.id,
        fullName: user.FullName || user.fullName || 'Unknown User',
        email: user.Email || user.email || 'No email',
        phoneNumber: user.PhoneNumber || user.phoneNumber,
        teamId: user.TeamId || user.teamId,
        isActive: user.IsActive ?? user.isActive ?? true
      }));
    
    console.log('✅ Available users:', availableUsers.value);
  } catch (error: any) {
    console.error('Error loading available users:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Failed to load available users';
    alert(`Failed to load available users: ${errorMessage}`);
  }
};

// Load team users with proper error handling
const loadTeamUsers = async (teamId: number) => {
  teamUsersLoading.value = true;
  try {
    console.log(`Loading users for team ${teamId}`);
    const response = await api.get(`/teams/${teamId}/users`);
    
    teamUsers.value = response.data.map((user: any) => ({
      id: user.Id || user.id,
      fullName: user.FullName || user.fullName || 'Unknown User',
      email: user.Email || user.email || 'No email provided',
      phoneNumber: user.PhoneNumber || user.phoneNumber,
      teamRole: user.TeamRole || user.teamRole || 'TeamMember',
      isActive: user.IsActive ?? user.isActive ?? true,
      lastLoginAt: user.LastLoginAt || user.lastLoginAt,
      createdAt: user.CreatedAt || user.createdAt,
      departmentId: user.DepartmentId || user.departmentId,
      depotId: user.DepotId || user.depotId,
      departmentName: user.DepartmentName || user.departmentName,
      depotName: user.DepotName || user.depotName,
      teamName: user.TeamName || user.teamName
    }));
    
    console.log(`✅ Loaded ${teamUsers.value.length} users for team ${teamId}:`, teamUsers.value);
  } catch (error: any) {
    console.error('❌ Error loading team users:', error);
    
    if (error.response) {
      console.error('Response status:', error.response.status);
      console.error('Response data:', error.response.data);
    }
    
    const errorMessage = error.response?.data?.message || error.message || 'Unknown error';
    
    if (error.response?.status === 403) {
      alert('You do not have permission to view users for this team');
    } else if (error.response?.status === 404) {
      alert('Team not found');
    } else if (error.response?.status === 400) {
      alert(`Bad request: ${errorMessage}. Please check if the team exists.`);
    } else {
      alert(`Failed to load team users: ${errorMessage}`);
    }
    
    teamUsers.value = [];
  } finally {
    teamUsersLoading.value = false;
  }
};

// Create or update team
const saveTeam = async () => {
  if (!teamForm.value.name.trim()) {
    alert('Team name is required');
    return;
  }

  saving.value = true;
  try {
    const formatField = (value: string) => value.trim() === '' ? null : value.trim();

    if (showEditModal.value && selectedTeam.value) {
      const updateData: any = {
        name: teamForm.value.name,
        description: formatField(teamForm.value.description),
        whatsAppPhoneNumberId: formatField(teamForm.value.whatsAppPhoneNumberId),
        whatsAppAccessToken: formatField(teamForm.value.whatsAppAccessToken),
        whatsAppBusinessAccountId: formatField(teamForm.value.whatsAppBusinessAccountId),
        whatsAppPhoneNumber: formatField(teamForm.value.whatsAppPhoneNumber),
        apiVersion: formatField(teamForm.value.apiVersion) || '18.0',
        isActive: teamForm.value.isActive
      };

      console.log('🔄 Updating team with data:', updateData);
      await api.put(`/teams/${selectedTeam.value.id}`, updateData);
      alert('Team updated successfully!');
    } else {
      const createData = {
        name: teamForm.value.name,
        description: formatField(teamForm.value.description),
        whatsAppPhoneNumberId: formatField(teamForm.value.whatsAppPhoneNumberId),
        whatsAppAccessToken: formatField(teamForm.value.whatsAppAccessToken),
        whatsAppBusinessAccountId: formatField(teamForm.value.whatsAppBusinessAccountId),
        whatsAppPhoneNumber: formatField(teamForm.value.whatsAppPhoneNumber),
        apiVersion: formatField(teamForm.value.apiVersion) || '18.0'
      };
      
      console.log('➕ Creating team with data:', createData);
      await api.post('/teams', createData);
      alert('Team created successfully!');
    }
    
    closeModal();
    await loadTeams();
  } catch (error: any) {
    console.error('❌ Error saving team:', error);
    
    let errorMessage = 'Failed to save team';
    if (error.response?.data) {
      const errorData = error.response.data;
      errorMessage = errorData.message || errorMessage;
      
      if (errorData.error) {
        errorMessage += `: ${errorData.error}`;
      }
      if (errorData.details) {
        errorMessage += `\n\nDetails: ${errorData.details}`;
      }
    } else {
      errorMessage += `: ${error.message || 'Unknown error'}`;
    }
    
    alert(errorMessage);
  } finally {
    saving.value = false;
  }
};

// Edit team
const editTeam = (team: Team) => {
  selectedTeam.value = team;
  teamForm.value = {
    name: team.name || '',
    description: team.description || '',
    whatsAppPhoneNumberId: team.whatsAppPhoneNumberId || '',
    whatsAppAccessToken: team.whatsAppAccessToken || '',
    whatsAppBusinessAccountId: team.whatsAppBusinessAccountId || '',
    countryCode: team.countryCode || '44',
    whatsAppPhoneNumber: team.whatsAppPhoneNumber || '',
    apiVersion: team.apiVersion || '18.0',
    isActive: team.isActive ?? true
  };
  showEditModal.value = true;
};

// Manage team users
const manageTeamUsers = async (team: Team) => {
  selectedTeam.value = team;
  showUsersModal.value = true;
  await Promise.all([
    loadTeamUsers(team.id),
    loadAvailableUsers()
  ]);
};

// Assign user to team
const assignUserToTeam = async () => {
  if (!selectedTeam.value || !selectedUserId.value) return;

  try {
    console.log(`Assigning user ${selectedUserId.value} to team ${selectedTeam.value.id}`);
    
    await api.post(`/teams/${selectedTeam.value.id}/assign-user`, {
      userId: selectedUserId.value,
      teamRole: 'TeamMember'
    });

    alert('User assigned to team successfully!');
    selectedUserId.value = '';
    
    // Refresh data
    await Promise.all([
      loadTeamUsers(selectedTeam.value.id),
      loadAvailableUsers(),
      loadTeams()
    ]);
  } catch (error: any) {
    console.error('❌ Error assigning user to team:', error);
    
    let errorMessage = 'Failed to assign user';
    if (error.response?.data) {
      errorMessage = error.response.data.message || errorMessage;
      if (error.response.data.error) {
        errorMessage += `: ${error.response.data.error}`;
      }
    } else {
      errorMessage += `: ${error.message || 'Unknown error'}`;
    }
    
    alert(errorMessage);
  }
};

// Remove user from team - FIXED: Properly handle TeamRole
const removeUserFromTeam = async (user: TeamUser) => {
  if (!selectedTeam.value) return;

  if (!confirm(`Are you sure you want to remove ${user.fullName} from this team?`)) {
    return;
  }

  try {
    // FIXED: Set TeamRole to null when removing from team
    await api.put(`/users/${user.id}/assignment`, {
      teamId: null,
      teamRole: null, // This will be set to null on the backend
      departmentId: user.departmentId,
      depotId: user.depotId
    });

    alert('User removed from team successfully!');
    
    // Refresh data
    await Promise.all([
      loadTeamUsers(selectedTeam.value.id),
      loadAvailableUsers(),
      loadTeams()
    ]);
  } catch (error: any) {
    console.error('❌ Error removing user from team:', error);
    
    let errorMessage = 'Failed to remove user';
    if (error.response?.data) {
      errorMessage = error.response.data.message || errorMessage;
      if (error.response.data.error) {
        errorMessage += `: ${error.response.data.error}`;
      }
    } else {
      errorMessage += `: ${error.message || 'Unknown error'}`;
    }
    
    alert(errorMessage);
  }
};

// Delete team - FIXED: Allow deletion of all teams
const deleteTeam = async (team: Team) => {
  if (team.userCount && team.userCount > 0) {
    alert(`Cannot delete team "${team.name}" because it has ${team.userCount} user(s) assigned. Please remove all users first.`);
    return;
  }

  if (!confirm(`Are you sure you want to delete the team "${team.name || 'Unnamed Team'}"? This action cannot be undone.`)) {
    return;
  }

  try {
    console.log(`Deleting team ${team.id}`);
    const response = await api.delete(`/teams/${team.id}`);
    
    if (response.data.message) {
      alert(response.data.message);
    } else {
      alert('Team deleted successfully!');
    }
    
    await loadTeams();
  } catch (error: any) {
    console.error('❌ Error deleting team:', error);
    
    let errorMessage = 'Failed to delete team';
    if (error.response?.data) {
      const errorData = error.response.data;
      errorMessage = errorData.message || errorMessage;
      
      if (errorData.error) {
        errorMessage += `: ${errorData.error}`;
      }
      if (errorData.details) {
        errorMessage += `\n\nDetails: ${errorData.details}`;
      }
    } else {
      errorMessage += `: ${error.message || 'Unknown error'}`;
    }
    
    alert(errorMessage);
  }
};

// Close modals
const closeModal = () => {
  showCreateModal.value = false;
  showEditModal.value = false;
  selectedTeam.value = null;
  teamForm.value = {
    name: '',
    description: '',
    whatsAppPhoneNumberId: '',
    whatsAppAccessToken: '',
    whatsAppBusinessAccountId: '',
    whatsAppPhoneNumber: '',
    apiVersion: '18.0',
    isActive: true
  };
};

const closeUsersModal = () => {
  showUsersModal.value = false;
  selectedTeam.value = null;
  teamUsers.value = [];
  selectedUserId.value = '';
};

// Initialize
onMounted(() => {
  if (!authStore.isAdmin && !authStore.isSuperAdmin) {
    router.push('/home');
    return;
  }
  loadTeams();
});
</script>

<style scoped>
.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.truncate {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>



