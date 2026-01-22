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
  const templateContent = `Template: ${templateName.value}`
  return conversationStore.isMessageSending(props.conversationId, templateContent)
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
  
  try {
    const templateParams: Record<string, string> = {}
    templateParameters.value.forEach(param => {
      if (param.value.trim()) {
        templateParams[param.name] = param.value.trim()
      }
    })

    // Generate a temporary message content for display
    const templateContent = `Template: ${templateName.value}` + 
      (Object.keys(templateParams).length > 0 ? 
        ` (${Object.values(templateParams).join(', ')})` : '')

    // Create optimistic message
    const optimisticMessage = {
      Content: templateContent,
      MessageType: 'Template',
      isFromDriver: false,
      ConversationId: props.conversationId,
      TeamId: props.teamId,
      PhoneNumber: props.phoneNumber,
      IsTemplateMessage: true,
      TemplateName: templateName.value,
      TemplateParameters: templateParams,
      status: 'sending' as const,
      SenderName: 'You',
      SenderPhoneNumber: 'Staff',
      IsGroupMessage: false
    }

    // Add optimistic update to store
    const tempId = conversationStore.addMessageOptimistically(optimisticMessage)

    // Send via messages endpoint
    const response = await api.post('/messages', {
      content: templateContent,
      messageType: 'Template', // ✅ FIXED: Use Template type, not Text
      isFromDriver: false,
      conversationId: props.conversationId,
      teamId: props.teamId,
      isTemplateMessage: true,
      templateName: templateName.value,
      templateParameters: templateParams,
      phoneNumber: props.phoneNumber
    })
    
    if (response.data && response.data.Id) {
      // Update message status with real ID
      conversationStore.updateMessageStatus(
        tempId, 
        response.data.WhatsAppMessageId || `server_${response.data.Id}`,
        'sent'
      )
      
      emit('sent', response.data)
      close()
    } else {
      throw new Error('Invalid response from server')
    }
    
  } catch (error: any) {
    console.error('Failed to send template:', error)
    
    // Update status to indicate failure
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