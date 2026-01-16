import { createRouter, createWebHistory } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { 
      path: '/', 
      redirect: '/home' 
    },
    { 
      path: '/home', 
      name: 'Home', 
      component: () => import('@/views/HomeView.vue'), 
      meta: { requiresAuth: true } 
    },
    { 
      path: '/login', 
      name: 'Login', 
      component: () => import('@/views/LoginView.vue'), 
      meta: { requiresAuth: false } 
    },
    { 
      path: '/reset-password', 
      name: 'ResetPassword', 
      component: () => import('@/views/ResetPasswordView.vue'), 
      meta: { requiresAuth: false } 
    },
    { 
      path: '/departments', 
      name: 'Departments', 
      component: () => import('@/views/DepartmentsView.vue'), 
      meta: { requiresAuth: true, requiresAdminOrManager: true } 
    },
    { 
      path: '/depots', 
      name: 'Depots', 
      component: () => import('@/views/DepotsView.vue'), 
      meta: { requiresAuth: true, requiresAdminOrManager: true } 
    },
    { 
      path: '/users', 
      name: 'Users', 
      component: () => import('@/views/UsersView.vue'), 
      meta: { requiresAuth: true, requiresAdminOrManager: true } 
    },
    { 
      path: '/teams', 
      name: 'Teams', 
      component: () => import('@/views/TeamsView.vue'), 
      meta: { requiresAuth: true, requiresAdmin: true } 
    },
    { 
      path: '/:pathMatch(.*)*', 
      redirect: '/home' 
    },
  ]
});

router.beforeEach((to, from, next) => {
  const authStore = useAuthStore();
  
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next('/login');
  } else if (to.meta.requiresAdminOrManager && !authStore.isAdminOrManager) {
    next('/home');
  } else if (to.meta.requiresAdmin && !authStore.isAdmin) {
    next('/home');
  } else if (to.path === '/login' && authStore.isAuthenticated) {
    next('/home');
  } else {
    next();
  }
});

export default router;