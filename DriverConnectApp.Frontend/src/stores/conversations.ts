import { defineStore } from 'pinia'
import { ref, computed, reactive } from 'vue'
import type { ConversationDto, MessageDto } from '@/types/conversations'
import api from '@/axios'

// Extended MessageDto with optimistic fields
interface MessageDtoWithTemp extends MessageDto {
  tempId?: number;
  status?: 'sending' | 'sent' | 'delivered' | 'read' | 'failed';
}

export const useConversationStore = defineStore('conversation', () => {
  const selectedTeamId = ref<number>(0)
  const currentConversations = ref<ConversationDto[]>([])
  const currentMessages = ref<MessageDtoWithTemp[]>([])
  const currentConversationId = ref<number | null>(null)
  const messageSendingQueue = reactive<Map<number, MessageDtoWithTemp>>(new Map())
  const webhookMessages = reactive<Map<string, MessageDtoWithTemp>>(new Map())
  
  const setSelectedTeamId = (teamId: number) => {
    selectedTeamId.value = teamId
  }
  
  const setConversations = (conversations: ConversationDto[]) => {
    currentConversations.value = conversations
  }
  
  const setMessages = (messages: MessageDto[]) => {
    // Sort messages by timestamp to ensure correct order
    currentMessages.value = (messages as MessageDtoWithTemp[]).sort((a, b) => 
      new Date(a.SentAt).getTime() - new Date(b.SentAt).getTime()
    )
  }
  
  const setCurrentConversationId = (conversationId: number) => {
    currentConversationId.value = conversationId
  }
  
  const addMessage = (message: MessageDtoWithTemp) => {
    // ✅ Deduplicate by WhatsAppMessageId (not tempId)
    const existingIndex = currentMessages.value.findIndex(m => 
      m.WhatsAppMessageId === message.WhatsAppMessageId || 
      (m.tempId && m.tempId === message.tempId)
    )
    
    if (existingIndex === -1) {
      currentMessages.value.push(message)
      // Sort after adding
      currentMessages.value.sort((a, b) => 
        new Date(a.SentAt).getTime() - new Date(b.SentAt).getTime()
      )
      
      _logger.log(`✅ Added message: ${message.WhatsAppMessageId}`)
    } else {
      _logger.log(`⚠️ Skipped duplicate: ${message.WhatsAppMessageId}`)
    }
  }
  
  const addMessageOptimistically = (message: MessageDtoWithTemp): number => {
    // Generate a temporary ID for optimistic update
    const tempId = Date.now() + Math.floor(Math.random() * 1000)
    
    const optimisticMessage: MessageDtoWithTemp = {
      ...message,
      tempId,
      status: 'sending',
      SentAt: new Date().toISOString(),
      WhatsAppMessageId: `temp_${tempId}` // Temporary WhatsApp ID
    }
    
    messageSendingQueue.set(tempId, optimisticMessage)
    addMessage(optimisticMessage)
    
    _logger.log(`📤 Optimistic message added: tempId=${tempId}`)
    return tempId
  }
  
  const updateMessageFromWebhook = (webhookMessage: MessageDto) => {
    // ✅ Update by WhatsAppMessageId, not tempId
    const index = currentMessages.value.findIndex(m => 
      m.WhatsAppMessageId === webhookMessage.WhatsAppMessageId ||
      (m.tempId && webhookMessage.WhatsAppMessageId?.includes(`temp_${m.tempId}`))
    )
    
    if (index !== -1) {
      // Update existing message
      const existingMessage = currentMessages.value[index]
      
      // Keep optimistic data if available
      const updatedMessage: MessageDtoWithTemp = {
        ...webhookMessage as MessageDtoWithTemp,
        tempId: existingMessage.tempId, // Preserve tempId
        status: webhookMessage.Status?.toLowerCase() as any || 'sent',
        // Preserve other optimistic fields
        ConversationId: existingMessage.ConversationId || webhookMessage.ConversationId,
        SentAt: webhookMessage.SentAt || existingMessage.SentAt
      }
      
      currentMessages.value[index] = updatedMessage
      _logger.log(`🔄 Updated from webhook: ${webhookMessage.WhatsAppMessageId}`)
    } else {
      // Add new message from webhook
      addMessage(webhookMessage as MessageDtoWithTemp)
      _logger.log(`➕ New from webhook: ${webhookMessage.WhatsAppMessageId}`)
    }
  }
  
  // ✅ NEW: Update message with backend response data
  const updateMessageWithBackendData = (tempId: number, backendMessage: MessageDtoWithTemp) => {
    const index = currentMessages.value.findIndex(m => m.tempId === tempId)
    
    if (index !== -1) {
      // Merge backend data with optimistic message
      const updatedMessage: MessageDtoWithTemp = {
        ...currentMessages.value[index],
        ...backendMessage,
        tempId: undefined, // Clear temp ID
        status: 'sent' as const
      }
      
      currentMessages.value[index] = updatedMessage
      messageSendingQueue.delete(tempId)
      
      _logger.log(`✅ Message updated from backend: tempId=${tempId}, content=${updatedMessage.Content.substring(0, 50)}...`)
    } else {
      _logger.log(`⚠️ Could not find message with tempId=${tempId}`)
      // Add as new message
      addMessage(backendMessage)
    }
  }
  
  const updateMessageStatus = (tempId: number, whatsAppMessageId: string, status: MessageDtoWithTemp['status']) => {
    const index = currentMessages.value.findIndex(m => m.tempId === tempId)
    
    if (index !== -1) {
      const updatedMessage = { 
        ...currentMessages.value[index], 
        WhatsAppMessageId: whatsAppMessageId, 
        status,
        tempId: undefined // Clear tempId once real ID is available
      }
      currentMessages.value[index] = updatedMessage
      messageSendingQueue.delete(tempId)
      _logger.log(`✅ Message status updated: tempId=${tempId} -> wamid=${whatsAppMessageId}, status=${status}`)
    } else {
      // Try to find by WhatsAppMessageId
      const byWamidIndex = currentMessages.value.findIndex(m => m.WhatsAppMessageId === whatsAppMessageId)
      if (byWamidIndex !== -1) {
        currentMessages.value[byWamidIndex].status = status
        _logger.log(`✅ Message status updated by WAMID: ${whatsAppMessageId}, status=${status}`)
      }
    }
  }
  
  const updateConversation = (conversationId: number, updates: Partial<ConversationDto>) => {
    const index = currentConversations.value.findIndex(c => c.Id === conversationId)
    if (index !== -1) {
      currentConversations.value[index] = { ...currentConversations.value[index], ...updates }
    }
  }

  // New method to prevent duplicate sending
  const isMessageSending = (conversationId: number, content: string): boolean => {
    return Array.from(messageSendingQueue.values()).some(msg => 
      msg.ConversationId === conversationId && 
      msg.Content === content && 
      msg.status === 'sending'
    )
  }
  
  // Helper to simulate console logging
  const _logger = {
    log: (message: string) => {
      console.log(`[ConversationStore] ${message}`)
    }
  }

  return {
    selectedTeamId,
    currentConversations,
    currentMessages,
    currentConversationId,
    messageSendingQueue,
    setSelectedTeamId,
    setConversations,
    setMessages,
    setCurrentConversationId,
    addMessage,
    addMessageOptimistically,
    updateMessageFromWebhook,
    updateMessageWithBackendData, // ✅ NEW
    updateMessageStatus,
    updateConversation,
    isMessageSending
  }
})