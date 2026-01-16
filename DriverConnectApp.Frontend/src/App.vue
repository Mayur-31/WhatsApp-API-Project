<template>
  <div class="min-h-screen bg-gray-100">
    <!-- Navigation Header -->
    <nav v-if="isAuthenticated" class="bg-green-600 text-white shadow-lg">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center py-4">
          <div class="flex items-center space-x-4">
            <h1 class="text-xl font-bold">DriverConnect</h1>
            <router-link 
              to="/home" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/home' }"
            >
              Chat
            </router-link>
            <router-link 
              to="/departments" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/departments' }"
            >
              Departments
            </router-link>
            <router-link 
              to="/depots" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/depots' }"
            >
              Depots
            </router-link>
            <router-link 
              to="/users" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/users' }"
            >
              Users
            </router-link>
          </div>
          <div class="flex items-center space-x-4">
            <span class="text-sm">Welcome, {{ user?.fullName }}</span>
            <button 
              @click="logout" 
              class="bg-red-500 hover:bg-red-600 px-3 py-2 rounded text-sm transition-colors"
            >
              Logout
            </button>
          </div>
        </div>
      </div>
    </nav>

    <!-- Main Content -->
    <main>
      <router-view />
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useAuthStore } from './stores/auth';

const authStore = useAuthStore();
const isAuthenticated = computed(() => authStore.isAuthenticated);
const user = computed(() => authStore.user);

const logout = () => authStore.logout();
</script>