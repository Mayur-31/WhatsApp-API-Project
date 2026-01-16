<template>
  <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-600 to-blue-800 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8 bg-white p-8 rounded-2xl shadow-xl">
      <div class="text-center">
        <h2 class="text-3xl font-bold text-gray-900 mb-2">
          {{ isLoginMode ? 'Sign in to your account' : 'Create your account' }}
        </h2>
        <p class="text-gray-600">
          {{ isLoginMode ? 'Or create a new account' : 'Or sign in to your existing account' }}
        </p>
      </div>

      <form class="mt-8 space-y-6" @submit.prevent="handleSubmit">
        <div v-if="!isLoginMode" class="space-y-4">
          <div>
            <label for="fullName" class="block text-sm font-medium text-gray-700">Full Name</label>
            <input
              id="fullName"
              v-model="form.fullName"
              type="text"
              required
              class="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Enter your full name"
            >
          </div>
        </div>

        <div class="space-y-4">
          <div>
            <label for="email" class="block text-sm font-medium text-gray-700">Email address</label>
            <input
              id="email"
              v-model="form.email"
              type="email"
              required
              class="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Enter your email"
            >
          </div>
          <div>
            <label for="password" class="block text-sm font-medium text-gray-700">Password</label>
            <input
              id="password"
              v-model="form.password"
              type="password"
              required
              class="mt-1 block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              placeholder="Enter your password"
            >
          </div>
        </div>

        <div v-if="isLoginMode" class="flex items-center justify-between">
          <div class="flex items-center">
            <input
              id="remember-me"
              v-model="form.rememberMe"
              type="checkbox"
              class="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
            >
            <label for="remember-me" class="ml-2 block text-sm text-gray-900">Remember me</label>
          </div>
          <button
            type="button"
            @click="showForgotPassword = true"
            class="text-sm font-medium text-blue-600 hover:text-blue-500"
          >
            Forgot your password?
          </button>
        </div>

        <div class="space-y-4">
          <button
            type="submit"
            :disabled="loading"
            class="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
          >
            {{ loading ? 'Please wait...' : (isLoginMode ? 'Sign in' : 'Register') }}
          </button>
          <button
            type="button"
            @click="toggleMode"
            class="w-full text-center text-sm font-medium text-blue-600 hover:text-blue-500"
          >
            {{ isLoginMode ? 'Create a new account' : 'Sign in to your existing account' }}
          </button>
        </div>
      </form>

      <!-- Forgot Password Modal -->
      <div v-if="showForgotPassword" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
        <div class="bg-white rounded-lg max-w-md w-full p-6">
          <h3 class="text-lg font-semibold text-gray-900 mb-4">Reset Password</h3>
          <p class="text-sm text-gray-600 mb-4">Enter your email address and we'll send you a link to reset your password.</p>
          <input
            v-model="forgotPasswordEmail"
            type="email"
            placeholder="Email address"
            class="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-blue-500 focus:border-blue-500 mb-4"
          >
          <div class="flex justify-end space-x-3">
            <button
              @click="showForgotPassword = false"
              class="px-4 py-2 text-sm font-medium text-gray-700 hover:text-gray-900"
            >
              Cancel
            </button>
            <button
              @click="handleForgotPassword"
              :disabled="forgotPasswordLoading"
              class="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 disabled:opacity-50"
            >
              {{ forgotPasswordLoading ? 'Sending...' : 'Send Reset Link' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import api from '@/axios';

const router = useRouter();
const authStore = useAuthStore();

const isLoginMode = ref(true);
const loading = ref(false);
const showForgotPassword = ref(false);
const forgotPasswordEmail = ref('');
const forgotPasswordLoading = ref(false);

const form = reactive({
  fullName: '',
  email: '',
  password: '',
  rememberMe: false
});

const toggleMode = () => {
  isLoginMode.value = !isLoginMode.value;
  form.fullName = '';
  form.email = '';
  form.password = '';
};

const handleSubmit = async () => {
  if (!form.email || !form.password) {
    alert('Please fill in all required fields');
    return;
  }

  loading.value = true;
  try {
    if (isLoginMode.value) {
      const result = await authStore.login({ 
        email: form.email, 
        password: form.password, 
        rememberMe: form.rememberMe 
      });
      
      if (result.success) {
        console.log('✅ Login successful, redirecting to home...');
        
        // Force navigation to home
        await router.push('/home');
        
        // Fallback: if router doesn't work, use window location
        setTimeout(() => {
          if (window.location.pathname === '/login' || window.location.pathname === '/') {
            console.log('Router navigation failed, using window location fallback');
            window.location.href = '/home';
          }
        }, 500);
      } else {
        throw new Error(result.error || 'Login failed');
      }
    } else {
      if (!form.fullName) {
        alert('Full name is required for registration');
        return;
      }

      const result = await authStore.register({ 
        fullName: form.fullName, 
        email: form.email, 
        password: form.password 
      });
      
      if (result.success) {
        // Switch to login mode after successful registration
        isLoginMode.value = true;
        alert('Registration successful! Please login with your credentials.');
        form.email = '';
        form.password = '';
      } else {
        throw new Error(result.error || 'Registration failed');
      }
    }
  } catch (error: any) {
    const errorMessage = error.message || 'Authentication failed';
    alert(errorMessage);
    console.error('Authentication error:', error);
  } finally {
    loading.value = false;
  }
};

const handleForgotPassword = async () => {
  if (!forgotPasswordEmail.value) {
    alert('Please enter your email address');
    return;
  }
  forgotPasswordLoading.value = true;
  try {
    await api.post('/auth/forgot-password', { email: forgotPasswordEmail.value });
    alert('If your email is registered, you will receive a password reset link.');
    showForgotPassword.value = false;
    forgotPasswordEmail.value = '';
  } catch (error: any) {
    const errorMessage = error.response?.data?.message || 'Failed to send reset link';
    alert(errorMessage);
  } finally {
    forgotPasswordLoading.value = false;
  }
};
</script>