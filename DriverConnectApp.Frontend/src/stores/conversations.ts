import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { ConversationDto, MessageDto } from '@/types/conversations'

export const useConversationStore = defineStore('conversation', () => {
  const selectedTeamId = ref<number>(0)
  const currentConversations = ref<ConversationDto[]>([])
  const currentMessages = ref<MessageDto[]>([])
  
  const setSelectedTeamId = (teamId: number) => {
    selectedTeamId.value = teamId
  }
  
  const setConversations = (conversations: ConversationDto[]) => {
    currentConversations.value = conversations
  }
  
  const setMessages = (messages: MessageDto[]) => {
    currentMessages.value = messages
  }
  
  const addMessage = (message: MessageDto) => {
    currentMessages.value.push(message)
  }
  
  const updateConversation = (conversationId: number, updates: Partial<ConversationDto>) => {
    const index = currentConversations.value.findIndex(c => c.Id === conversationId)
    if (index !== -1) {
      currentConversations.value[index] = { ...currentConversations.value[index], ...updates }
    }
  }

  return {
    selectedTeamId,
    currentConversations,
    currentMessages,
    setSelectedTeamId,
    setConversations,
    setMessages,
    addMessage,
    updateConversation
  }
})