<template>
  <div class="media-gallery">
    <!-- Header -->
    <div class="bg-green-600 text-white p-4 shadow-lg">
      <div class="flex items-center space-x-4">
        <button @click="$emit('close')" class="text-white hover:text-green-200 transition-colors">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
          </svg>
        </button>
        <div>
          <h1 class="text-xl font-semibold">{{ conversationName }}</h1>
          <p class="text-green-100 text-sm">{{ getMediaCountText() }}</p>
        </div>
      </div>
    </div>

    <!-- Tabs -->
    <div class="bg-white border-b sticky top-0 z-10">
      <div class="flex overflow-x-auto">
        <button 
          v-for="tab in tabs" 
          :key="tab.id"
          @click="activeTab = tab.id"
          :class="[
            'flex-1 px-4 py-3 text-sm font-medium whitespace-nowrap transition-colors',
            activeTab === tab.id 
              ? 'text-green-600 border-b-2 border-green-600' 
              : 'text-gray-500 hover:text-gray-700'
          ]"
        >
          {{ tab.name }} ({{ getTabCount(tab.id) }})
        </button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex items-center justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-green-600"></div>
      <span class="ml-2 text-gray-600">Loading media...</span>
    </div>

    <!-- Content -->
    <div v-else class="p-4">
      <!-- Media Grid -->
      <div v-if="activeTab === 'media'" class="grid grid-cols-3 gap-2">
        <div 
          v-for="image in mediaData.Images" 
          :key="image.Id"
          @click="openMediaViewer('image', image)"
          class="aspect-square bg-gray-200 rounded-lg overflow-hidden cursor-pointer hover:opacity-90 transition-opacity"
        >
          <img 
            :src="getMediaUrl(image.Url)" 
            :alt="image.Description || 'Image'"
            class="w-full h-full object-cover"
            loading="lazy"
          />
        </div>
        <div 
          v-for="video in mediaData.Videos" 
          :key="video.Id"
          @click="openMediaViewer('video', video)"
          class="aspect-square bg-gray-800 rounded-lg overflow-hidden cursor-pointer hover:opacity-90 transition-opacity relative"
        >
          <img 
            v-if="video.ThumbnailUrl"
            :src="getMediaUrl(video.ThumbnailUrl)" 
            :alt="video.Description || 'Video'"
            class="w-full h-full object-cover opacity-70"
          />
          <div v-else class="w-full h-full bg-gray-700 flex items-center justify-center">
            <span class="text-white text-2xl">🎥</span>
          </div>
          <div class="absolute inset-0 flex items-center justify-center">
            <div class="w-12 h-12 bg-black bg-opacity-50 rounded-full flex items-center justify-center">
              <span class="text-white text-lg">▶️</span>
            </div>
          </div>
          <div class="absolute bottom-2 right-2 bg-black bg-opacity-70 text-white text-xs px-1 rounded">
            {{ getVideoDuration(video) }}
          </div>
        </div>
      </div>

      <!-- Documents List -->
      <div v-if="activeTab === 'documents'" class="space-y-2">
        <div 
          v-for="doc in mediaData.Documents" 
          :key="doc.Id"
          @click="openDocument(doc)"
          class="flex items-center space-x-3 p-3 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer transition-colors"
        >
          <div class="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center">
            <span class="text-2xl">{{ getDocumentIcon(doc.MimeType) }}</span>
          </div>
          <div class="flex-1 min-w-0">
            <p class="font-medium text-gray-900 truncate">{{ doc.FileName || 'Document' }}</p>
            <p class="text-sm text-gray-500 flex items-center space-x-2">
              <span>{{ formatFileSize(doc.FileSize) }}</span>
              <span>•</span>
              <span>{{ formatDate(doc.SentAt) }}</span>
            </p>
            <p v-if="doc.Description" class="text-sm text-gray-600 truncate">{{ doc.Description }}</p>
          </div>
          <div class="text-right">
            <p class="text-xs text-gray-500">{{ doc.SenderName }}</p>
            <button 
              @click.stop="downloadFile(doc)"
              class="mt-1 text-green-600 hover:text-green-700 text-sm font-medium"
            >
              Download
            </button>
          </div>
        </div>
      </div>

      <!-- Links List -->
      <div v-if="activeTab === 'links'" class="space-y-3">
        <div 
          v-for="link in mediaData.Links" 
          :key="link.Id"
          @click="openLink(link.Url!)"
          class="p-4 bg-white border border-gray-200 rounded-lg hover:shadow-md cursor-pointer transition-shadow"
        >
          <div class="flex items-start space-x-3">
            <div class="w-16 h-16 bg-blue-100 rounded-lg flex items-center justify-center flex-shrink-0">
              <span class="text-2xl">🔗</span>
            </div>
            <div class="flex-1 min-w-0">
              <p class="font-medium text-gray-900 mb-1">{{ link.Title }}</p>
              <p class="text-sm text-blue-600 truncate mb-2">{{ link.Url }}</p>
              <p v-if="link.Description" class="text-sm text-gray-600 line-clamp-2">{{ link.Description }}</p>
              <div class="flex items-center space-x-2 mt-2 text-xs text-gray-500">
                <span>{{ formatDate(link.SentAt) }}</span>
                <span>•</span>
                <span>{{ link.SenderName }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="getTabCount(activeTab) === 0" class="text-center py-12 text-gray-500">
        <div class="text-6xl mb-4">📁</div>
        <h3 class="text-lg font-semibold mb-2">No {{ getTabName(activeTab) }} found</h3>
        <p>No {{ getTabName(activeTab) }} have been shared in this conversation yet.</p>
      </div>
    </div>

    <!-- Media Viewer Modal -->
    <div v-if="showMediaViewer && selectedMedia" class="fixed inset-0 bg-black bg-opacity-90 z-50 flex items-center justify-center">
      <div class="max-w-4xl max-h-full w-full p-4">
        <!-- Image Viewer -->
        <div v-if="selectedMedia.Type === 'image'" class="text-center">
          <img 
            :src="getMediaUrl(selectedMedia.Url!)" 
            :alt="selectedMedia.Description || 'Image'"
            class="max-w-full max-h-[80vh] object-contain mx-auto"
          />
        </div>
        
        <!-- Video Viewer -->
        <div v-else-if="selectedMedia.Type === 'video'" class="text-center">
          <video 
            :src="getMediaUrl(selectedMedia.Url!)" 
            controls 
            class="max-w-full max-h-[80vh] mx-auto"
          >
            Your browser does not support the video tag.
          </video>
        </div>

        <!-- Media Info -->
        <div class="mt-4 bg-white rounded-lg p-4 max-w-2xl mx-auto">
          <p class="text-sm text-gray-600 mb-2">{{ selectedMedia.SenderName }} • {{ formatDate(selectedMedia.SentAt) }}</p>
          <p v-if="selectedMedia.Description" class="text-gray-800">{{ selectedMedia.Description }}</p>
          <div class="flex justify-center space-x-4 mt-4">
            <button 
              @click="downloadFile(selectedMedia)"
              class="bg-green-600 text-white px-4 py-2 rounded-lg hover:bg-green-700 transition-colors"
            >
              Download
            </button>
            <button 
              @click="showMediaViewer = false"
              class="bg-gray-600 text-white px-4 py-2 rounded-lg hover:bg-gray-700 transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      </div>
      
      <!-- Close Button -->
      <button 
        @click="showMediaViewer = false"
        class="absolute top-4 right-4 text-white text-2xl bg-black bg-opacity-50 rounded-full w-10 h-10 flex items-center justify-center hover:bg-opacity-70 transition-colors"
      >
        ×
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import api from '@/axios';
import type { ConversationMediaResponse, MediaItemDto } from '@/types/conversations';

interface Props {
  conversationId: number;
  conversationName: string;
  isGroup: boolean;
}

interface Emits {
  (e: 'close'): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

const loading = ref(true);
const mediaData = ref<ConversationMediaResponse>({
  Images: [],
  Videos: [],
  Documents: [],
  Links: [],
  TotalItems: 0,
  ConversationName: '',
  IsGroupConversation: false
});

const activeTab = ref('media'); // 'media', 'documents', 'links'
const showMediaViewer = ref(false);
const selectedMedia = ref<MediaItemDto | null>(null);

const tabs = [
  { id: 'media', name: 'Media' },
  { id: 'documents', name: 'Documents' },
  { id: 'links', name: 'Links' }
];

// Computed properties
const getMediaCountText = () => {
  const total = mediaData.value.TotalItems;
  if (total === 0) return 'No media shared';
  if (total === 1) return '1 item';
  return `${total} items`;
};

const getTabCount = (tabId: string) => {
  switch (tabId) {
    case 'media': return mediaData.value.Images.length + mediaData.value.Videos.length;
    case 'documents': return mediaData.value.Documents.length;
    case 'links': return mediaData.value.Links.length;
    default: return 0;
  }
};

const getTabName = (tabId: string) => {
  const tab = tabs.find(t => t.id === tabId);
  return tab ? tab.name.toLowerCase() : '';
};

// Methods
const loadMediaData = async () => {
  try {
    loading.value = true;
    const response = await api.get(`/conversations/${props.conversationId}/media`);
    mediaData.value = response.data;
  } catch (error: any) {
    console.error('Error loading media data:', error);
    alert(`Failed to load media: ${error.response?.data?.message || error.message}`);
  } finally {
    loading.value = false;
  }
};

const getMediaUrl = (url: string | undefined): string => {
  if (!url) return '';
  
  if (url.startsWith('http://') || url.startsWith('https://')) {
    return url;
  }
  
  const cleanUrl = url.replace(/^\/+/, '');
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5001';
  return `${baseUrl}/${cleanUrl}`;
};

const openMediaViewer = (type: string, media: MediaItemDto) => {
  selectedMedia.value = media;
  showMediaViewer.value = true;
};

const openDocument = (doc: MediaItemDto) => {
  if (doc.Url) {
    window.open(getMediaUrl(doc.Url), '_blank');
  }
};

const openLink = (url: string) => {
  window.open(url, '_blank');
};

const downloadFile = (item: MediaItemDto) => {
  if (item.Url) {
    const link = document.createElement('a');
    link.href = getMediaUrl(item.Url);
    link.download = item.FileName || `download_${item.Id}`;
    link.target = '_blank';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
};

const getDocumentIcon = (mimeType: string | undefined): string => {
  if (!mimeType) return '📄';
  
  if (mimeType.includes('pdf')) return '📕';
  if (mimeType.includes('word') || mimeType.includes('document')) return '📘';
  if (mimeType.includes('excel') || mimeType.includes('spreadsheet')) return '📗';
  if (mimeType.includes('powerpoint') || mimeType.includes('presentation')) return '📙';
  if (mimeType.includes('audio')) return '🎵';
  if (mimeType.includes('zip') || mimeType.includes('rar') || mimeType.includes('archive')) return '📦';
  
  return '📄';
};

const getVideoDuration = (video: MediaItemDto): string => {
  // In a real implementation, you would extract duration from video metadata
  return video.Duration || '--:--';
};

const formatFileSize = (bytes: number | undefined): string => {
  if (!bytes) return 'Unknown size';
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

const formatDate = (dateString: string): string => {
  const date = new Date(dateString);
  const now = new Date();
  const diffTime = Math.abs(now.getTime() - date.getTime());
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
  
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
};

// Lifecycle
onMounted(() => {
  loadMediaData();
});
</script>

<style scoped>
.media-gallery {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: white;
  z-index: 40;
  display: flex;
  flex-direction: column;
}

.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* Smooth scrolling */
.media-gallery {
  overflow-y: auto;
}

/* Custom scrollbar */
.media-gallery::-webkit-scrollbar {
  width: 6px;
}

.media-gallery::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.media-gallery::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 3px;
}

.media-gallery::-webkit-scrollbar-thumb:hover {
  background: #a8a8a8;
}
</style>