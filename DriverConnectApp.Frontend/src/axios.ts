import axios from 'axios';

const isDevelopment = import.meta.env.DEV;
const baseURL = isDevelopment ? 'https://localhost:5001/api' : '/api';

console.log('API Base URL:', baseURL);

const api = axios.create({
  baseURL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  },
  timeout: 30000,
});

// Request interceptor
api.interceptors.request.use(
  (config) => {
    console.log('🚀 API Request:', { 
      url: config.url, 
      method: config.method, 
      data: config.data 
    });
    
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    
    return config;
  },
  (error) => {
    console.error('❌ API Request Error:', error);
    return Promise.reject(error);
  }
);

// Response interceptor
api.interceptors.response.use(
  (response) => {
    console.log('✅ API Response:', { 
      url: response.config.url, 
      status: response.status, 
      data: response.data 
    });
    return response;
  },
  (error) => {
    console.error('❌ API Response Error:', { 
      url: error.config?.url, 
      status: error.response?.status, 
      data: error.response?.data,
      message: error.message 
    });
    
    if (error.response?.status === 401) {
      localStorage.removeItem('authToken');
      localStorage.removeItem('user');
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    
    // Handle CORS and network errors
    if (error.code === 'NETWORK_ERROR' || error.message.includes('Network Error')) {
      console.error('Network error - check if server is running and CORS is configured');
    }
    
    return Promise.reject(error);
  }
);

export default api;