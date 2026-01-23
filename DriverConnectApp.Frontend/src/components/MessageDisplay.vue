<template>
  <div :class="['message', message.IsFromDriver ? 'incoming' : 'outgoing']">
    <div class="message-content">
      <!-- Display message content -->
      <p>{{ message.Content }}</p>
      
      <!-- Show template details for template messages -->
      <div v-if="message.MessageType === 'Template'" class="template-info">
        <div class="template-meta">
          <span class="template-badge">📋 Template</span>
          <span class="template-name">{{ message.TemplateName }}</span>
        </div>
        
        <div v-if="parsedParameters && Object.keys(parsedParameters).length > 0" class="template-params">
          <div v-for="(value, key) in parsedParameters" :key="key" class="param">
            <span class="param-key">{{ key }}:</span>
            <span class="param-value">{{ value }}</span>
          </div>
        </div>
      </div>
    </div>
    
    <div class="message-meta">
      <span class="timestamp">{{ formatTime(message.SentAt) }}</span>
      <span v-if="!message.IsFromDriver" class="status">
        <template v-if="message.status === 'sending'">🔄</template>
        <template v-else-if="message.status === 'sent'">✓</template>
        <template v-else-if="message.status === 'delivered'">✓✓</template>
        <template v-else-if="message.status === 'read'">✓✓👁️</template>
        <template v-else-if="message.status === 'failed'">❌</template>
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { MessageDto } from '@/types/conversations'

const props = defineProps<{
  message: MessageDto & { status?: string }
}>()

const parsedParameters = computed(() => {
  try {
    if (!props.message.TemplateParameters) return {}
    if (typeof props.message.TemplateParameters === 'string') {
      return JSON.parse(props.message.TemplateParameters)
    }
    return props.message.TemplateParameters
  } catch {
    return {}
  }
})

const formatTime = (dateString: string) => {
  const date = new Date(dateString)
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}
</script>

<style scoped>
.message {
  margin: 8px 0;
  padding: 8px 12px;
  border-radius: 8px;
  max-width: 80%;
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

.template-info {
  margin-top: 8px;
  padding: 6px;
  background-color: rgba(255, 255, 255, 0.3);
  border-radius: 4px;
  border-left: 3px solid #4CAF50;
}

.template-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
}

.template-badge {
  font-size: 0.8rem;
  background-color: #e8f5e9;
  color: #2e7d32;
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 500;
}

.template-name {
  font-size: 0.85rem;
  color: #555;
  font-weight: 500;
}

.template-params {
  font-size: 0.8rem;
  color: #666;
}

.param {
  display: flex;
  margin: 2px 0;
}

.param-key {
  font-weight: 500;
  min-width: 100px;
  color: #444;
}

.param-value {
  color: #222;
}

.message-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 4px;
  font-size: 0.75rem;
  color: #666;
}
</style>