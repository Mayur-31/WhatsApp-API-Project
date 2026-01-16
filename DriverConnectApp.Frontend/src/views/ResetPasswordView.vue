<template>
  <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-600 to-blue-800 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8 bg-white p-8 rounded-2xl shadow-xl">
      <div class="text-center">
        <h2 class="text-3xl font-bold text-gray-900 mb-2">Reset Your Password</h2>
        <p class="text-gray-600">Enter your new password below</p>
      </div>

      <form class="mt-8 space-y-6" @submit.prevent="handleResetPassword">
        <div class="space-y-4">
          <div>
            <label for="email" class="block text-sm font-medium text-gray-700">Email address</label>
            <input
              id="email"
              v-model="form.email"
              type="email"
              required
              readonly
              class="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm bg-gray-50 focus:outline-none focus:ring-blue-500 focus:border-blue-500"
            >
          </div>
          <div>
            <label for="newPassword" class="block text-sm font-medium text-gray-700">New Password</label>
            <input
              id="newPassword"
              v-model="form.newPassword"
              type="password"
              required
              class="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Enter new password"
            >
          </div>
          <div>
            <label for="confirmPassword" class="block text-sm font-medium text-gray-700">Confirm Password</label>
            <input
              id="confirmPassword"
              v-model="form.confirmPassword"
              type="password"
              required
              class="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Confirm new password"
            >
          </div>
        </div>

        <div v-if="error" class="text-red-600 text-sm text-center bg-red-50 py-2 px-3 rounded">
          {{ error }}
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
        >
          {{ loading ? 'Resetting Password...' : 'Reset Password' }}
        </button>

        <div class="text-center">
          <router-link to="/login" class="text-sm font-medium text-blue-600 hover:text-blue-500">
            Back to Login
          </router-link>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import api from '@/axios';

const router = useRouter();
const route = useRoute();

const loading = ref(false);
const error = ref('');

const form = reactive({
  email: '',
  newPassword: '',
  confirmPassword: '',
  token: ''
});

onMounted(() => {
  form.token = route.query.token as string;
  form.email = route.query.email as string;
  if (!form.token || !form.email) {
    error.value = 'Invalid reset link. Please request a new password reset.';
  }
});

const handleResetPassword = async () => {
  if (form.newPassword !== form.confirmPassword) {
    error.value = 'Passwords do not match';
    return;
  }
  if (form.newPassword.length < 6) {
    error.value = 'Password must be at least 6 characters long';
    return;
  }
  loading.value = true;
  error.value = '';
  try {
    await api.post('/auth/reset-password', {
      email: form.email,
      token: form.token,
      newPassword: form.newPassword,
      confirmPassword: form.confirmPassword
    });
    alert('Password has been reset successfully. You can now login with your new password.');
    router.push('/login');
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to reset password. Please try again.';
  } finally {
    loading.value = false;
  }
};
</script>