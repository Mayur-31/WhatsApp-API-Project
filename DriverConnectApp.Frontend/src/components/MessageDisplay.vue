<template>
  <div :class="['message', message.IsFromDriver ? 'incoming' : 'outgoing']">
    <div class="message-content">
      <!-- ✅ Simple: Just display the content backend provides -->
      <p>{{ message.Content }}</p>
      
      <!-- Optional: Template badge for visual distinction -->
      <span v-if="message.IsTemplateMessage" class="template-badge">
        📋 Template
      </span>
    </div>
    
    <div class="message-meta">
      <span class="timestamp">{{ formatTime(message.SentAt) }}</span>
      <span v-if="!message.IsFromDriver" class="status">
        {{ getStatusIcon(message.status) }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps } from 'vue'
import type { MessageDto } from '@/types/conversations'

const props = defineProps<{
  message: MessageDto & { status?: string }
}>()

const formatTime = (dateString: string) => {
  const date = new Date(dateString)
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

const getStatusIcon = (status?: string) => {
  switch (status) {
    case 'sending': return '🔄'
    case 'sent': return '✓'
    case 'delivered': return '✓✓'
    case 'read': return '✓✓👁️'
    case 'failed': return '❌'
    default: return ''
  }
}
</script>

<style scoped>
.message {
  margin: 10px 0;
  padding: 12px 16px;
  border-radius: 18px;
  max-width: 80%;
  word-wrap: break-word;
}

.message.incoming {
  background-color: #f0f0f0;
  align-self: flex-start;
}

.message.outgoing {
  background-color: #dcf8c6;
  align-self: flex-end;
  margin-left: auto;
}

.template-badge {
  display: inline-block;
  margin-top: 6px;
  padding: 3px 8px;
  background-color: #e8f5e9;
  color: #2e7d32;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.message-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 4px;
  font-size: 0.75rem;
  color: #666;
}

.timestamp {
  opacity: 0.8;
}

.status {
  margin-left: 8px;
}
</style>