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
            <div class="mt-2 text-xs text-gray-500 space-y-1">
              <p>Password must:</p>
              <ul class="list-disc pl-4">
                <li>Be at least 6 characters</li>
                <li>Contain at least one uppercase letter</li>
                <li>Contain at least one lowercase letter</li>
                <li>Contain at least one number</li>
              </ul>
            </div>
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

        <!-- Error Display -->
        <div v-if="error" class="rounded-md bg-red-50 p-4">
          <div class="flex">
            <div class="flex-shrink-0">
              <svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
              </svg>
            </div>
            <div class="ml-3">
              <h3 class="text-sm font-medium text-red-800">{{ error }}</h3>
            </div>
          </div>
        </div>

        <!-- Success Message -->
        <div v-if="success" class="rounded-md bg-green-50 p-4">
          <div class="flex">
            <div class="flex-shrink-0">
              <svg class="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
              </svg>
            </div>
            <div class="ml-3">
              <h3 class="text-sm font-medium text-green-800">{{ success }}</h3>
            </div>
          </div>
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="w-full flex justify-center py-3 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <span v-if="loading">
            <svg class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Resetting Password...
          </span>
          <span v-else>Reset Password</span>
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
const success = ref('');

const form = reactive({
  email: '',
  newPassword: '',
  confirmPassword: '',
  token: ''
});

onMounted(() => {
  // Get parameters from URL - they are already URL-encoded
  form.token = route.query.token as string || '';
  form.email = route.query.email as string || '';
  
  // Validate required parameters
  if (!form.token || !form.email) {
    error.value = 'Invalid or missing reset link parameters. Please request a new password reset.';
  }
});

const validatePassword = (password: string): string => {
  if (password.length < 6) {
    return 'Password must be at least 6 characters long';
  }
  
  const hasUpperCase = /[A-Z]/.test(password);
  const hasLowerCase = /[a-z]/.test(password);
  const hasNumber = /[0-9]/.test(password);
  
  if (!hasUpperCase || !hasLowerCase || !hasNumber) {
    return 'Password must contain at least one uppercase letter, one lowercase letter, and one number';
  }
  
  return '';
};

const handleResetPassword = async () => {
  // Clear previous messages
  error.value = '';
  success.value = '';
  
  // Validate inputs
  if (!form.token || !form.email) {
    error.value = 'Missing required information. Please use the reset link from your email.';
    return;
  }
  
  if (!form.newPassword || !form.confirmPassword) {
    error.value = 'Please enter and confirm your new password';
    return;
  }
  
  if (form.newPassword !== form.confirmPassword) {
    error.value = 'Passwords do not match';
    return;
  }
  
  const passwordError = validatePassword(form.newPassword);
  if (passwordError) {
    error.value = passwordError;
    return;
  }
  
  loading.value = true;
  
  try {
    const response = await api.post('/auth/reset-password', {
      email: form.email,
      token: form.token, // Send as-is (already URL-encoded)
      newPassword: form.newPassword,
      confirmPassword: form.confirmPassword
    });
    
    if (response.data.success) {
      success.value = '✅ Password reset successfully! You can now login with your new password.';
      
      // Redirect to login after 3 seconds
      setTimeout(() => {
        router.push('/login');
      }, 3000);
    } else {
      error.value = response.data.message || 'Failed to reset password';
    }
  } catch (err: any) {
    console.error('Password reset error:', err);
    
    if (err.response?.status === 400) {
      error.value = err.response.data?.message || 'Invalid reset request. Please check your information.';
    } else if (err.response?.status === 401) {
      error.value = 'Invalid or expired reset link. Please request a new password reset.';
    } else if (err.response?.status === 429) {
      error.value = 'Too many attempts. Please try again later.';
    } else {
      error.value = 'An error occurred. Please try again or contact support.';
    }
  } finally {
    loading.value = false;
  }
};
</script>