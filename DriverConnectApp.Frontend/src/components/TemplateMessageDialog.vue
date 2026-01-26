<template>
  <div v-if="show" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
    <div class="bg-white rounded-lg max-w-4xl w-full p-8 max-h-[90vh] overflow-y-auto">
      <h2 class="text-3xl font-bold mb-6">Send Template Message</h2>
      
      <div class="space-y-4">
        <div>
          <label class="block text-lg font-medium text-gray-700 mb-2">Template Name *</label>
          <select 
            v-model="templateName" 
            class="w-full border rounded-lg p-3 text-lg" 
            required 
            @change="updateParameters"
            :disabled="sending"
          >
            <option value="">Select a template</option>
            <option value="hello_world">Hello World</option>
            <option value="order_confirmation">Order Confirmation</option>
            <option value="delivery_update">Delivery Update</option>
            <option value="welcome_message">Welcome Message</option>
            <option value="payment_reminder">Payment Reminder</option>
            <option value="service_update">Service Update</option>
            <option value="follow_up">Follow Up</option>
          </select>
        </div>
        
        <div v-if="templateParameters.length > 0">
          <h3 class="text-sm font-medium text-gray-700 mb-2">Template Parameters</h3>
          <div v-for="(param, index) in templateParameters" :key="index" class="mb-2">
            <label class="block text-lg font-medium text-gray-700 mb-2">{{ param.displayName }}</label>
            <input 
              v-model="param.value"
              :placeholder="`Enter ${param.displayName}`"
              class="w-full border rounded-lg p-3 text-lg"
              required
              :disabled="sending"
            />
          </div>
        </div>

        <div v-else-if="templateName">
          <p class="text-sm text-gray-600">This template doesn't require parameters.</p>
        </div>
        
        <div v-if="errorMessage" class="bg-red-50 border border-red-200 rounded p-3">
          <p class="text-sm text-red-800">{{ errorMessage }}</p>
        </div>
        
        <div class="bg-blue-50 border border-blue-200 rounded p-3">
          <p class="text-sm text-blue-800">
            <span class="font-medium">Note:</span> Template messages can be sent to any contact, even if they haven't messaged in the last 24 hours.
          </p>
        </div>
        
        <div class="flex justify-end space-x-3 mt-6">
          <button 
            @click="close" 
            class="px-6 py-3 text-lg font-medium text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors"
            :disabled="sending"
          >
            Cancel
          </button>
          <button 
            @click="send" 
            :disabled="!templateName || sending || isDuplicateSending"
            class="px-6 py-3 text-lg font-medium bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:bg-gray-400 disabled:cursor-not-allowed shadow-md"
          >
            {{ sending ? 'Sending...' : 'Send Template' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import api from '@/axios'
import { useConversationStore } from '@/stores/conversations'

interface TemplateParameter {
  name: string;
  displayName: string;
  value: string;
}

const props = defineProps<{
  show: boolean;
  conversationId: number;
  phoneNumber: string;
  teamId: number;
}>()

const emit = defineEmits<{
  close: [];
  sent: [message: any];
}>()

const templateName = ref('')
const templateParameters = ref<TemplateParameter[]>([])
const sending = ref(false)
const errorMessage = ref<string>('')
const conversationStore = useConversationStore()

// Define template parameters based on template name
const templateConfigs: Record<string, { name: string; displayName: string }[]> = {
  hello_world: [],
  order_confirmation: [
    { name: 'order_number', displayName: 'Order Number' },
    { name: 'customer_name', displayName: 'Customer Name' },
    { name: 'delivery_date', displayName: 'Delivery Date' }
  ],
  delivery_update: [
    { name: 'tracking_number', displayName: 'Tracking Number' },
    { name: 'estimated_delivery', displayName: 'Estimated Delivery Time' },
    { name: 'driver_name', displayName: 'Driver Name' }
  ],
  welcome_message: [
    { name: 'company_name', displayName: 'Company Name' },
    { name: 'contact_person', displayName: 'Contact Person' }
  ],
  payment_reminder: [
    { name: 'invoice_number', displayName: 'Invoice Number' },
    { name: 'due_date', displayName: 'Due Date' },
    { name: 'amount', displayName: 'Amount' }
  ],
  service_update: [
    { name: 'service_type', displayName: 'Service Type' },
    { name: 'update_details', displayName: 'Update Details' },
    { name: 'next_steps', displayName: 'Next Steps' }
  ],
  follow_up: [
    { name: 'last_contact', displayName: 'Last Contact Date' },
    { name: 'reason', displayName: 'Follow-up Reason' }
  ]
}

// Check if the same message is already being sent
const isDuplicateSending = computed(() => {
  const templateKey = `template_${templateName.value}`
  return conversationStore.isMessageSending(props.conversationId, templateKey)
})

const updateParameters = () => {
  if (templateName.value in templateConfigs) {
    const config = templateConfigs[templateName.value]
    templateParameters.value = config.map(param => ({
      ...param,
      value: ''
    }))
  } else {
    templateParameters.value = []
  }
}

watch(templateName, () => {
  errorMessage.value = ''
  updateParameters()
})

// Get driver ID from conversation
const getDriverIdFromConversation = async (): Promise<number> => {
  try {
    // Try to get from conversation store first
    const conversation = conversationStore.currentConversations.find(
      c => c.Id === props.conversationId
    )
    
    if (conversation && conversation.DriverId) {
      return conversation.DriverId
    }
    
    // If not found, fetch from API
    const response = await api.get(`/conversations/${props.conversationId}`)
    if (response.data && response.data.DriverId) {
      return response.data.DriverId
    }
    
    throw new Error('Could not find driver ID for this conversation')
  } catch (error) {
    console.error('Error getting driver ID:', error)
    throw error
  }
}

const send = async () => {
  if (!templateName.value || !props.phoneNumber || !props.teamId || !props.conversationId) {
    errorMessage.value = 'Please fill all required fields'
    return
  }

  if (isDuplicateSending.value) {
    errorMessage.value = 'This message is already being sent. Please wait.'
    return
  }

  sending.value = true
  errorMessage.value = ''
  
  let tempId: number | undefined
  
  try {
    const templateParams: Record<string, string> = {}
    templateParameters.value.forEach(param => {
      if (param.value.trim()) {
        templateParams[param.name] = param.value.trim()
      }
    })

    // Create a temporary placeholder (will be replaced by actual content)
    const optimisticMessage = {
      Id: 0,
      Content: `📋 Sending template: ${templateName.value}...`,
      MessageType: 'Text',
      IsFromDriver: false,
      ConversationId: props.conversationId,
      TeamId: props.teamId,
      PhoneNumber: props.phoneNumber,
      IsTemplateMessage: true,
      TemplateName: templateName.value,
      TemplateParameters: templateParams,
      Status: 'sending',
      SenderName: 'You',
      SenderPhoneNumber: 'Staff',
      IsGroupMessage: false,
      SentAt: new Date().toISOString(),
      FormattedDate: new Date().toLocaleDateString(),
      FormattedTime: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      WhatsAppMessageId: '',
      status: 'sending' as const
    }

    // Add optimistic update to store
    tempId = conversationStore.addMessageOptimistically(optimisticMessage as any)

    // Get driver ID
    const driverId = await getDriverIdFromConversation()
    
    if (!driverId) {
      throw new Error('Could not find driver ID for this conversation')
    }


    // ✅ Use the CORRECT endpoint for template messages
    const response = await api.post('/messages/send-template', {
      driverId: driverId,
      templateName: templateName.value,
      templateParameters: templateParams,
      teamId: props.teamId,
      languageCode: 'en_US'
    })
    
    if (response.data && response.data.messageId) {
      // ✅ Use the actual content returned by backend
      const actualContent = response.data.displayContent || 
                           response.data.actualContent || 
                           `Template: ${templateName.value}`;
      
      // Update message with real content from backend
      conversationStore.updateMessageWithBackendData(tempId, {
        ...optimisticMessage,
        Id: response.data.messageId,
        Content: actualContent, // ✅ Now this will show actual template content
        WhatsAppMessageId: response.data.whatsAppMessageId || `template_${response.data.messageId}`,
        Status: 'sent',
        status: 'sent' as const,
        tempId: undefined
      })
      
      emit('sent', response.data)
      close()
    } else {
      throw new Error('Invalid response from server')
    }
    
  } catch (error: any) {
    console.error('Failed to send template:', error)
    
    if (tempId) {
      conversationStore.updateMessageStatus(tempId, '', 'failed')
    }
    
    errorMessage.value = `Failed to send template message: ${error.response?.data?.error || error.message || 'Unknown error'}`
  } finally {
    sending.value = false
  }
}

const close = () => {
  templateName.value = ''
  templateParameters.value = []
  errorMessage.value = ''
  emit('close')
}

defineExpose({
  close
})
</script>
