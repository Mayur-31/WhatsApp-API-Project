<template>
  <div class="team-selector" v-if="showTeamSelector">
    <label for="team-select" class="team-selector-label">Team:</label>
    <select 
      id="team-select"
      v-model="selectedTeamId" 
      @change="onTeamChange"
      class="team-select"
    >
      <option value="0">All Teams</option>
      <option 
        v-for="team in teams" 
        :key="team.id" 
        :value="team.id"
      >
        {{ team.name }}
      </option>
    </select>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useAuthStore } from '@/stores/auth'
import axios from 'axios'

interface Team {
  id: number
  name: string
  description: string
  whatsAppPhoneNumber: string
  isActive: boolean
  createdAt: string
}

const authStore = useAuthStore()
const teams = ref<Team[]>([])
const selectedTeamId = ref<number>(0)
const showTeamSelector = ref(false)

const loadTeams = async () => {
  try {
    const response = await axios.get('/api/whatsapp/teams')
    teams.value = response.data
  } catch (error) {
    console.error('Failed to load teams:', error)
  }
}

const onTeamChange = () => {
  // Emit event or update global state
  emit('team-change', selectedTeamId.value)
}

const checkUserRole = () => {
  showTeamSelector.value = authStore.user?.roles.includes('Admin') || false
}

onMounted(async () => {
  await loadTeams()
  checkUserRole()
})

// Watch for user changes
watch(() => authStore.user, () => {
  checkUserRole()
})

defineEmits<{
  'team-change': [teamId: number]
}>()
</script>

<style scoped>
.team-selector {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.team-selector-label {
  font-weight: 500;
  color: #374151;
}

.team-select {
  padding: 0.5rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  background-color: white;
  min-width: 150px;
}
</style>