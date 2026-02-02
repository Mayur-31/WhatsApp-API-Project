<template>
  <div class="h-screen flex flex-col bg-white overflow-hidden">
    <!-- Navigation Header with Team Selector -->
    <nav v-if="isAuthenticated" class="bg-green-600 text-white shadow-lg">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center py-4">
          <div class="flex items-center space-x-4">
            <h1 class="text-xl font-bold">DriverConnect</h1>
            
            <!-- Team Selector for Admin -->
            <div v-if="isAdmin && teams.length > 0" class="relative">
              <select 
                v-model="selectedTeamId" 
                @change="switchTeam"
                class="bg-green-700 border border-green-600 text-white px-3 py-2 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
              >
                <option value="0">All Teams</option>
                <option v-for="team in teams" :key="team.id" :value="team.id">
                  {{ team.name }}
                </option>
              </select>
              <span v-if="selectedTeam" class="ml-2 text-sm text-green-200">
                Team: {{ selectedTeam.name }}
              </span>
            </div>

            <!-- Show team info for non-admin users -->
            <div v-else-if="userTeam" class="text-sm text-green-200">
              Team: {{ userTeam.name }}
            </div>

            <router-link 
              to="/home" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/home' }"
            >
              Chat
            </router-link>
            <router-link 
              to="/departments" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/departments' }"
            >
              Departments
            </router-link>
            <router-link 
              to="/depots" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/depots' }"
            >
              Depots
            </router-link>
            <router-link 
              to="/users" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/users' }"
            >
              Users
            </router-link>
            
            <!-- Team Management for Admin -->
            <router-link 
              v-if="isAdmin"
              to="/teams" 
              class="hover:bg-green-500 px-3 py-2 rounded transition-colors"
              :class="{ 'bg-green-500': $route.path === '/teams' }"
            >
              Teams
            </router-link>
          </div>
          <div class="flex items-center space-x-4">
            <span class="text-sm">Welcome, {{ user?.fullName }}</span>
            <span v-if="!isAdmin && userTeam" class="text-sm text-green-200">Team: {{ userTeam.name }}</span>
            <button 
              @click="logout" 
              class="bg-red-500 hover:bg-red-600 px-3 py-2 rounded text-sm transition-colors"
            >
              Logout
            </button>
          </div>
        </div>
      </div>
    </nav>

    <!-- Main Content -->
    <main class="flex-1 w-full overflow-hidden">
      <!-- Team Info Banner -->
      <div v-if="isAdmin && selectedTeam" class="mb-4 p-3 bg-blue-100 border border-blue-300 rounded-lg max-w-screen-2xl mx-auto">
        <p class="text-blue-700 text-sm">
          📋 Viewing conversations for: <strong>{{ selectedTeam.name }}</strong>
          <span v-if="selectedTeam.whatsAppPhoneNumber" class="ml-2">
            (WhatsApp: {{ selectedTeam.whatsAppPhoneNumber }})
          </span>
        </p>
      </div>
  
      <div v-else-if="userTeam" class="mb-4 p-3 bg-green-100 border border-green-300 rounded-lg max-w-screen-2xl mx-auto">
        <p class="text-green-700 text-sm">
          👥 You are viewing your team: <strong>{{ userTeam.name }}</strong>
        </p>
      </div>
  
      <!-- UPDATED: Full-width Flex Layout -->
      <div class="flex flex-1 h-[calc(100vh-180px)] bg-white border-t border-gray-200">
        <!-- Conversations List -->
        <!-- Updated Conversations List Header Section -->
        <div class="w-[360px] flex-shrink-0 bg-white border-r border-gray-200 flex flex-col h-full">
          <div class="bg-green-100 px-4 py-3 border-b">
            <!-- Main Header Row -->
            <div class="flex justify-between items-center mb-3">
              <h2 class="text-lg font-semibold text-gray-800">Conversations</h2>
              <div class="flex space-x-2">
                <!-- Moved buttons to keep them accessible -->
                <button 
                  v-if="isAdminOrManager"
                  @click="showCreateGroupModal = true" 
                  class="text-xs bg-green-500 hover:bg-green-600 text-white px-2 py-1 rounded flex items-center space-x-1"
                >
                  <span>➕</span>
                  <span>New Group</span>
                </button>
                <!-- NEW: Add Contact Button -->
                <button 
                  v-if="isAdminOrManager"
                  @click="showCreateContactModal = true" 
                  class="text-xs bg-blue-500 hover:bg-blue-600 text-white px-2 py-1 rounded flex items-center space-x-1"
                >
                  <span>👤</span>
                  <span>New Contact</span>
                </button>
              </div>
            </div>
    
            <!-- WhatsApp-style Search Bar -->
            <div class="mb-3">
              <div class="relative">
                <!-- Search Icon -->
                <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <svg class="h-5 w-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"></path>
                  </svg>
                </div>
        
                <!-- Search Input -->
                <input
                  v-model="searchQuery"
                  type="text"
                  placeholder="Search or start new chat"
                  class="w-full pl-10 pr-10 py-2.5 bg-gray-100 border-0 rounded-full text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-green-500 focus:bg-white focus:shadow-md transition-all duration-200"
                />
        
                <!-- Clear Button (only shows when there's text) -->
                <button 
                  v-if="searchQuery" 
                  @click="searchQuery = ''" 
                  class="absolute inset-y-0 right-0 pr-3 flex items-center"
                  title="Clear search"
                >
                  <svg class="h-5 w-5 text-gray-400 hover:text-gray-600 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                  </svg>
                </button>
              </div>
      
              <!-- Search Results Info -->
              <div v-if="searchQuery" class="mt-2 px-2">
                <div v-if="filteredConversations.length > 0" class="text-xs text-blue-600">
                  Found {{ filteredConversations.length }} result(s) for "{{ searchQuery }}"
                </div>
                <div v-else class="text-xs text-gray-500">
                  No conversations found for "{{ searchQuery }}"
                </div>
              </div>
            </div>
    
            <!-- Filter Row -->
            <div class="flex justify-between items-center mb-2">
              <div class="flex space-x-1">
                <button 
                  @click="toggleUnansweredFilter" 
                  :class="['text-xs px-3 py-1.5 rounded-full transition-all', showUnansweredOnly ? 'bg-red-500 text-white' : 'bg-gray-200 text-gray-700 hover:bg-gray-300']"
                >
                  {{ showUnansweredOnly ? `Unanswered (${unansweredCount})` : 'All' }}
                </button>
                <button 
                  @click="toggleGroupsFilter" 
                  :class="['text-xs px-3 py-1.5 rounded-full transition-all flex items-center space-x-1', showGroupsOnly ? 'bg-blue-500 text-white' : 'bg-gray-200 text-gray-700 hover:bg-gray-300']"
                >
                  <span>👥</span>
                  <span>{{ showGroupsOnly ? 'Groups' : 'All Types' }}</span>
                </button>
              </div>
      
              <!-- Stats -->
              <div class="flex space-x-3 text-xs text-gray-600">
                <span>Total: {{ conversations.length }}</span>
                <span>Groups: {{ groupConversationsCount }}</span>
                <span>Indiv: {{ individualConversationsCount }}</span>
              </div>
            </div>
          </div>
  
          <!-- Conversations List -->
          <div class="flex-1 overflow-y-auto">
            <!-- ... rest of your conversations list remains the same ... -->
            <div v-if="loading" class="p-4 text-center text-gray-500">Loading conversations...</div>
            <div v-else-if="!conversations || conversations.length === 0" class="p-4 text-center text-gray-500">
              No conversations found
            </div>
            <div v-else>
              <div
                v-for="conv in filteredConversations"
                :key="conv.Id"
                @click="selectConversation(conv)"
                :class="['p-4 border-b cursor-pointer hover:bg-gray-50 transition-colors', 
                  selectedConversation?.Id === conv.Id ? 'bg-green-50 border-l-4 border-l-green-500' : '', 
                  !conv.IsAnswered ? 'border-l-4 border-l-red-500' : '']"
              >
                <!-- ... conversation item content remains the same ... -->
                <div class="flex items-center justify-between">
                  <div class="flex-1">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center space-x-2">
                        <span v-if="conv.IsGroupConversation" class="text-lg" title="Group Conversation">👥</span>
                          <h3 class="font-semibold text-gray-900">{{ getConversationDisplayName(conv) }}</h3>
                      </div>
                      <span v-if="!conv.IsAnswered" class="text-xs bg-red-100 text-red-800 px-2 py-1 rounded">New</span>
                    </div>
            
                    <div class="flex items-center space-x-2 mt-1">
                      <p class="text-sm text-gray-600">{{ getConversationSubtitle(conv) }}</p>
                      <span v-if="conv.IsGroupConversation" class="text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded">
                        Group
                      </span>
                    </div>
            
                    <p v-if="conv.DepartmentName" class="text-xs text-blue-600 mt-1">📂 {{ conv.DepartmentName }}</p>
                    <p class="text-xs text-gray-500 mt-1 truncate">{{ conv.LastMessagePreview || 'No messages' }}</p>
                      <div class="flex items-center justify-between mt-1">
                        <span class="text-xs text-gray-400">{{ conv.MessageCount }} messages</span>
                        <span v-if="conv.UnreadCount > 0" class="text-xs bg-blue-500 text-white px-1 rounded">{{ conv.UnreadCount }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        

        
        <!-- Chat Area -->
        <div class="flex-1 flex flex-col bg-white min-h-0 h-full">
          <div v-if="!selectedConversation" class="flex flex-col items-center justify-center h-full text-gray-500 p-8 bg-gray-50">
            <div class="text-6xl mb-4">💬</div>
            <h3 class="text-xl font-semibold mb-2">No Conversation Selected</h3>
            <p class="text-center">Select a conversation from the list to start chatting</p>
            <p class="text-sm mt-2 text-gray-400">
              Use filters to find specific conversations
              <span v-if="showGroupsOnly" class="block">Currently showing: Groups Only</span>
              <span v-if="showUnansweredOnly" class="block">Currently showing: Unanswered Only</span>
            </p>
          </div>
          <div class="flex-shrink-0">
            <!-- Chat Header -->
            <div class="bg-white border-b border-gray-200 px-4 py-3">
              <div class="flex items-center justify-between">
    
              <!-- Left: Contact Info -->
              <div class="flex items-center space-x-3 flex-1 min-w-0">
                <!-- Online Status Indicator -->
                <div :class="['w-2.5 h-2.5 rounded-full flex-shrink-0', 
                              selectedConversation.IsAnswered ? 'bg-green-500' : 'bg-red-500']">
                </div>
      
                <!-- Avatar -->
                <div class="w-10 h-10 bg-gradient-to-br from-green-400 to-green-600 rounded-full flex items-center justify-center text-white font-semibold text-sm flex-shrink-0 shadow-sm">
                  {{ getInitials(selectedConversation.DriverName) }}
                </div>
      
                <!-- Contact Details -->
                <div class="min-w-0 flex-1">
                  <div class="flex items-center space-x-2">
                    <h2 class="font-semibold text-gray-900 truncate text-base">
                      {{ selectedConversation.DriverName }}
                    </h2>
                    <span v-if="selectedConversation.IsGroupConversation" 
                          class="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full flex-shrink-0 font-medium">
                      👥 Group
                    </span>
                  </div>
                  <p class="text-xs text-gray-500 truncate flex items-center space-x-2">
                    <span>{{ selectedConversation.DriverPhone }}</span>
                    <span v-if="selectedConversation.DepartmentName" class="flex items-center">
                      <span class="mx-1">•</span>
                      <span class="text-blue-600">{{ selectedConversation.DepartmentName }}</span>
                    </span>
                    <span v-if="selectedConversation.IsGroupConversation" class="flex items-center">
                      <span class="mx-1">•</span>
                      <span>{{ selectedConversation.Participants?.length || 0 }} members</span>
                    </span>
                  </p>
                </div>
              </div>
    
              <!-- Right: Actions -->
              <div class="flex items-center space-x-2 flex-shrink-0 action-dropdown-container">
      
                <!-- 24-hour Window Status Badge (Hidden on mobile) -->
                <div v-if="!selectedConversation.IsGroupConversation" class="hidden sm:block">
                  <div v-if="selectedConversation.canSendNonTemplateMessages" 
                        class="flex items-center space-x-1 px-3 py-1.5 bg-green-50 border border-green-200 rounded-lg">
                    <svg class="w-3.5 h-3.5 text-green-600" fill="currentColor" viewBox="0 0 20 20">
                      <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                    </svg>
                    <span class="text-xs font-semibold text-green-700">
                      {{ Math.floor(selectedConversation.hoursRemaining || 0) }}h 
                      {{ Math.floor((selectedConversation.minutesRemaining || 0) % 60) }}m
                    </span>
                  </div>
                  <div v-else 
                        class="flex items-center space-x-1 px-3 py-1.5 bg-yellow-50 border border-yellow-200 rounded-lg">
                    <svg class="w-3.5 h-3.5 text-yellow-600" fill="currentColor" viewBox="0 0 20 20">
                      <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
                    </svg>
                    <span class="text-xs font-semibold text-yellow-700">Templates Only</span>
                  </div>
                </div>
      
                <!-- Template Button (for non-groups) -->
                <button 
                  v-if="!selectedConversation.IsGroupConversation && isAdminOrManager"
                  @click="openTemplateDialog"
                  class="hidden sm:flex items-center space-x-1.5 px-3 py-1.5 bg-purple-600 text-white text-sm rounded-lg hover:bg-purple-700 transition-colors shadow-sm"
                  title="Send template message"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  <span class="font-medium">Template</span>
                </button>
      
                <!-- Group Management Button (for groups) -->
                <button 
                  v-if="selectedConversation.IsGroupConversation && isAdminOrManager"
                  @click="showGroupManagementModal = true"
                  class="hidden sm:flex items-center space-x-1.5 px-3 py-1.5 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 transition-colors shadow-sm"
                  title="Manage group"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                  </svg>
                  <span class="font-medium">Manage</span>
                </button>
      
                <!-- Three-Dot Menu -->
                <div class="relative">
                  <button 
                    @click.stop="showActionsDropdown = !showActionsDropdown"
                    class="p-2 hover:bg-gray-100 rounded-full transition-colors"
                    title="More options"
                  >
                    <svg class="w-5 h-5 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
                      <path d="M10 6a2 2 0 110-4 2 2 0 010 4zM10 12a2 2 0 110-4 2 2 0 010 4zM10 18a2 2 0 110-4 2 2 0 010 4z" />
                    </svg>
                  </button>
        
                  <!-- Dropdown Menu -->
                  <Transition
                    enter-active-class="transition ease-out duration-100"
                    enter-from-class="transform opacity-0 scale-95"
                    enter-to-class="transform opacity-100 scale-100"
                    leave-active-class="transition ease-in duration-75"
                    leave-from-class="transform opacity-100 scale-100"
                    leave-to-class="transform opacity-0 scale-95"
                  >
                    <div 
                      v-if="showActionsDropdown"
                      class="absolute right-0 mt-2 w-56 bg-white rounded-lg shadow-xl border border-gray-200 z-50 py-1"
                      @click.stop
                    >
                      <!-- Media -->
                      <button 
                        @click="openMediaGallery(); showActionsDropdown = false"
                        class="w-full text-left px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 flex items-center space-x-3 transition-colors"
                      >
                        <span class="text-lg">🖼️</span>
                        <div class="flex-1">
                          <div class="font-medium">Media</div>
                          <div class="text-xs text-gray-500">View shared media</div>
                        </div>
                      </button>
            
                      <!-- Assign -->
                      <button 
                        @click="openAssignModal(); showActionsDropdown = false"
                        class="w-full text-left px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 flex items-center space-x-3 transition-colors"
                      >
                        <span class="text-lg">👤</span>
                        <div class="flex-1">
                          <div class="font-medium">Assign</div>
                          <div class="text-xs text-gray-500">Assign to department</div>
                        </div>
                      </button>
            
                      <!-- Mark Answered/Unanswered -->
                      <button 
                        @click="toggleAnsweredStatus(); showActionsDropdown = false"
                        class="w-full text-left px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 flex items-center space-x-3 transition-colors"
                      >
                        <span class="text-lg">{{ selectedConversation.IsAnswered ? '❌' : '✅' }}</span>
                        <div class="flex-1">
                          <div class="font-medium">
                            {{ selectedConversation.IsAnswered ? 'Mark Unanswered' : 'Mark Answered' }}
                          </div>
                          <div class="text-xs text-gray-500">Change conversation status</div>
                        </div>
                      </button>
            
                      <!-- Divider -->
                      <div class="border-t border-gray-100 my-1"></div>
            
                      <!-- Delete Contact (Danger Zone) -->
                      <button 
                        @click="deleteCurrentContact(); showActionsDropdown = false"
                        class="w-full text-left px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 flex items-center space-x-3 transition-colors"
                      >
                        <span class="text-lg">🗑️</span>
                        <div class="flex-1">
                          <div class="font-medium">Delete Contact</div>
                          <div class="text-xs text-red-500">Permanently remove</div>
                        </div>
                      </button>
                    </div>
                  </Transition>
                </div>
              </div>
            </div>
          </div>
            
            <!-- 24-Hour Window Warning (Slim Banner) -->
            <div v-if="!selectedConversation.canSendNonTemplateMessages && !selectedConversation.IsGroupConversation" 
                  class="bg-yellow-50 px-4 py-2 border-b border-yellow-200 flex items-center justify-between">
              <div class="flex items-center space-x-2 text-sm">
                <svg class="w-4 h-4 text-yellow-600 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                  <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
                </svg>
                <span class="text-yellow-800 font-medium">24-hour window expired - Only template messages can be sent</span>
              </div>
              <button 
                @click="openTemplateDialog" 
                class="px-3 py-1 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 transition-colors font-medium flex-shrink-0">
                Send Template
              </button>
            </div>

            <!-- Messages Area -->
            <div 
              class="flex-1 overflow-y-auto p-4 space-y-4 bg-gray-50 custom-scrollbar relative"
              ref="chatContainer"
              @scroll="handleScroll"
            >
              <div v-if="messagesLoading" class="text-center text-gray-500 py-8">
                <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-green-500 mx-auto mb-2"></div>
                Loading messages...
              </div>
              <div v-else-if="!messages || messages.length === 0" class="text-center text-gray-500 py-8">
                <div class="text-4xl mb-2">💭</div>
                <p class="text-lg font-semibold">No messages yet</p>
                <p class="text-sm">Start the conversation by sending a message!</p>
                <p v-if="selectedConversation.IsGroupConversation" class="text-xs text-gray-400 mt-2">
                  This is a group conversation. Messages will be sent to all group members.
                </p>
              </div>
              <div v-else>
                <div 
                  v-for="(message, index) in messages" 
                  :key="message.Id"
                  :id="`message-${message.Id}`"
                  :class="['message-container group', { 'highlighted': highlightedMessageId === message.Id }]"
                  @contextmenu="openMessageMenu(message, $event)"
                >
                  <!-- Date Separator -->
                  <div v-if="shouldShowDateSeparator(message, index)" class="text-center my-4">
                    <span class="bg-white px-3 py-1 rounded-full text-xs text-gray-500 border">
                      {{ message.FormattedDate || formatMessageDate(message.SentAt) }}
                    </span>
                  </div>
                  
                  <!-- Message -->
                  <div :class="['flex', message.IsFromDriver ? 'justify-start' : 'justify-end']">
                    <div :class="['max-w-xs lg:max-w-md px-4 py-2 rounded-lg shadow-sm', message.IsFromDriver ? 'bg-white text-gray-800 border border-gray-200' : 'bg-green-500 text-white']">
                      
                      <!-- ENHANCED: Sender Info for ALL Group Messages -->
                      <div v-if="message.IsGroupMessage" class="text-xs font-semibold mb-1 flex items-center space-x-2"
                          :class="message.IsFromDriver ? 'text-gray-700' : 'text-green-100'">
                        <span class="text-lg">👤</span>
                        <span>{{ getEnhancedSenderName(message) }}</span>
                        <!-- Staff/Driver badge -->
                        <span v-if="!message.IsFromDriver" class="staff-badge">
                          Staff
                        </span>
                        <span v-else class="driver-badge">
                          Driver
                        </span>
                        <!-- Show actual staff name if available -->
                        <span v-if="!message.IsFromDriver && message.SentByUserName" class="text-xs opacity-75">
                          ({{ message.SentByUserName }})
                        </span>
                      </div>

                      <!-- Individual message sender info (for non-group staff messages) -->
                      <div v-else-if="!message.IsFromDriver" 
                          class="text-xs font-semibold mb-1 text-green-100 flex items-center space-x-2">
                        <span class="text-lg">👤</span>
                        <span>{{ message.SentByUserName || 'Staff' }}</span>
                        <span class="staff-badge">Staff</span>
                      </div>
                      
                      <!-- ENHANCED: Clickable Reply Context with Staff/Driver Info -->
                      <div 
                        v-if="message.ReplyToMessageId" 
                        class="reply-context mb-2 p-2 rounded text-xs cursor-pointer transition-all"
                        :class="message.IsFromDriver ? 'bg-gray-100 text-gray-600 border-l-4 border-gray-400' : 'bg-green-400 text-white border-l-4 border-green-600'"
                        @click="scrollToRepliedMessage(message.ReplyToMessageId)"
                        :title="`Click to view the original message from ${getEnhancedReplySenderName(message)}`"
                      >
                        <div class="font-semibold flex items-center space-x-1 mb-1">
                          <span>↩️</span>
                          <span>Replying to {{ getEnhancedReplySenderName(message) }}</span>
                        </div>
                        <p class="truncate opacity-90">{{ message.ReplyToMessageContent || message.ReplyToMessage?.Content || 'Previous message' }}</p>
                      </div>
                      
                      <!-- Image Message -->
                      <div v-if="message.MessageType === 'Image'" class="text-center">
                        <div class="relative">
                          <!-- Loading spinner -->
                          <div v-if="imageLoadingStates[message.Id]" 
                              class="absolute inset-0 flex items-center justify-center bg-gray-200 bg-opacity-50 rounded-lg min-h-[100px]">
                            <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-green-500"></div>
                            <span class="ml-2 text-sm">Loading image...</span>
                          </div>
                          
                          <!-- Error state -->
                          <div v-if="imageErrors[message.Id]" 
                              class="bg-red-50 border border-red-200 rounded-lg p-4 text-center min-h-[100px] flex flex-col items-center justify-center">
                            <div class="text-red-500 text-2xl mb-2">❌</div>
                            <p class="text-red-600 text-sm mb-2">Failed to load image</p>
                            <button 
                              @click="retryImageLoad(message)" 
                              class="mt-2 text-xs bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600">
                              Retry
                            </button>
                          </div>
                          
                          <!-- Image -->
                          <img 
                            :src="getMediaUrl(message.MediaUrl)" 
                            :alt="message.FileName || 'Image'"
                            
                            class="media-image"
                            @click="openImageModal(getMediaUrl(message.MediaUrl))"
                            @load="handleImageLoad(message.Id)"
                            @error="handleImageError(message.Id, $event)"
                            loading="lazy"
                            :key="'img-' + message.Id"
                          />
                        </div>
                        
                        <!-- Image caption -->
                        <p v-if="message.Content && !isDefaultImageCaption(message.Content)" 
                          class="text-sm break-words mt-2">
                          {{ message.Content }}
                        </p>
                        
                        <!-- Image metadata -->
                        <div class="flex justify-between items-center mt-2 text-xs">
                          <div class="flex items-center space-x-2 opacity-75">
                            <span>{{ message.FileName || 'Image' }}</span>
                            <span v-if="message.FileSize">{{ formatFileSize(message.FileSize) }}</span>
                          </div>
                          <a 
                            :href="getMediaUrl(message.MediaUrl)" 
                            :download="message.FileName || 'image.jpg'"
                            class="bg-blue-500 hover:bg-blue-600 text-white px-3 py-1 rounded text-xs flex items-center space-x-1"
                            @click.stop
                            title="Download image"
                          >
                            <span>⬇️</span>
                            <span>Download</span>
                          </a>
                        </div>
                      </div>
                      
                      <!-- Video Message -->
                      <div v-else-if="message.MessageType === 'Video'" class="text-center">
                        <div class="relative">
                          <!-- Loading spinner -->
                          <div v-if="videoLoadingStates[message.Id]" 
                              class="absolute inset-0 flex items-center justify-center bg-gray-200 bg-opacity-50 rounded-lg min-h-[100px]">
                            <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-green-500"></div>
                            <span class="ml-2 text-sm">Loading video...</span>
                          </div>
                          
                          <!-- Error state -->
                          <div v-if="videoErrors[message.Id]" 
                              class="bg-red-50 border border-red-200 rounded-lg p-4 text-center min-h-[100px] flex flex-col items-center justify-center">
                            <div class="text-red-500 text-2xl mb-2">❌</div>
                            <p class="text-red-600 text-sm mb-2">Failed to load video</p>
                            <button 
                              @click="retryVideoLoad(message)" 
                              class="mt-2 text-xs bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600">
                              Retry
                            </button>
                          </div>
                          
                          <!-- Video -->
                          <video 
                            :src="getMediaUrl(message.MediaUrl)"
                            controls
                            class="media-video"
                            
                            @loadstart="handleVideoLoadStart(message.Id)"
                            @loadeddata="handleVideoLoad(message.Id)"
                            @error="handleVideoError(message.Id, $event)"
                            :key="'video-' + message.Id"
                          >
                            Your browser does not support the video tag.
                          </video>
                        </div>
                        <p v-if="message.Content" class="text-sm break-words">{{ message.Content }}</p>
                        <div class="flex justify-between items-center mt-2 text-xs">
                          <div class="flex items-center space-x-2 opacity-75">
                            <span>{{ message.FileName || 'Video' }}</span>
                            <span v-if="message.FileSize">{{ formatFileSize(message.FileSize) }}</span>
                          </div>
                          <a 
                            :href="getMediaUrl(message.MediaUrl)" 
                            :download="message.FileName || 'video.mp4'"
                            class="bg-blue-500 hover:bg-blue-600 text-white px-3 py-1 rounded text-xs flex items-center space-x-1"
                            @click.stop
                            title="Download video"
                          >
                            <span>⬇️</span>
                            <span>Download</span>
                          </a>
                        </div>
                      </div>
                      
                      <!-- Audio Message -->
                      <div v-else-if="message.MessageType === 'Audio'" class="text-center">
                        <div class="relative">
                          <!-- Loading spinner -->
                          <div v-if="audioLoadingStates[message.Id]" 
                              class="absolute inset-0 flex items-center justify-center bg-gray-200 bg-opacity-50 rounded-lg min-h-[80px]">
                            <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-green-500"></div>
                            <span class="ml-2 text-sm">Loading audio...</span>
                          </div>
                          
                          <!-- Error state -->
                          <div v-if="audioErrors[message.Id]" 
                              class="bg-red-50 border border-red-200 rounded-lg p-4 text-center min-h-[80px] flex flex-col items-center justify-center">
                            <div class="text-red-500 text-2xl mb-2">❌</div>
                            <p class="text-red-600 text-sm mb-2">Failed to load audio</p>
                            <button 
                              @click="retryAudioLoad(message)" 
                              class="mt-2 text-xs bg-red-500 text-white px-3 py-1 rounded hover:bg-red-600">
                              Retry
                            </button>
                          </div>
                          
                          <!-- Audio -->
                          <audio 
                            :src="getMediaUrl(message.MediaUrl)"
                            controls
                            class="media-audio"
                           
                            @loadstart="handleAudioLoadStart(message.Id)"
                            @loadeddata="handleAudioLoad(message.Id)"
                            @error="handleAudioError(message.Id, $event)"
                            :key="'audio-' + message.Id"
                          >
                            Your browser does not support the audio element.
                          </audio>
                        </div>
                        <p v-if="message.Content" class="text-sm break-words">{{ message.Content }}</p>
                        <div class="flex justify-between items-center mt-2 text-xs">
                          <div class="flex items-center space-x-2 opacity-75">
                            <span>{{ message.FileName || 'Audio' }}</span>
                            <span v-if="message.FileSize">{{ formatFileSize(message.FileSize) }}</span>
                          </div>
                          <a 
                            :href="getMediaUrl(message.MediaUrl)" 
                            :download="message.FileName || 'audio.mp3'"
                            class="bg-blue-500 hover:bg-blue-600 text-white px-3 py-1 rounded text-xs flex items-center space-x-1"
                            @click.stop
                            title="Download audio"
                          >
                            <span>⬇️</span>
                            <span>Download</span>
                          </a>
                        </div>
                      </div>
                      
                      <!-- Document Message -->
                      <div v-else-if="message.MessageType === 'Document'" class="flex items-center space-x-3 p-2 bg-black bg-opacity-10 rounded">
                        <div class="text-2xl">📄</div>
                        <div class="flex-1">
                          <a :href="getMediaUrl(message.MediaUrl)" 
                            target="_blank" 
                            class="font-medium hover:underline block" 
                            @click.stop
                            :class="message.IsFromDriver ? 'text-gray-800' : 'text-white'">
                            {{ message.FileName || 'Document' }}
                          </a>
                          <p class="text-xs opacity-75">{{ formatFileSize(message.FileSize) }}</p>
                          <p v-if="message.Content" class="text-sm mt-1">{{ message.Content }}</p>
                        </div>
                      </div>
                      
                      <!-- Location Message -->
                      <div v-else-if="message.MessageType === 'Location'" class="flex items-center space-x-3 p-2 bg-black bg-opacity-10 rounded">
                        <div class="text-2xl">📍</div>
                        <div class="flex-1">
                          <p class="font-medium">Location Shared</p>
                          <a v-if="message.Location" 
                            :href="`https://maps.google.com/?q=${message.Location}`" 
                            target="_blank" 
                            class="text-sm opacity-75 hover:underline block"
                            @click.stop
                            :class="message.IsFromDriver ? 'text-gray-600' : 'text-green-100'"
                          >
                            View on Google Maps
                          </a>
                          <p v-if="message.Content" class="text-sm mt-1">{{ message.Content }}</p>
                        </div>
                      </div>
                      
                      <!-- Text Message -->
                      <div v-else>
                        <p class="text-sm break-words" v-html="renderMessageWithLinks(message.Content)"></p>
                      </div>
                      <button 
                        v-if="message.MessageType !== 'Text'"
                        @click="debugMediaUrl(message)"
                        class="text-xs bg-gray-200 p-1 rounded"
                      >
                        Debug
                      </button>
                      <!-- WhatsApp Message Status & Interactions -->
                      <div class="flex items-center justify-between mt-1">
                        <div class="flex items-center space-x-1">
                          <!-- Quick Reactions -->
                          <button 
                            v-for="reaction in commonReactions" 
                            :key="reaction"
                            @click="reactToMessage(message, reaction)"
                            class="opacity-0 group-hover:opacity-100 transition-opacity duration-200 text-sm hover:scale-125 transform"
                            :title="`React with ${reaction}`"
                          >
                            {{ reaction }}
                          </button>
                          
                          <!-- Existing reply button -->
                          <button 
                            v-if="selectedConversation"
                            @click="startReply(message)"
                            class="text-xs opacity-70 hover:opacity-100 transition-opacity flex items-center space-x-1"
                            :class="message.IsFromDriver ? 'text-gray-500' : 'text-green-100'"
                          >
                            <span>↩️</span>
                            <span>Reply</span>
                          </button>
                        </div>
                        
                        <!-- Message Status and Time -->
                        <div class="flex items-center space-x-1 text-xs opacity-70">
                          <span v-if="message.IsStarred" title="Starred">⭐</span>
                          <span v-if="message.IsPinned" title="Pinned">📌</span>
                          <span v-if="message.ForwardCount > 0" :title="`Forwarded ${message.ForwardCount} times`">
                            ↩️{{ message.ForwardCount }}
                          </span>
                          <span :title="`Message status: ${message.Status}`">{{ message.StatusIcon }}</span>
                          <span>{{ message.FormattedTime || formatMessageTime(message.SentAt) }}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <button
                v-show="showScrollToBottomButton"
                @click="scrollToBottom()"
                class="absolute bottom-6 right-6 z-20 w-10 h-10 rounded-full bg-white shadow-lg flex items-center justify-center hover:bg-gray-100 transition-all border border-gray-200"
                title="Scroll to latest message"
              >
                <svg
                  class="w-5 h-5 text-gray-600"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M19 14l-7 7m0 0l-7-7m7 7V3"
                  />
                </svg>
              </button>
            </div>

            <!-- ENHANCED: Clickable Reply Context Bar -->
            <div 
              v-if="replyingToMessage" 
              class="bg-blue-50 px-6 py-2 border-t border-blue-200 flex items-center justify-between cursor-pointer hover:bg-blue-100 transition-colors"
              @click="scrollToRepliedMessage(replyingToMessage.Id)"
              :title="`Click to view the original message from ${getEnhancedSenderName(replyingToMessage)}`"
            >
              <div class="flex items-center space-x-2">
                <span class="text-blue-600">↩️</span>
                <span class="text-sm text-blue-700">Replying to {{ getEnhancedSenderName(replyingToMessage) }}</span>
                <span class="text-xs text-blue-600 truncate max-w-xs">{{ getReplyPreview(replyingToMessage) }}</span>
              </div>
              <button 
                @click.stop="cancelReply"
                class="text-red-500 hover:text-red-700 text-lg font-bold"
              >
                ×
              </button>
            </div>

            <!-- Upload Progress Section -->
            <div v-if="isUploading" class="bg-blue-50 px-6 py-3 border-t border-blue-200">
              <div class="flex items-center justify-between mb-2">
                <span class="text-sm font-medium text-blue-700">{{ uploadStatus }}</span>
                <span class="text-sm text-blue-600">{{ uploadProgress }}%</span>
              </div>
              <div class="w-full bg-blue-200 rounded-full h-2">
                <div 
                  class="bg-blue-600 h-2 rounded-full transition-all duration-300 ease-out" 
                  :style="{ width: uploadProgress + '%' }"
                ></div>
              </div>
            </div>

            <!-- File Info Section -->
            <div v-if="showFileInfo && uploadedFile" class="bg-green-50 px-6 py-2 border-t border-green-200">
              <div class="flex items-center justify-between">
                <span class="text-sm text-green-700">
                  📎 {{ uploadedFile.name }} ({{ fileInfo }})
                </span>
                <button 
                  @click="showFileInfo = false; uploadedFile = null" 
                  class="text-red-500 hover:text-red-700 text-lg font-bold"
                >
                  ×
                </button>
              </div>
            </div>

            <!-- Enhanced Media Options -->
            <div v-if="showMediaOptions" class="bg-gray-100 px-6 py-3 border-t">
              <div class="flex justify-between items-center mb-3">
                <span class="text-sm font-medium text-gray-700">Send Media</span>
                <button 
                  @click="showFileSizeLimits" 
                  class="text-xs bg-blue-500 hover:bg-blue-600 text-white px-2 py-1 rounded flex items-center space-x-1"
                >
                  <span>📏</span>
                  <span>Size Limits</span>
                </button>
              </div>
              <div class="flex flex-wrap gap-2">
                <button @click="openGallery" class="bg-white hover:bg-gray-50 text-gray-700 px-3 py-2 rounded-lg text-sm border border-gray-300 transition-colors flex items-center space-x-2">
                  <span>🖼️</span>
                  <span>Gallery</span>
                </button>
                <button @click="sendLocation" class="bg-white hover:bg-gray-50 text-gray-700 px-3 py-2 rounded-lg text-sm border border-gray-300 transition-colors flex items-center space-x-2">
                  <span>📍</span>
                  <span>Location</span>
                </button>
                <button @click="sendDocument" class="bg-white hover:bg-gray-50 text-gray-700 px-3 py-2 rounded-lg text-sm border border-gray-300 transition-colors flex items-center space-x-2">
                  <span>📄</span>
                  <span>Document</span>
                </button>
              </div>
            </div>
            
            
            
            <!-- Message Input -->
            <div class="bg-white px-6 py-4 border-t">
              <div class="flex space-x-4 items-start">
                <!-- Media Toggle Button -->
                <button 
                  @click="toggleMediaOptions" 
                  class="bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 py-2 rounded-lg transition-colors flex items-center justify-center"
                  :disabled="sending || !selectedConversation"
                >
                  <span class="text-lg">➕</span>
                </button>
                
                <!-- Message Input Container -->
                <div class="relative flex-1">
                  <input 
                    v-model="newMessage" 
                    type="text" 
                    :placeholder="getMessagePlaceholder()" 
                    class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent transition-all" 
                    @keypress.enter="sendMessage" 
                    :disabled="sending || !selectedConversation" 
                    :class="getInputClasses()"
                  />
                
                <!-- Disabled overlay for template-only mode -->
                  <div v-if="!selectedConversation.canSendNonTemplateMessages && !selectedConversation.IsGroupConversation" 
                       class="absolute inset-0 flex items-center justify-center bg-gray-900 bg-opacity-5 rounded-lg pointer-events-none">
                    <div class="bg-white px-3 py-1 rounded shadow-lg border-2 border-yellow-500">
                      <span class="text-yellow-600 text-sm font-semibold flex items-center">
                        <svg class="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                        </svg>
                        Templates Only
                      </span>
                    </div>
                  </div>



                <button
                    @click="sendMessage"
                    :disabled="(!newMessage.trim() && !uploadedFile) || sending || !selectedConversation || (!selectedConversation.canSendNonTemplateMessages && !selectedConversation.IsGroupConversation)"
                    class="absolute right-3 bottom-3 text-blue-600 hover:text-blue-700 disabled:text-gray-400 disabled:cursor-not-allowed transition-colors"
                    :title="(!selectedConversation.canSendNonTemplateMessages && !selectedConversation.IsGroupConversation) ? 'Use template button to send messages' : ''"
                  >
                    <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                    </svg>
                  </button>
                </div>
                
                <!-- Template Button -->
                <button 
                  @click="openTemplateDialog"
                  class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-3 rounded-lg transition-colors flex items-center justify-center"
                  :title="selectedConversation.IsGroupConversation ? 'Send template to group' : 'Send template message'"
                >
                  <svg class="w-5 h-5 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  <span class="hidden sm:inline">Template</span>
                </button>
              </div>
              
              <div v-if="selectedConversation.IsGroupConversation" class="text-xs text-gray-500 mt-2 text-center">
                💡 This message will be sent to all group members
              </div>
              
              <div v-else-if="!selectedConversation.canSendNonTemplateMessages" class="text-xs text-yellow-600 mt-2 text-center">
                ⚠️ Regular messages blocked. Use template button to send messages.
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>

    <!-- Template Dialog -->
    <TemplateMessageDialog
      v-if="showTemplateDialog && selectedConversation"
      :show="showTemplateDialog"
      :conversation-id="selectedConversation.Id"
      :phone-number="selectedConversation.DriverPhone"
      :team-id="selectedConversation.TeamId || selectedTeamId || 1"
      @close="showTemplateDialog = false"
      @sent="handleTemplateSent"
    />

    <!-- WhatsApp Message Context Menu -->
    <div 
      v-if="showMessageMenu && selectedMessageForMenu" 
      class="fixed bg-white shadow-2xl rounded-lg border border-gray-200 py-2 z-50 min-w-48"
      :style="{ left: `${menuPosition.x}px`, top: `${menuPosition.y}px` }"
      @click.stop
    >
      <div class="text-sm text-gray-700">
        <!-- Copy -->
        <button 
          @click="copyMessage(selectedMessageForMenu)"
          class="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center space-x-2"
        >
          <span>📋</span>
          <span>Copy</span>
        </button>
        
        <!-- Forward -->
        <button 
          @click="forwardMessage(selectedMessageForMenu)"
          class="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center space-x-2"
        >
          <span>↩️</span>
          <span>Forward</span>
          <span v-if="selectedMessageForMenu.ForwardCount > 0" class="text-xs bg-blue-100 text-blue-600 px-1 rounded">
            {{ selectedMessageForMenu.ForwardCount }}
          </span>
        </button>
        
        <!-- Star/Unstar -->
        <button 
          @click="toggleStarMessage(selectedMessageForMenu)"
          class="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center space-x-2"
        >
          <span>{{ selectedMessageForMenu.IsStarred ? '⭐' : '☆' }}</span>
          <span>{{ selectedMessageForMenu.IsStarred ? 'Unstar' : 'Star' }}</span>
        </button>
        
        <!-- Pin/Unpin -->
        <button 
          @click="togglePinMessage(selectedMessageForMenu)"
          class="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center space-x-2"
        >
          <span>{{ selectedMessageForMenu.IsPinned ? '📌' : '📍' }}</span>
          <span>{{ selectedMessageForMenu.IsPinned ? 'Unpin' : 'Pin' }}</span>
        </button>
        
        <!-- Info -->
        <button 
          @click="showMessageInfo(selectedMessageForMenu)"
          class="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center space-x-2"
        >
          <span>ℹ️</span>
          <span>Info</span>
        </button>
        
        <!-- Delete -->
        <button 
          v-if="selectedMessageForMenu.CanDelete"
          @click="deleteMessage(selectedMessageForMenu)"
          class="w-full text-left px-4 py-2 hover:bg-red-50 text-red-600 flex items-center space-x-2"
        >
          <span>🗑️</span>
          <span>Delete</span>
        </button>
      </div>
    </div>

    <!-- Forward Message Modal -->
    <div v-if="showForwardModal" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="cancelForward"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Forward Message</h3>
            
            <!-- Selected Message Preview -->
            <div v-if="messageToForward" class="mb-4 p-3 bg-gray-50 rounded-lg border">
              <p class="text-sm text-gray-600 mb-1">Forwarding this message:</p>
              <div class="flex items-start space-x-2">
                <div class="flex-1">
                  <p class="text-sm font-medium text-gray-800 truncate">
                    {{ getMessagePreview(messageToForward) }}
                  </p>
                  <p class="text-xs text-gray-500">
                    {{ formatMessageTime(messageToForward.SentAt) }} • 
                    {{ messageToForward.MessageType }}
                  </p>
                </div>
              </div>
            </div>
            
            <!-- Conversations List -->
            <div class="mb-4">
              <label class="block text-sm font-medium text-gray-700 mb-2">
                Select Conversations to Forward To
              </label>
              
              <div v-if="loadingAvailableConversations" class="text-center py-4">
                <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-green-500 mx-auto"></div>
                <p class="text-sm text-gray-500 mt-2">Loading conversations...</p>
              </div>
              
              <div v-else-if="availableConversations.length === 0" class="text-center py-4 border border-gray-300 rounded-lg">
                <p class="text-sm text-gray-500">No other conversations available for forwarding.</p>
                <p class="text-xs text-gray-400 mt-1">Create another conversation first.</p>
              </div>
              
              <div v-else class="max-h-64 overflow-y-auto border border-gray-300 rounded-lg">
                <div 
                  v-for="conversation in availableConversations" 
                  :key="conversation.Id"
                  class="flex items-center space-x-3 p-3 hover:bg-gray-50 border-b border-gray-100 last:border-b-0 cursor-pointer transition-colors"
                  @click="toggleForwardConversation(conversation)"
                  :class="{
                    'bg-green-50 border-green-200': isConversationSelected(conversation.Id)
                  }"
                >
                  <input 
                    type="checkbox" 
                    :checked="isConversationSelected(conversation.Id)"
                    class="h-4 w-4 text-green-600 focus:ring-green-500 border-gray-300 rounded"
                    @click.stop="toggleForwardConversation(conversation)"
                  />
                  
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center space-x-2 mb-1">
                      <p class="text-sm font-medium text-gray-900 truncate">
                        {{ getConversationDisplayName(conversation) }}
                      </p>
                      <span 
                        v-if="conversation.IsGroupConversation" 
                        class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800"
                      >
                        Group
                      </span>
                    </div>
                    
                    <p class="text-xs text-gray-500 truncate">
                      {{ getConversationSubtitle(conversation) }}
                    </p>
                    
                    <div class="flex items-center space-x-2 mt-1">
                      <span class="text-xs text-gray-400">
                        ID: {{ conversation.Id }}
                      </span>
                      <span 
                        v-if="!conversation.IsAnswered" 
                        class="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-red-100 text-red-800"
                      >
                        New
                      </span>
                    </div>
                  </div>
                </div>
              </div>
              
              <p class="text-xs text-gray-500 mt-2">
                Selected: {{ selectedForwardConversations.length }} conversation(s)
              </p>
            </div>
            
            <!-- Custom Message -->
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-2">
                Custom Message (Optional)
              </label>
              <textarea 
                v-model="customForwardMessage" 
                rows="3" 
                placeholder="Add a message to accompany the forwarded content..."
                class="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-transparent text-sm"
              ></textarea>
              <p class="text-xs text-gray-400 mt-1">
                Leave empty to forward the original message as-is
              </p>
            </div>
          </div>
          
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="executeForward" 
              :disabled="selectedForwardConversations.length === 0 || forwardingMessage || !messageToForward"
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
            >
              <span v-if="forwardingMessage" class="flex items-center">
                <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                Forwarding...
              </span>
              <span v-else>
                Forward to {{ selectedForwardConversations.length }} conversation(s)
              </span>
            </button>
            
            <button 
              @click="cancelForward" 
              :disabled="forwardingMessage"
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-100 disabled:cursor-not-allowed transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Message Info Modal -->
    <div v-if="showInfoModal && messageInfo" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="showInfoModal = false"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Message Info</h3>
            
            <!-- Message Preview -->
            <div class="mb-6 p-4 bg-gray-50 rounded-lg">
              <div class="flex items-start space-x-3">
                <div class="flex-1">
                  <p class="text-sm text-gray-600 mb-1">
                    {{ messageInfo.Message.IsFromDriver ? 'From Driver' : 'From You' }} • 
                    {{ formatMessageTime(messageInfo.Message.SentAt) }}
                  </p>
                  <p class="text-gray-800">{{ messageInfo.Message.Content }}</p>
                  <div class="flex items-center space-x-2 mt-2 text-xs text-gray-500">
                    <span>Status: {{ messageInfo.Message.Status }}</span>
                    <span v-if="messageInfo.Message.IsStarred">⭐ Starred</span>
                    <span v-if="messageInfo.Message.IsPinned">📌 Pinned</span>
                    <span v-if="messageInfo.Message.ForwardCount > 0">↩️ Forwarded {{ messageInfo.Message.ForwardCount }} times</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Delivery Stats -->
            <div class="grid grid-cols-3 gap-4 mb-6 text-center">
              <div class="bg-blue-50 p-3 rounded-lg">
                <p class="text-2xl font-bold text-blue-600">{{ messageInfo.TotalRecipients }}</p>
                <p class="text-xs text-blue-600">Total</p>
              </div>
              <div class="bg-green-50 p-3 rounded-lg">
                <p class="text-2xl font-bold text-green-600">{{ messageInfo.DeliveredCount }}</p>
                <p class="text-xs text-green-600">Delivered</p>
              </div>
              <div class="bg-purple-50 p-3 rounded-lg">
                <p class="text-2xl font-bold text-purple-600">{{ messageInfo.ReadCount }}</p>
                <p class="text-xs text-purple-600">Read</p>
              </div>
            </div>

            <!-- Recipients List -->
            <div v-if="messageInfo.Recipients.length > 0">
              <h4 class="font-medium text-gray-700 mb-3">Recipients</h4>
              <div class="space-y-2 max-h-64 overflow-y-auto">
                <div 
                  v-for="recipient in messageInfo.Recipients" 
                  :key="recipient.Id"
                  class="flex items-center justify-between p-3 border border-gray-200 rounded-lg"
                >
                  <div class="flex items-center space-x-3">
                    <div class="w-8 h-8 bg-green-500 rounded-full flex items-center justify-center text-white text-sm">
                      {{ getInitials(recipient.ParticipantName || recipient.DriverName || '?') }}
                    </div>
                    <div>
                      <p class="font-medium text-sm">
                        {{ recipient.ParticipantName || recipient.DriverName || 'Unknown' }}
                      </p>
                      <p class="text-xs text-gray-500">
                        {{ recipient.PhoneNumber || 'No phone' }}
                      </p>
                    </div>
                  </div>
                  <div class="text-right">
                    <p class="text-sm font-medium" :class="getStatusColor(recipient.Status)">
                      {{ recipient.Status }}
                    </p>
                    <p v-if="recipient.ReadAt" class="text-xs text-gray-500">
                      Read {{ formatRelativeTime(recipient.ReadAt) }}
                    </p>
                    <p v-else-if="recipient.DeliveredAt" class="text-xs text-gray-500">
                      Delivered {{ formatRelativeTime(recipient.DeliveredAt) }}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <!-- Reactions -->
            <div v-if="messageInfo.Reactions.length > 0" class="mt-6">
              <h4 class="font-medium text-gray-700 mb-3">Reactions</h4>
              <div class="flex flex-wrap gap-2">
                <div 
                  v-for="reaction in messageInfo.Reactions" 
                  :key="reaction.Id"
                  class="flex items-center space-x-1 bg-gray-100 px-3 py-1 rounded-full"
                >
                  <span class="text-sm">{{ reaction.Reaction }}</span>
                  <span class="text-xs text-gray-600">{{ reaction.ReactorName }}</span>
                </div>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="showInfoModal = false" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:ml-3 sm:w-auto sm:text-sm"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- NEW: Create Contact Modal -->
    <div v-if="showCreateContactModal" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeCreateContactModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-md sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Create New Contact</h3>
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Name *</label>
                <input 
                  v-model="newContact.Name" 
                  type="text" 
                  placeholder="Contact Name" 
                  class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Phone Number *</label>
                <input 
                  v-model="newContact.PhoneNumber" 
                  type="text" 
                  placeholder="+1234567890" 
                  class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                />
              </div>
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">First Name</label>
                  <input 
                    v-model="newContact.FirstName" 
                    type="text" 
                    placeholder="First Name" 
                    class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                  />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Last Name</label>
                  <input 
                    v-model="newContact.LastName" 
                    type="text" 
                    placeholder="Last Name" 
                    class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                  />
                </div>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="createNewContact" 
              :disabled="creatingContact || !isValidContact" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {{ creatingContact ? 'Creating...' : 'Create Contact' }}
            </button>
            <button 
              @click="closeCreateContactModal" 
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Existing modals (Image Modal, Assignment Modal, Create Group Modal, etc.) -->
    <!-- ... your existing modal code remains the same ... -->
    <!-- Image Modal -->
    <div v-if="showImageModal" class="fixed inset-0 bg-black bg-opacity-75 flex items-center justify-center z-50" @click="closeImageModal">
      <div class="max-w-4xl max-h-full p-4">
        <img :src="selectedImage" alt="Enlarged view" class="max-w-full max-h-full object-contain rounded-lg">
        <button @click="closeImageModal" class="absolute top-4 right-4 text-white text-2xl bg-black bg-opacity-50 rounded-full w-10 h-10 flex items-center justify-center">
          ×
        </button>
      </div>
    </div>

    <!-- Assignment Modal -->
    <div v-if="showAssignModal" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeAssignModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Assign Conversation</h3>
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Department</label>
                <select v-model="assignData.DepartmentId" class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500">
                  <option :value="null">No Department</option>
                  <option v-for="dept in departments" :key="dept.Id" :value="dept.Id">{{ dept.Name }}</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Assign To User</label>
                <select v-model="assignData.AssignedToUserId" class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500">
                  <option :value="null">No User</option>
                  <option v-for="usr in users" :key="usr.Id" :value="usr.Id">{{ usr.FullName }} ({{ usr.Email }})</option>
                </select>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="saveAssignment" 
              :disabled="assigning" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {{ assigning ? 'Saving...' : 'Save Assignment' }}
            </button>
            <button 
              @click="closeAssignModal" 
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Enhanced Create Group Modal -->
    <div v-if="showCreateGroupModal" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeCreateGroupModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Create New Group</h3>
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">WhatsApp Group ID *</label>
                <input 
                  v-model="newGroup.WhatsAppGroupId" 
                  type="text" 
                  placeholder="120363123456789@g.us" 
                  class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                />
                <p class="text-xs text-gray-500 mt-1">Format: 120363123456789@g.us</p>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Group Name *</label>
                <input 
                  v-model="newGroup.Name" 
                  type="text" 
                  placeholder="DriverConnect Group" 
                  class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                />
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Description (Optional)</label>
                <textarea 
                  v-model="newGroup.Description" 
                  rows="3" 
                  placeholder="Group description..." 
                  class="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500"
                ></textarea>
              </div>
              
              <!-- Participants Section -->
              <div>
                <div class="flex justify-between items-center mb-2">
                  <label class="block text-sm font-medium text-gray-700">Group Participants</label>
                  <button 
                    type="button"
                    @click="addEmptyParticipant"
                    class="text-xs bg-green-500 hover:bg-green-600 text-white px-2 py-1 rounded flex items-center space-x-1"
                  >
                    <span>➕</span>
                    <span>Add Participant</span>
                  </button>
                </div>
                
                <div v-for="(participant, index) in newGroup.Participants" :key="index" class="flex space-x-2 mb-2 items-end">
                  <div class="flex-1">
                    <label class="block text-xs text-gray-600 mb-1">Phone Number *</label>
                    <input 
                      v-model="participant.PhoneNumber" 
                      type="text" 
                      placeholder="+1234567890" 
                      class="block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500 text-sm"
                    />
                  </div>
                  <div class="flex-1">
                    <label class="block text-xs text-gray-600 mb-1">Name</label>
                    <input 
                      v-model="participant.ParticipantName" 
                      type="text" 
                      placeholder="Participant Name" 
                      class="block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500 text-sm"
                    />
                  </div>
                  <div class="w-24">
                    <label class="block text-xs text-gray-600 mb-1">Role</label>
                    <select 
                      v-model="participant.Role" 
                      class="block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500 text-sm"
                    >
                      <option value="member">Member</option>
                      <option value="admin">Admin</option>
                    </select>
                  </div>
                  <button 
                    type="button"
                    @click="removeParticipant(index)"
                    class="bg-red-500 hover:bg-red-600 text-white p-2 rounded text-sm"
                  >
                    ✕
                  </button>
                </div>
                
                <div v-if="newGroup.Participants.length === 0" class="text-center py-4 text-gray-500 border-2 border-dashed border-gray-300 rounded-lg">
                  <p>No participants added yet</p>
                  <p class="text-sm">Click "Add Participant" to add members to this group</p>
                </div>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="createGroup" 
              :disabled="creatingGroup || !isValidGroup" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {{ creatingGroup ? 'Creating...' : 'Create Group' }}
            </button>
            <button 
              @click="closeCreateGroupModal" 
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Group Management Modal -->
    <div v-if="showGroupManagementModal && selectedConversation" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeGroupManagementModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Manage Group: {{ selectedConversation.GroupName }}</h3>
            
            <!-- Group Info -->
            <div class="mb-6 p-4 bg-gray-50 rounded-lg">
              <h4 class="font-medium text-gray-700 mb-2">Group Information</h4>
              <div class="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <label class="font-medium">Group ID:</label>
                  <p class="text-gray-600">{{ selectedConversation.GroupId || 'Not available' }}</p>
                </div>
                <div>
                  <label class="font-medium">WhatsApp ID:</label>
                  <p class="text-gray-600">{{ selectedConversation.WhatsAppGroupId }}</p>
                </div>
                <div>
                  <label class="font-medium">Created:</label>
                  <p class="text-gray-600">{{ formatDate(selectedConversation.CreatedAt) }}</p>
                </div>
                <div>
                  <label class="font-medium">Participants:</label>
                  <p class="text-gray-600">{{ selectedConversation.Participants?.length || 0 }}</p>
                </div>
              </div>
            </div>

            <!-- Participants List -->
            <div>
              <div class="flex justify-between items-center mb-4">
                <h4 class="font-medium text-gray-700">
                  Participants ({{ selectedConversation.Participants ? selectedConversation.Participants.length : 0 }})
                </h4>
                <button 
                  @click="showAddParticipantsModal = true"
                  class="text-xs bg-green-500 hover:bg-green-600 text-white px-3 py-2 rounded flex items-center space-x-1"
                  :disabled="!selectedConversation.GroupId"
                  :title="!selectedConversation.GroupId ? 'Group ID missing - cannot add participants' : 'Add participants to group'"
                >
                  <span>➕</span>
                  <span>Add Participants</span>
                </button>
              </div>
              
              <div v-if="!selectedConversation.Participants || selectedConversation.Participants.length === 0" 
                   class="text-center py-8 text-gray-500 bg-gray-50 rounded-lg">
                <p class="mb-2">No participants in this group</p>
                <p class="text-sm">Click "Add Participants" to add members to this group</p>
              </div>
              
              <div v-else class="space-y-2 max-h-64 overflow-y-auto">
                <div 
                  v-for="participant in selectedConversation.Participants" 
                  :key="participant.Id"
                  class="flex items-center justify-between p-3 bg-white border border-gray-200 rounded-lg hover:bg-gray-50"
                >
                  <div class="flex items-center space-x-3">
                    <div class="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-sm">
                      {{ getInitials(participant.ParticipantName || participant.DriverName || participant.PhoneNumber || '?') }}
                    </div>
                    <div>
                      <p class="font-medium text-sm">
                        {{ participant.ParticipantName || participant.DriverName || 'Unknown' }}
                      </p>
                      <p class="text-xs text-gray-500">
                        {{ participant.PhoneNumber || participant.DriverPhone || 'No phone' }}
                      </p>
                      <p v-if="participant.DriverName && participant.ParticipantName !== participant.DriverName" 
                         class="text-xs text-green-600">
                        Driver: {{ participant.DriverName }}
                      </p>
                    </div>
                  </div>
                  <div class="flex items-center space-x-2">
                    <span class="text-xs bg-gray-100 text-gray-700 px-2 py-1 rounded capitalize">
                      {{ participant.Role }}
                    </span>
                    <button 
                      @click="removeGroupParticipant(participant.Id)"
                      class="text-red-500 hover:text-red-700 text-sm font-medium px-2 py-1 rounded hover:bg-red-50 transition-colors"
                      :disabled="!selectedConversation.GroupId"
                      :title="!selectedConversation.GroupId ? 'Group ID missing - cannot remove participant' : 'Remove participant'"
                    >
                      Remove
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="deleteCurrentGroup"
              :disabled="deletingGroup || !selectedConversation.GroupId"
              :title="!selectedConversation.GroupId ? 'Group ID missing - cannot delete group' : 'Delete this group'"
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-red-600 text-base font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {{ deletingGroup ? 'Deleting...' : 'Delete Group' }}
            </button>
            <button 
              @click="closeGroupManagementModal" 
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Add Participants Modal -->
    <div v-if="showAddParticipantsModal" class="fixed z-50 inset-0 overflow-y-auto">
      <div class="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" @click="closeAddParticipantsModal"></div>
        <span class="hidden sm:inline-block sm:align-middle sm:h-screen">&#8203;</span>
        <div class="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Add Participants to Group</h3>
            
            <div class="space-y-4">
              <div v-for="(participant, index) in newParticipants" :key="index" class="flex space-x-2 items-end">
                <div class="flex-1">
                  <label class="block text-xs text-gray-600 mb-1">Phone Number *</label>
                  <input 
                    v-model="participant.PhoneNumber" 
                    type="text" 
                    placeholder="+1234567890" 
                    class="block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500 text-sm"
                  />
                </div>
                <div class="flex-1">
                  <label class="block text-xs text-gray-600 mb-1">Name</label>
                  <input 
                    v-model="participant.ParticipantName" 
                    type="text" 
                    placeholder="Participant Name" 
                    class="block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500 text-sm"
                  />
                </div>
                <div class="w-24">
                  <label class="block text-xs text-gray-600 mb-1">Role</label>
                  <select 
                    v-model="participant.Role" 
                    class="block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-green-500 focus:border-green-500 text-sm"
                  >
                    <option value="member">Member</option>
                    <option value="admin">Admin</option>
                  </select>
                </div>
                <button 
                  @click="removeNewParticipant(index)"
                  class="bg-red-500 hover:bg-red-600 text-white p-2 rounded text-sm"
                >
                  ✕
                </button>
              </div>
              
              <button 
                @click="addNewParticipant"
                class="w-full bg-gray-100 hover:bg-gray-200 text-gray-700 py-2 rounded-lg text-sm border border-dashed border-gray-300"
              >
                + Add Another Participant
              </button>
            </div>
          </div>
          <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
              @click="addParticipantsToGroup" 
              :disabled="addingParticipants || !selectedConversation?.GroupId" 
              class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-green-600 text-base font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:ml-3 sm:w-auto sm:text-sm disabled:bg-gray-400 disabled:cursor-not-allowed"
            >
              {{ addingParticipants ? 'Adding...' : 'Add Participants' }}
            </button>
            <button 
              @click="closeAddParticipantsModal" 
              class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Media Gallery -->
    <MediaGalleryView 
      v-if="showMediaGallery && selectedConversationForMedia"
      :conversation-id="selectedConversationForMedia.id"
      :conversation-name="selectedConversationForMedia.name"
      :is-group="selectedConversationForMedia.isGroup"
      @close="closeMediaGallery"
    />

    <!-- Hidden File Input -->
    <input 
      type="file" 
      ref="fileInput" 
      @change="handleFileUpload" 
      style="display: none" 
      accept="image/*,video/*,audio/*,.pdf,.doc,.docx,.txt,.zip,.rar,.7z,.tar,.gz,.xls,.xlsx,.ppt,.pptx"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, watch, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import api from '@/axios';
import MediaGalleryView from '@/components/MediaGalleryView.vue';
import TemplateMessageDialog from '@/components/TemplateMessageDialog.vue';

import type { 
  ConversationDto, 
  MessageDto, 
  DepartmentDto, 
  UserDto, 
  ConversationDetailDto, 
  CreateGroupRequest,
  GroupParticipantRequest,
  AddParticipantsRequest,
  MessageRequest,
  MessageInfoResponse,
  UpdateMessageStatusRequest,
  ReactToMessageRequest,
  ForwardMessageRequest,
  PinMessageRequest,
  StarMessageRequest
} from '@/types/conversations';

const router = useRouter();
const authStore = useAuthStore();

// Team state
const teams = ref<any[]>([]);
const selectedTeamId = ref<number>(0);
const selectedTeam = ref<any>(null);
const showActionMenu = ref(false);
const showActionsDropdown = ref(false);

// 24-hour window state
const showTemplateDialog = ref(false);
const templatePhoneNumber = ref('');

const windowStatusPollInterval = ref<number | null>(null);
// ✅ NEW: Window status polling interval

const searchQuery = ref('');

function renderMessageWithLinks(content: string) {
  if (!content) return '';
  
  // URL regex pattern
  const urlPattern = /(https?:\/\/[^\s]+)/g;
  
  // Replace URLs with clickable links
  return content.replace(urlPattern, (url) => {
    return `<a href="${url}" target="_blank" rel="noopener noreferrer" class="text-blue-600 hover:text-blue-800 underline" onclick="event.stopPropagation()">${url}</a>`;
  });
}

// ✅ FIXED: Safe alternative (Option 1) - Array-based rendering
function renderMessageContent(content: string) {
  if (!content) return [];
  
  // Detect URLs
  const urlPattern = /(https?:\/\/[^\s]+)/g;
  const parts = [];
  let lastIndex = 0;
  let match;

  while ((match = urlPattern.exec(content)) !== null) {
    // Add text before URL
    if (match.index > lastIndex) {
      parts.push({
        type: 'text',
        content: content.substring(lastIndex, match.index)
      });
    }
    
    // Add URL
    parts.push({
      type: 'link',
      content: match[0],
      url: match[0]
    });
    
    lastIndex = match.index + match[0].length;
  }
  
  // Add remaining text
  if (lastIndex < content.length) {
    parts.push({
      type: 'text',
      content: content.substring(lastIndex)
    });
  }
  
  return parts.length > 0 ? parts : [{ type: 'text', content }];
}

function getMediaUrl(url: string | undefined): string {
  if (!url) return '';
  
  // If URL is already absolute, return as is
  if (url.startsWith('http://') || url.startsWith('https://')) {
    return url;
  }
  
  // If URL is relative, prepend base URL
  const baseUrl = window.location.origin;
  return url.startsWith('/') ? `${baseUrl}${url}` : `${baseUrl}/${url}`;
}

// ✅ FIXED: Format file size
function formatFileSize(bytes: number | undefined): string {
  if (!bytes) return '';
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
}

const closeDropdownOnClickOutside = (event) => {
  if (!event.target.closest('.action-dropdown-container')) {
    showActionsDropdown.value = false;
  }
};


const debugMediaUrl = (message: MessageDto) => {
  console.log('Message ID:', message.Id);
  console.log('Media URL from DB:', message.MediaUrl);
  console.log('Processed URL:', getMediaUrl(message.MediaUrl));
  console.log('Message Type:', message.MessageType);
  console.log('File Name:', message.FileName);
};

const getInitials = (name: string): string => {
  if (!name) return '?';
  const parts = name.trim().split(' ');
  if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
};

// Computed properties
const isAdmin = computed(() => authStore.isAdmin);
const isAdminOrManager = computed(() => authStore.isAdminOrManager);
const userTeam = computed(() => {
  if (!authStore.userTeamId) return null;
  return teams.value.find(t => t.id === authStore.userTeamId);
});

const currentUserTeamId = computed(() => authStore.userTeamId || 0);

// WhatsApp Message Interaction Features
const showMessageMenu = ref(false);
const selectedMessageForMenu = ref<MessageDto | null>(null);
const menuPosition = ref({ x: 0, y: 0 });
const showForwardModal = ref(false);
const showInfoModal = ref(false);
const messageInfo = ref<MessageInfoResponse | null>(null);
const availableConversations = ref<ConversationDto[]>([]);
const selectedForwardConversations = ref<number[]>([]);
const customForwardMessage = ref('');
const forwardingMessage = ref(false);
const loadingAvailableConversations = ref(false);
const messageToForward = ref<MessageDto | null>(null);

// Common emoji reactions
const commonReactions = ['👍', '❤️', '😂', '😮', '😢', '🙏'];

// Contact Management State
const showCreateContactModal = ref(false);
const creatingContact = ref(false);
const newContact = ref({
  Name: '',
  PhoneNumber: '',
  FirstName: '',
  LastName: ''
});

// Existing reactive data
const loading = ref(false);
const messagesLoading = ref(false);
const sending = ref(false);
const assigning = ref(false);
const creatingGroup = ref(false);
const conversations = ref<ConversationDto[]>([]);
const selectedConversation = ref<ConversationDetailDto | null>(null);
const messages = ref<MessageDto[]>([]);
const newMessage = ref('');
const chatContainer = ref<HTMLElement | null>(null);

const showScrollToBottomButton = ref(false);
const isAutoScrolling = ref(false);

const showUnansweredOnly = ref(false);
const showGroupsOnly = ref(false);
const unansweredCount = ref(0);
const showAssignModal = ref(false);
const showCreateGroupModal = ref(false);
const showMediaOptions = ref(false);
const showImageModal = ref(false);
const selectedImage = ref('');
const departments = ref<DepartmentDto[]>([]);
const users = ref<UserDto[]>([]);
const uploadedFile = ref<File | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);

const showMediaGallery = ref(false);
const selectedConversationForMedia = ref<{id: number, name: string, isGroup: boolean} | null>(null);

// Upload functionality
const uploadProgress = ref(0);
const isUploading = ref(false);
const uploadStatus = ref('');
const showFileInfo = ref(false);
const fileInfo = ref('');

// Media loading states
const imageLoadingStates = ref<Record<number, boolean>>({});
const imageErrors = ref<Record<number, boolean>>({});
const imageRetryCount = ref<Record<number, number>>({});

const videoLoadingStates = ref<Record<number, boolean>>({});
const videoErrors = ref<Record<number, boolean>>({});
const videoRetryCount = ref<Record<number, number>>({});

const audioLoadingStates = ref<Record<number, boolean>>({});
const audioErrors = ref<Record<number, boolean>>({});
const audioRetryCount = ref<Record<number, number>>({});

// Group management
const showGroupManagementModal = ref(false);
const showAddParticipantsModal = ref(false);
const deletingGroup = ref(false);
const addingParticipants = ref(false);

// Enhanced: Reply functionality with message highlighting
const replyingToMessage = ref<MessageDto | null>(null);
const highlightedMessageId = ref<number | null>(null);

const newGroup = ref<CreateGroupRequest>({
  WhatsAppGroupId: '',
  Name: '',
  Description: '',
  Participants: []
});

const newParticipants = ref<GroupParticipantRequest[]>([]);

const assignData = ref({
  ConversationId: 0,
  DepartmentId: null as number | null,
  AssignedToUserId: null as string | null
});

// Quick replies
const quickReplies = [
  'Hello, how can I help?',
  'I\'m available for the delivery',
  'Running late, will arrive in 10 minutes',
  'I have arrived at the location',
  'Thanks for the update!',
  'Can you provide more details?',
  'Please confirm the address',
  'I need assistance with the route'
];

// Computed properties
const user = computed(() => authStore.user);
const groupConversationsCount = computed(() => conversations.value.filter(c => c.IsGroupConversation).length);
const individualConversationsCount = computed(() => conversations.value.filter(c => !c.IsGroupConversation).length);
const isAuthenticated = computed(() => authStore.isAuthenticated);

const isValidGroup = computed(() => {
  return newGroup.value.WhatsAppGroupId.trim() !== '' && 
         newGroup.value.Name.trim() !== '' &&
         newGroup.value.Participants.length > 0;
});

const isValidContact = computed(() => {
  return newContact.value.Name.trim() !== '' && 
         newContact.value.PhoneNumber.trim() !== '';
});

// Update filteredConversations computed property
const filteredConversations = computed(() => {
  // If no search query, return all conversations
  if (!searchQuery.value.trim()) {
    return conversations.value;
  }

  const query = searchQuery.value.toLowerCase();
  
  return conversations.value.filter(conv => {
    // Search by driver/contact name
    const nameMatch = conv.DriverName?.toLowerCase().includes(query) || false;
    
    // Search by phone number
    const phoneMatch = conv.DriverPhone?.includes(query) || 
                      conv.PhoneNumber?.includes(query) || false;
    
    // Search by group name (if group conversation)
    const groupNameMatch = conv.GroupName?.toLowerCase().includes(query) || false;
    
    // Search by WhatsApp ID
    const groupIdMatch = conv.WhatsAppGroupId?.toLowerCase().includes(query) || false;
    
    return nameMatch || phoneMatch || groupNameMatch || groupIdMatch;
  });
});

// NEW: 24-hour window methods
const getInputPlaceholder = () => {
  if (!selectedConversation.value) return 'Type a message...';
  
  if (selectedConversation.value.IsGroupConversation) {
    return 'Type a message to the group...';
  }
  
  return selectedConversation.value.canSendNonTemplateMessages 
    ? 'Type a message...' 
    : 'Regular messages blocked. Use template button.';
};

const getInputClasses = () => {
  if (!selectedConversation.value) {
    return 'border-gray-300 focus:ring-blue-500';
  }
  
  if (selectedConversation.value.IsGroupConversation) {
    return 'border-gray-300 focus:ring-blue-500';
  }
  
  return selectedConversation.value.canSendNonTemplateMessages
    ? 'border-gray-300 focus:ring-blue-500'
    : 'bg-gray-100 border-yellow-300 focus:ring-yellow-500 cursor-not-allowed text-gray-500';
};

const formatTimeAgo = (dateString: string) => {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  
  if (diffHours < 1) {
    const diffMinutes = Math.floor(diffMs / (1000 * 60));
    return `${diffMinutes} minute${diffMinutes !== 1 ? 's' : ''} ago`;
  } else if (diffHours < 24) {
    return `${diffHours} hour${diffHours !== 1 ? 's' : ''} ago`;
  } else {
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} day${diffDays !== 1 ? 's' : ''} ago`;
  }
};

// ✅ NEW: Function to poll window status
const startWindowStatusPolling = () => {
  if (windowStatusPollInterval.value) {
    clearInterval(windowStatusPollInterval.value);
  }

  const pollFunction = async () => {
    if (! selectedConversation.value?. Id) return;

    try {
      const response = await api.get(`/conversations/${selectedConversation.value.Id}/window-status`);
      
      if (response.data) {
        selectedConversation.value.canSendNonTemplateMessages = response.data.canSendNonTemplateMessages;
        selectedConversation.value.hoursRemaining = response.data.hoursRemaining;
        selectedConversation.value.minutesRemaining = response.data.minutesRemaining;
        selectedConversation.value.windowExpiresAt = response.data.windowExpiresAt;
        selectedConversation.value.lastInboundMessageAt = response. data.lastInboundMessageAt;
        
        // ✅ Update UI status message
        console.log(`🔄 Window Status:  CanSend=${response.data. canSendNonTemplateMessages}, Status=${response.data.status}`);
      }
    } catch (error) {
      console.warn("Failed to poll window status:", error);
    }
  };

  // Poll immediately and then every 30 seconds
  pollFunction();
  windowStatusPollInterval.value = window.setInterval(pollFunction, 30000) as unknown as number;
};

const handleTemplateSent = async () => {
  showTemplateDialog.value = false;
  if (selectedConversation.value) {
    console.log('🔄 Template sent, refreshing conversation and window status...');
    
    // Refresh the conversation to get updated window status
    try {
      await selectConversation(selectedConversation.value);
      
      // Restart window status polling for individual conversations
      if (!selectedConversation.value.IsGroupConversation) {
        console.log('🔄 Refreshing window status after sending message');
        // Trigger immediate window status refresh
        if (windowStatusPollInterval.value) {
          clearInterval(windowStatusPollInterval.value);
          windowStatusPollInterval.value = null;
        }
        startWindowStatusPolling();
      }
    } catch (error) {
      console.error('Error refreshing conversation after template sent:', error);
    }
  }
};
// NEW: Team methods
const loadTeams = async () => {
  console.log('Loading teams... User is admin:', isAdmin.value);
  console.log('User team ID:', authStore.userTeamId);
  
  try {
    const response = await api.get('/whatsapp/teams');
    teams.value = response.data || [];
    console.log(`Loaded ${teams.value.length} teams:`, teams.value);
    
    // Set default selected team - FIXED LOGIC
    if (isAdmin.value && teams.value.length > 0) {
      selectedTeamId.value = 0; // Default to "All Teams" for admin
      console.log('Admin user - defaulting to "All Teams"');
    } else if (!isAdmin.value && userTeam.value) {
      selectedTeamId.value = userTeam.value.id;
      console.log('Non-admin user - defaulting to their team:', userTeam.value.name);
    } else if (!isAdmin.value && teams.value.length > 0) {
      // Non-admin user without specific team, use first available team
      selectedTeamId.value = teams.value[0].id;
      console.log('Non-admin user without specific team - defaulting to first team:', teams.value[0].name);
    }
    
    // Set selected team object
    if (selectedTeamId.value > 0) {
      selectedTeam.value = teams.value.find(t => t.id === selectedTeamId.value);
    } else {
      selectedTeam.value = null;
    }

    console.log('Final selected team ID:', selectedTeamId.value);
    console.log('Final selected team:', selectedTeam.value);
  } catch (error: any) {
    console.error('Error loading teams:', error);
    // Don't show error for non-admin users if teams fail to load
    if (isAdmin.value) {
      alert(`Failed to load teams: ${error.response?.data?.message || error.message || 'Unknown error'}`);
    }
  }
};

const switchTeam = async () => {
  console.log('Switching team to:', selectedTeamId.value);
  
  if (selectedTeamId.value === 0) {
    selectedTeam.value = null;
    console.log('Selected "All Teams"');
  } else {
    selectedTeam.value = teams.value.find(t => t.id === selectedTeamId.value);
    console.log('Selected team:', selectedTeam.value);
  }
  
  // Reload conversations for selected team
  await loadConversations();
};

// UPDATED: loadConversations to filter by team
const loadConversations = async () => {
  loading.value = true;
  try {
    let url = '/conversations';
    const params = [];
    
    if (showUnansweredOnly.value) params.push('unanswered=true');
    if (showGroupsOnly.value) params.push('groupsOnly=true');
    
    // 🔥 CRITICAL: Always include team parameter - FIXED LOGIC
    if (isAdmin.value) {
      // Admin: send selected team ID (0 for "All Teams")
      params.push(`teamId=${selectedTeamId.value}`);
      console.log(`Admin loading conversations for team: ${selectedTeamId.value}`);
    } else {
      // Non-admin: send their team ID or selected team if they have access
      const teamIdToUse = selectedTeamId.value > 0 ? selectedTeamId.value : currentUserTeamId.value;
      params.push(`teamId=${teamIdToUse}`);
      console.log(`Non-admin loading conversations for team: ${teamIdToUse}`);
    }
    
    if (params.length > 0) {
      url += '?' + params.join('&');
    }
    
    console.log('Loading conversations from:', url);
    const response = await api.get(url);
    
    if (response.data && Array.isArray(response.data)) {
      conversations.value = response.data;
      
      conversations.value.sort((a, b) => {
        const dateA = new Date(a.LastMessageAt || a.CreatedAt);
        const dateB = new Date(b.LastMessageAt || b.CreatedAt);
        return dateB.getTime() - dateA.getTime();
      });
      
      console.log(`Loaded ${conversations.value.length} conversations for team filter`);
      
      // Update selected conversation if needed
      if (selectedConversation.value) {
        const stillExists = conversations.value.some(c => c.Id === selectedConversation.value?.Id);
        if (!stillExists) {
          selectedConversation.value = null;
          messages.value = [];
        }
      }
      
      // Auto-select first conversation if none selected
      if (conversations.value.length > 0 && !selectedConversation.value) {
        const validConversation = conversations.value.find(c => c.Id && (c.DriverId || c.IsGroupConversation));
        if (validConversation) {
          await selectConversation(validConversation);
        }
      }
    } else {
      conversations.value = [];
      selectedConversation.value = null;
      messages.value = [];
    }
  } catch (error: any) {
    console.error('Error loading conversations:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Unknown error';
    
    // Don't show permission errors for non-admin users
    if (!errorMessage.includes('access') || isAdmin.value) {
      alert(`Failed to load conversations: ${errorMessage}`);
    }
    
    conversations.value = [];
    selectedConversation.value = null;
    messages.value = [];
  } finally {
    loading.value = false;
  }
};

// UPDATED: sendMessage to include 24-hour window check
const sendMessage = async () => {
  if ((! newMessage.value. trim() && !uploadedFile.value) || !selectedConversation.value || sending.value) {
    return;
  }

  // ✅ CRITICAL FIX: Check 24-hour window BEFORE sending non-template messages
  if (! selectedConversation.value. IsGroupConversation && !selectedConversation.value.canSendNonTemplateMessages) {
    // ✅ Block the message and show error
    alert('❌ Cannot send regular messages outside 24-hour window.\n\nThe 24-hour messaging window only opens when a customer messages you first.\n\nPlease use the Template button to send a message instead.');
    return;
  }

  sending.value = true;
  const messageText = newMessage.value;
  newMessage.value = '';

  try {
    // Determine team ID - CRITICAL FIX
    let teamId: number;
    
    if (isAdmin.value && selectedTeamId.value > 0) {
      // Admin with specific team selected
      teamId = selectedTeamId.value;
    } else if (isAdmin.value && selectedTeamId.value === 0 && selectedConversation.value.TeamId) {
      // Admin with "All Teams" but specific conversation has team
      teamId = selectedConversation.value.TeamId;
    } else {
      // Non-admin or fallback: use user's team
      teamId = currentUserTeamId.value;
    }

    if (!teamId) {
      alert('No team context available for sending message');
      sending.value = false;
      newMessage.value = messageText;
      return;
    }

    console.log(`Sending message for team: ${teamId}`);

    let payload: MessageRequest = {
      Content: messageText,
      IsFromDriver: false,
      ConversationId: selectedConversation.value.Id,
      WhatsAppMessageId: `web_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      MessageType: 'Text',
      TeamId: teamId,
      IsTemplateMessage: false
    };

    if (selectedConversation.value.IsGroupConversation) {
      payload.IsGroupMessage = true;
      payload.GroupId = selectedConversation.value.WhatsAppGroupId;
    } else {
      payload.DriverId = selectedConversation.value.DriverId;
    }

    // Include reply context
    if (replyingToMessage.value) {
      payload.ReplyToMessageId = replyingToMessage.value.Id;
      payload.ReplyToMessageContent = getEnhancedReplyPreview(replyingToMessage.value);
      payload.ReplyToSenderName = getEnhancedReplySenderName(replyingToMessage.value);
    }

    if (uploadedFile.value) {
      const fileData = await uploadFile(uploadedFile.value);
      
      payload = {
        ...payload,
        MessageType: fileData.MessageType,
        MediaUrl: fileData.MediaUrl,
        FileName: fileData.FileName,
        FileSize: fileData.FileSize,
        MimeType: fileData.MimeType,
        Content: messageText || `Sent a ${fileData.MessageType.toLowerCase()}`
      };
    }

    const response = await api.post('/messages', payload);
    
    const sentMessage = response.data;
    messages.value.push(sentMessage);

    // ✅ Refresh window status after sending
    await startWindowStatusPolling();
    
    cancelReply();
    
    await scrollToBottom(false);

    if (!selectedConversation.value.IsAnswered) {
      try {
        await api.put(`/conversations/${selectedConversation.value.Id}/mark-answered`);
        selectedConversation.value.IsAnswered = true;
    
        await loadConversations();
        await loadUnansweredCount();
      } catch (statusError) {
        console.warn('Failed to update conversation status:', statusError);
      }
    }

    uploadedFile.value = null;
    showMediaOptions.value = false;
    showFileInfo.value = false;

  } catch (error:  any) {
    // ✅ ENHANCED: Handle template-required error
    if (error.response?. data?.code === 'TEMPLATE_REQUIRED' || 
        error.response?.data?. message?. includes('TEMPLATE_REQUIRED')) {
      alert('❌ Cannot send regular messages outside 24-hour window.\n\nPlease use a template message instead.');
      openTemplateDialog();
    } else {
      alert(`Failed to send message: ${error.response?.data?.message || error.message}`);
    }
    newMessage.value = messageText;
  } finally {
    sending.value = false;
  }
};


let pollingInterval: number | null = null;

// Start polling for updates
function startPolling() {
  if (pollingInterval) {
    clearInterval(pollingInterval);
  }

  pollingInterval = window.setInterval(async () => {
    // Silently refresh conversations
    try {
      await loadConversations();
      
      // Refresh current conversation messages
      if (selectedConversation.value?.Id) {
        const response = await api.get(`/conversations/${selectedConversation.value.Id}`);
        const newMessages = response.data.Messages || [];
        
        // Only update if there are new messages
        if (newMessages.length > messages.value.length) {
          messages.value = newMessages;
          await scrollToBottom();
        }
      }
    } catch (error) {
      console.error('Polling error:', error);
    }
  }, 5000); // Poll every 5 seconds
}

// Stop polling
function stopPolling() {
  if (pollingInterval) {
    clearInterval(pollingInterval);
    pollingInterval = null;
  }
}

// UPDATED: selectConversation to include window status check
const selectConversation = async (conversation: ConversationDto) => {
  if (!conversation. Id) {
    alert('Invalid conversation data');
    return;
  }

  try {
    messagesLoading.value = true;
    const response = await api.get(`/conversations/${conversation.Id}`);
    
    selectedConversation.value = response.data;
    messages.value = Array.isArray(selectedConversation.value.Messages) 
      ? selectedConversation. value.Messages 
      : [];
    
    console.log(`Loaded ${messages.value.length} messages for conversation ${conversation.Id}`);
    
    // Reset media states when switching conversations
    imageLoadingStates.value = {};
    imageErrors.value = {};
    imageRetryCount.value = {};
    videoLoadingStates.value = {};
    videoErrors.value = {};
    videoRetryCount.value = {};
    audioLoadingStates.value = {};
    audioErrors.value = {};
    audioRetryCount.value = {};
    
    // Reset reply states
    replyingToMessage.value = null;
    highlightedMessageId.value = null;
    
    // Check 24-hour window status for individual conversations
    if (windowStatusPollInterval.value) {
      clearInterval(windowStatusPollInterval);
      windowStatusPollInterval.value = null;
    }

    // ✅ Start polling window status for individual conversations
    if (! selectedConversation.value.IsGroupConversation) {
      startWindowStatusPolling();
    }
    
    await scrollToBottom(false);
  } catch (error: any) {
    alert(`Failed to load conversation: ${error.response?.data?.message || error.message}`);
    selectedConversation.value = null;
    messages.value = [];
  } finally {
    messagesLoading.value = false;
  }
};

// Template dialog method
const openTemplateDialog = () => {
  if (!selectedConversation.value) {
    alert('Please select a conversation first');
    return;
  }
  
  templatePhoneNumber.value = selectedConversation.value.DriverPhone || '';
  showTemplateDialog.value = true;
};

// Lifecycle - UPDATED: Load teams on mount
onMounted(async () => {
  console.log('HomeView mounted - initializing teams and conversations');
  console.log('User roles:', authStore.user?.roles);
  console.log('User team ID:', authStore.user?.teamId);
  
  // Load teams first
  await loadTeams();
  
  // Then load conversations with team context
  await loadConversations();
  startPolling();
  await loadUnansweredCount();
  
  if (isAdminOrManager.value) {
    await loadDepartments();
    await loadUsers();
  }

  // Close context menu when clicking outside
  document.addEventListener('click', closeMessageMenu);
  document.addEventListener('click', closeDropdownOnClickOutside);

   nextTick(() => {
    console.log('🔄 Chat container mounted:', {
      element: chatContainer.value,
      hasScrollEvent: chatContainer.value?.onscroll ? 'YES' : 'NO',
      isScrollable: chatContainer.value?.style.overflowY === 'auto' ? 'YES' : 'NO'
    });
  });
});

// Watch for team changes
watch(selectedTeamId, async (newTeamId) => {
  console.log('Team selection changed to:', newTeamId);
  await loadConversations();
});

onUnmounted(() => {
  stopPolling();
  if (windowStatusPollInterval.value) {
    clearInterval(windowStatusPollInterval.value);
    windowStatusPollInterval.value = null;
  }
  document.removeEventListener('click', closeDropdownOnClickOutside);
});

// Watch for messages changes to preload media
watch(messages, (newMessages) => {
  if (newMessages && newMessages.length > 0) {
    console.log('Messages updated, preloading media...');
    preloadMedia();
  }
}, { deep: true });

watch(
  () => messages.length,
  (newLength, oldLength) => {
    if (newLength > oldLength) {
      // New message arrived
      nextTick(() => {
        if (!chatContainer.value) return;
        
        const { scrollTop, scrollHeight, clientHeight } = chatContainer.value;
        const isNearBottom = scrollHeight - scrollTop - clientHeight < 200;
        
        // Only auto-scroll if user is near the bottom
        if (isNearBottom) {
          scrollToBottom(false); // Instant scroll for new messages
        }
      });
    }
  }
);


// Rest of existing methods remain the same...
const refreshCurrentConversation = async () => {
  if (!selectedConversation.value?.Id) return;
  
  try {
    console.log('Refreshing current conversation...');
    const response = await api.get(`/conversations/${selectedConversation.value.Id}`);
    const updatedConversation = response.data;
    
    selectedConversation.value = {
      ...selectedConversation.value,
      ...updatedConversation,
      Participants: updatedConversation.Participants || []
    };
    
    console.log('Refreshed conversation participants:', selectedConversation.value.Participants?.length);
  } catch (error) {
    console.error('Error refreshing conversation:', error);
    if (selectedConversation.value.Id) {
      await selectConversation({ Id: selectedConversation.value.Id } as ConversationDto);
    }
  }
};

const loadUnansweredCount = async () => {
  try {
    const response = await api.get('/conversations/unanswered/count');
    unansweredCount.value = response.data.count || 0;
  } catch (error: any) {
    console.error('Error loading unanswered count:', error);
    unansweredCount.value = 0;
  }
};

const loadDepartments = async () => {
  try {
    const response = await api.get('/departments');
    departments.value = Array.isArray(response.data) ? response.data : [];
  } catch (error: any) {
    console.error('Error loading departments:', error);
    departments.value = [];
  }
};

const loadUsers = async () => {
  try {
    const response = await api.get('/users');
    users.value = Array.isArray(response.data) ? response.data : [];
  } catch (error: any) {
    console.error('Error loading users:', error);
    users.value = [];
  }
};

const toggleUnansweredFilter = async () => {
  showUnansweredOnly.value = !showUnansweredOnly.value;
  await loadConversations();
};

const toggleGroupsFilter = async () => {
  showGroupsOnly.value = !showGroupsOnly.value;
  await loadConversations();
};

const getConversationDisplayName = (conv: ConversationDto) => {
  if (conv.IsGroupConversation) {
    return conv.GroupName || conv.DriverName || 'Unknown Group';
  }
  return conv.DriverName || 'Unknown Driver';
};

const getConversationSubtitle = (conv: ConversationDto) => {
  if (conv.IsGroupConversation) {
    return conv.WhatsAppGroupId || 'Group Chat';
  }
  return conv.DriverPhone || 'No phone';
};

// Media handling functions
const preloadMedia = () => {
  if (!messages.value) return;
  
  messages.value.forEach(message => {
    if (message.MessageType === 'Image' && message.MediaUrl) {
      preloadImage(message);
    } else if (message.MessageType === 'Video' && message.MediaUrl) {
      preloadVideo(message);
    } else if (message.MessageType === 'Audio' && message.MediaUrl) {
      preloadAudio(message);
    }
  });
};

const preloadImage = (message: MessageDto) => {
  if (!imageLoadingStates.value[message.Id] && !imageErrors.value[message.Id]) {
    imageLoadingStates.value[message.Id] = true;
    imageErrors.value[message.Id] = false;
    
    const img = new Image();
    img.onload = () => {
      imageLoadingStates.value[message.Id] = false;
      imageErrors.value[message.Id] = false;
    };
    img.onerror = () => {
      imageLoadingStates.value[message.Id] = false;
      imageErrors.value[message.Id] = true;
    };
    img.src = getMediaUrl(message.MediaUrl);
  }
};

const preloadVideo = (message: MessageDto) => {
  if (!videoLoadingStates.value[message.Id] && !videoErrors.value[message.Id]) {
    videoLoadingStates.value[message.Id] = true;
    videoErrors.value[message.Id] = false;
    
    const video = document.createElement('video');
    video.onloadeddata = () => {
      videoLoadingStates.value[message.Id] = false;
      videoErrors.value[message.Id] = false;
    };
    video.onerror = () => {
      videoLoadingStates.value[message.Id] = false;
      videoErrors.value[message.Id] = true;
    };
    video.src = getMediaUrl(message.MediaUrl);
  }
};

const preloadAudio = (message: MessageDto) => {
  if (!audioLoadingStates.value[message.Id] && !audioErrors.value[message.Id]) {
    audioLoadingStates.value[message.Id] = true;
    audioErrors.value[message.Id] = false;
    
    const audio = new Audio();
    audio.onloadeddata = () => {
      audioLoadingStates.value[message.Id] = false;
      audioErrors.value[message.Id] = false;
    };
    audio.onerror = () => {
      audioLoadingStates.value[message.Id] = false;
      audioErrors.value[message.Id] = true;
    };
    audio.src = getMediaUrl(message.MediaUrl);
  }
};



const downloadMedia = async (message: MessageDto) => {
  try {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || window.location.origin;
    const downloadUrl = `${baseUrl}/api/messages/download/${message.Id}`;
    
    // Fetch with credentials
    const response = await fetch(downloadUrl, {
      credentials: 'include',
      headers: {
        'Authorization': `Bearer ${authStore.token}`
      }
    });
    
    if (!response.ok) throw new Error('Download failed');
    
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = message.FileName || `whatsapp_media_${message.Id}`;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
    
  } catch (error) {
    console.error('Download failed:', error);
    // Fallback: Open in new tab
    window.open(getMediaUrl(message), '_blank');
  }
};

// Image handling
const handleImageLoad = (messageId: number) => {
  imageLoadingStates.value[messageId] = false;
  imageErrors.value[messageId] = false;
};

const handleImageError = async (messageId: number, event: Event) => {
  console.error('Direct image load failed for message:', messageId);
  imageErrors.value[messageId] = true;
  
  // Try API endpoint as fallback
  const message = messages.value.find(m => m.Id === messageId);
  if (message) {
    const apiUrl = `${import.meta.env.VITE_API_BASE_URL}/api/messages/preview/${messageId}`;
    
    try {
      const response = await fetch(apiUrl);
      if (response.ok) {
        const data = await response.json();
        if (data.url) {
          // Update message with API URL
          const index = messages.value.findIndex(m => m.Id === messageId);
          if (index !== -1) {
            messages.value[index].MediaUrl = data.url;
            // Force re-render
            messages.value = [...messages.value];
            imageErrors.value[messageId] = false;
          }
        }
      }
    } catch (apiError) {
      console.error('API fallback also failed:', apiError);
    }
  }
};

const extractUrl = (text: string): string | null => {
  const urlRegex = /(https?:\/\/[^\s]+)/g;
  const match = text.match(urlRegex);
  return match ? match[0] : null;
};

const isLinkMessage = (message: MessageDto): boolean => {
  return message.Content?.includes('http') || 
         message.MessageType === 'Link' || 
         !!extractUrl(message.Content || '');
};

const retryImageLoad = (message: MessageDto) => {
  imageRetryCount.value[message.Id] = (imageRetryCount.value[message.Id] || 0) + 1;
  imageErrors.value[message.Id] = false;
  imageLoadingStates.value[message.Id] = true;
  
  setTimeout(() => {
    messages.value = [...messages.value];
  }, 100);
};

// Video handling
const handleVideoLoadStart = (messageId: number) => {
  videoLoadingStates.value[messageId] = true;
  videoErrors.value[messageId] = false;
};

const handleVideoLoad = (messageId: number) => {
  videoLoadingStates.value[messageId] = false;
  videoErrors.value[messageId] = false;
};

const handleVideoError = (messageId: number, event: Event) => {
  console.error('Failed to load video for message:', messageId);
  videoLoadingStates.value[messageId] = false;
  videoErrors.value[messageId] = true;
};

const retryVideoLoad = (message: MessageDto) => {
  videoRetryCount.value[message.Id] = (videoRetryCount.value[message.Id] || 0) + 1;
  videoErrors.value[message.Id] = false;
  videoLoadingStates.value[message.Id] = true;
  
  setTimeout(() => {
    messages.value = [...messages.value];
  }, 100);
};

// Audio handling
const handleAudioLoadStart = (messageId: number) => {
  audioLoadingStates.value[messageId] = true;
  audioErrors.value[messageId] = false;
};

const handleAudioLoad = (messageId: number) => {
  audioLoadingStates.value[messageId] = false;
  audioErrors.value[messageId] = false;
};

const handleAudioError = (messageId: number, event: Event) => {
  console.error('Failed to load audio for message:', messageId);
  audioLoadingStates.value[messageId] = false;
  audioErrors.value[messageId] = true;
};

const retryAudioLoad = (message: MessageDto) => {
  audioRetryCount.value[message.Id] = (audioRetryCount.value[message.Id] || 0) + 1;
  audioErrors.value[message.Id] = false;
  audioLoadingStates.value[message.Id] = true;
  
  setTimeout(() => {
    messages.value = [...messages.value];
  }, 100);
};

// File upload handling
const handleFileUpload = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files[0]) {
    const file = target.files[0];
    const fileSizeMB = file.size / (1024 * 1024);
    const fileType = getFileType(file.type);
    
    fileInfo.value = `${fileType}: ${fileSizeMB.toFixed(1)}MB`;
    showFileInfo.value = true;
    
    let maxUploadSize = 0;
    let warningMessage = '';
    
    switch (fileType) {
      case 'Image':
        maxUploadSize = 100;
        if (fileSizeMB > 16) {
          warningMessage = `This image (${fileSizeMB.toFixed(1)}MB) will be automatically compressed. Continue?`;
        }
        break;
      case 'Video':
        maxUploadSize = 500;
        if (fileSizeMB > 16) {
          warningMessage = `This video (${fileSizeMB.toFixed(1)}MB) exceeds WhatsApp's 16MB limit and will be sent as a document. Continue?`;
        }
        break;
      case 'Audio':
        maxUploadSize = 100;
        if (fileSizeMB > 16) {
          warningMessage = `This audio file (${fileSizeMB.toFixed(1)}MB) exceeds WhatsApp's 16MB limit and will be sent as a document. Continue?`;
        }
        break;
      case 'Document':
        maxUploadSize = 100;
        break;
    }
    
    if (fileSizeMB > maxUploadSize) {
      alert(`${fileType} files cannot exceed ${maxUploadSize}MB.\nYour file: ${fileSizeMB.toFixed(1)}MB`);
      target.value = '';
      showFileInfo.value = false;
      return;
    }
    
    if (warningMessage && !confirm(warningMessage)) {
      target.value = '';
      showFileInfo.value = false;
      return;
    }
    
    await uploadFile(file);
    showMediaOptions.value = false;
    target.value = '';
  }
};

const uploadFile = async (file: File) => {
  isUploading.value = true;
  uploadProgress.value = 0;
  uploadStatus.value = 'Preparing upload...';
  
  try {
    const formData = new FormData();
    formData.append('file', file);

    const response = await api.post('/messages/upload-media', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      },
      onUploadProgress: (progressEvent) => {
        if (progressEvent.total) {
          uploadProgress.value = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          uploadStatus.value = `Uploading... ${uploadProgress.value}%`;
        }
      }
    });

    const fileData = response.data;
    
    if (fileData.CompressionInfo) {
      uploadStatus.value = `✓ ${fileData.CompressionInfo}`;
    } else {
      uploadStatus.value = '✓ Upload completed';
    }
    
    uploadedFile.value = new File([file], file.name, { type: file.type });
    
    setTimeout(() => {
      uploadStatus.value = '';
      isUploading.value = false;
      uploadProgress.value = 0;
    }, 5000);
    
    return fileData;
    
  } catch (uploadError: any) {
    console.error('Error uploading file:', uploadError);
    isUploading.value = false;
    uploadStatus.value = '';
    
    const errorData = uploadError.response?.data;
    let errorMessage = 'Failed to upload file';
    
    if (errorData) {
      if (errorData.message) {
        errorMessage = errorData.message;
      }
      if (errorData.suggestion) {
        errorMessage += '\n\n' + errorData.suggestion;
      }
    } else {
      errorMessage += `: ${uploadError.message || 'Unknown error'}`;
    }
    
    alert(errorMessage);
    throw uploadError;
  }
};

// Group management methods
const addEmptyParticipant = () => {
  newGroup.value.Participants.push({
    PhoneNumber: '',
    ParticipantName: '',
    Role: 'member'
  });
};

const removeParticipant = (index: number) => {
  newGroup.value.Participants.splice(index, 1);
};

const addNewParticipant = () => {
  newParticipants.value.push({
    PhoneNumber: '',
    ParticipantName: '',
    Role: 'member'
  });
};

const removeNewParticipant = (index: number) => {
  newParticipants.value.splice(index, 1);
};



const formatDate = (dateString: string): string => {
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
};

const openGroupManagementModal = () => {
  showGroupManagementModal.value = true;
};

const closeGroupManagementModal = () => {
  showGroupManagementModal.value = false;
};

const openAddParticipantsModal = () => {
  newParticipants.value = [{
    PhoneNumber: '',
    ParticipantName: '',
    Role: 'member'
  }];
  showAddParticipantsModal.value = true;
};

const closeAddParticipantsModal = () => {
  showAddParticipantsModal.value = false;
  newParticipants.value = [];
};

const deleteCurrentGroup = async () => {
  if (!selectedConversation.value?.GroupId) {
    alert('Group ID not found. Please refresh and try again.');
    return;
  }

  if (!confirm('Are you sure you want to delete this group? This action cannot be undone.')) {
    return;
  }

  deletingGroup.value = true;
  try {
    await api.delete(`/conversations/groups/${selectedConversation.value.GroupId}`);
    
    showGroupManagementModal.value = false;
    selectedConversation.value = null;
    messages.value = [];
    
    await loadConversations();
    
    alert('Group deleted successfully!');
  } catch (error: any) {
    console.error('Error deleting group:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Failed to delete group';
    alert(`Failed to delete group: ${errorMessage}`);
  } finally {
    deletingGroup.value = false;
  }
};

const removeGroupParticipant = async (participantId: number) => {
  if (!selectedConversation.value?.GroupId) {
    alert('Group ID not found. Please refresh and try again.');
    return;
  }

  if (!confirm('Are you sure you want to remove this participant?')) {
    return;
  }

  try {
    await api.delete(`/conversations/groups/${selectedConversation.value.GroupId}/participants`, {
      data: {
        ParticipantIds: [participantId]
      }
    });
    
    await refreshCurrentConversation();
    
    alert('Participant removed successfully!');
  } catch (error: any) {
    console.error('Error removing participant:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Failed to remove participant';
    alert(`Failed to remove participant: ${errorMessage}`);
  }
};

const addParticipantsToGroup = async () => {
  if (!selectedConversation.value?.GroupId) {
    alert('Group ID not found. Please refresh and try again.');
    return;
  }

  const validParticipants = newParticipants.value.filter(p => p.PhoneNumber && p.PhoneNumber.trim() !== '');
  if (validParticipants.length === 0) {
    alert('Please add at least one valid participant with a phone number');
    return;
  }

  addingParticipants.value = true;
  try {
    const request: AddParticipantsRequest = {
      Participants: validParticipants
    };

    await api.post(`/conversations/groups/${selectedConversation.value.GroupId}/participants`, request);
    
    showAddParticipantsModal.value = false;
    newParticipants.value = [];
    
    setTimeout(async () => {
      await refreshCurrentConversation();
      
      if (selectedConversation.value?.Id) {
        await selectConversation({ Id: selectedConversation.value.Id } as ConversationDto);
      }
      
      alert(`Successfully added ${validParticipants.length} participant(s) to the group!`);
    }, 500);
    
  } catch (error: any) {
    console.error('Error adding participants:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Failed to add participants';
    alert(`Failed to add participants: ${errorMessage}`);
  } finally {
    addingParticipants.value = false;
  }
};

const createGroup = async () => {
  if (!newGroup.value.WhatsAppGroupId || !newGroup.value.Name) {
    alert('WhatsApp Group ID and Name are required');
    return;
  }

  const validParticipants = newGroup.value.Participants.filter(p => p.PhoneNumber.trim() !== '');
  if (validParticipants.length === 0) {
    alert('Please add at least one participant with a phone number');
    return;
  }

  // 🔥 CRITICAL: Determine team ID for new group
  let teamId: number;
  
  if (isAdmin.value) {
    // Admin: use selected team or first available team
    if (selectedTeamId.value > 0) {
      teamId = selectedTeamId.value;
    } else if (teams.value.length > 0) {
      teamId = teams.value[0].id; // Default to first team
    } else {
      throw new Error('No teams available for group creation');
    }
  } else {
    // Non-admin: use their team
    if (!currentUserTeamId.value) {
      throw new Error('User is not assigned to a team');
    }
    teamId = currentUserTeamId.value;
  }

  console.log('Creating group for team:', teamId);

  creatingGroup.value = true;
  try {
    const request: CreateGroupRequest = {
      ...newGroup.value,
      Participants: validParticipants,
      TeamId: teamId // 🔥 Include team ID
    };

    await api.post('/conversations/groups', request);
    
    newGroup.value = {
      WhatsAppGroupId: '',
      Name: '',
      Description: '',
      Participants: []
    };
    
    showCreateGroupModal.value = false;
    await loadConversations();
    
    alert('Group created successfully with ' + validParticipants.length + ' participants!');
  } catch (error: any) {
    console.error('Error creating group:', error);
    alert(`Failed to create group: ${error.response?.data?.message || error.message || 'Failed to create group'}`);
  } finally {
    creatingGroup.value = false;
  }
};

// Contact Management Methods
const openCreateContactModal = () => {
  showCreateContactModal.value = true;
};

const closeCreateContactModal = () => {
  showCreateContactModal.value = false;
  newContact.value = {
    Name: '',
    PhoneNumber: '',
    FirstName: '',
    LastName: ''
  };
};

const createNewContact = async () => {
  if (!isValidContact.value) {
    alert('Name and Phone Number are required');
    return;
  }

  creatingContact.value = true;
  try {
    let fullName = newContact.value.Name;
    if (newContact.value.FirstName || newContact.value.LastName) {
      fullName = `${newContact.value.FirstName || ''} ${newContact.value.LastName || ''}`.trim();
    }

    // 🔥 CRITICAL: Determine team ID for new contact - FIXED LOGIC
    let teamId: number;
    
    if (isAdmin.value) {
      // Admin: use selected team or user's team
      if (selectedTeamId.value > 0) {
        teamId = selectedTeamId.value;
      } else if (authStore.userTeamId) {
        teamId = authStore.userTeamId;
      } else if (teams.value.length > 0) {
        teamId = teams.value[0].id; // Default to first team
      } else {
        throw new Error('No teams available for contact creation');
      }
    } else {
      // Non-admin: use their team
      if (!authStore.userTeamId) {
        throw new Error('User is not assigned to a team');
      }
      teamId = authStore.userTeamId;
    }

    console.log('Creating contact for team:', teamId);

    const contactData = {
      Name: fullName,
      PhoneNumber: newContact.value.PhoneNumber,
      IsActive: true,
      TeamId: teamId // 🔥 Include team ID
    };

    const response = await api.post('/drivers', contactData);
    
    await loadConversations();
    
    closeCreateContactModal();
    alert('Contact created successfully!');
    
    const newDriverId = response.data.id;
    if (newDriverId) {
      // Wait a moment for the conversation to be created
      await new Promise(resolve => setTimeout(resolve, 1000));
      
      await loadConversations();
      
      // Try to find and select the new conversation
      const newConversation = conversations.value.find(c => 
        c.DriverId === newDriverId || 
        c.DriverPhone === newContact.value.PhoneNumber
      );
      
      if (newConversation) {
        await selectConversation(newConversation);
      }
    }
  } catch (error: any) {
    console.error('Error creating contact:', error);
    alert(`Failed to create contact: ${error.response?.data?.message || error.message || 'Failed to create contact'}`);
  } finally {
    creatingContact.value = false;
  }
};

// Delete Contact Method
const deleteCurrentContact = async () => {
  if (!selectedConversation.value || !selectedConversation.value.DriverId) {
    alert('No contact selected or contact does not have an ID');
    return;
  }

  if (!confirm('Are you sure you want to delete this contact? This will also delete all conversations and messages with this contact.')) {
    return;
  }

  try {
    await api.delete(`/drivers/${selectedConversation.value.DriverId}`);
    
    selectedConversation.value = null;
    messages.value = [];
    await loadConversations();
    
    alert('Contact deleted successfully!');
  } catch (error: any) {
    console.error('Error deleting contact:', error);
    alert(`Failed to delete contact: ${error.response?.data?.message || error.message || 'Failed to delete contact'}`);
  }
};

// ENHANCED: Improved message placeholder with staff context
const getMessagePlaceholder = (): string => {
  if (replyingToMessage.value) {
    const senderName = getEnhancedSenderName(replyingToMessage.value);
    return `Replying to ${senderName}...`;
  }
  return selectedConversation.value?.IsGroupConversation 
    ? 'Type a message to the group...' 
    : 'Type a message...';
};

// ENHANCED: Reply functionality with message navigation
const startReply = (message: MessageDto) => {
  replyingToMessage.value = message;
  nextTick(() => {
    const input = document.querySelector('input[type="text"]') as HTMLInputElement;
    input?.focus();
  });
};

const cancelReply = () => {
  replyingToMessage.value = null;
};

// Enhanced sender name function
const getEnhancedSenderName = (message: MessageDto): string => {
  if (!message.IsFromDriver) {
    return message.SentByUserName || message.SenderName || 'Staff';
  }
  
  if (message.IsGroupMessage) {
    return message.SenderName || message.SenderPhoneNumber || 'Driver';
  }
  
  return selectedConversation.value?.DriverName || 'Driver';
};

// Enhanced reply sender name
const getEnhancedReplySenderName = (message: MessageDto): string => {
  if (message.ReplyToSenderName) {
    return message.ReplyToSenderName;
  }
  
  if (message.ReplyToMessage) {
    return getEnhancedSenderName(message.ReplyToMessage);
  }
  
  if (message.ReplyToMessageId) {
    const repliedMessage = messages.value.find(m => m.Id === message.ReplyToMessageId);
    if (repliedMessage) {
      return getEnhancedSenderName(repliedMessage);
    }
  }
  
  return 'Unknown';
};

// Enhanced reply preview
const getEnhancedReplyPreview = (message: MessageDto): string => {
  const senderInfo = getEnhancedSenderName(message);
  let contentPreview = '';
  
  if (message.MessageType === 'Image') contentPreview = '📷 Image';
  else if (message.MessageType === 'Video') contentPreview = '🎥 Video';
  else if (message.MessageType === 'Audio') contentPreview = '🎵 Audio';
  else if (message.MessageType === 'Document') contentPreview = '📄 Document';
  else if (message.MessageType === 'Location') contentPreview = '📍 Location';
  else contentPreview = message.Content.length > 50 ? message.Content.substring(0, 50) + '...' : message.Content;
  
  return `${senderInfo}: ${contentPreview}`;
};

// Simple reply preview for the reply context bar
const getReplyPreview = (message: MessageDto): string => {
  let contentPreview = '';
  
  if (message.MessageType === 'Image') contentPreview = '📷 Image';
  else if (message.MessageType === 'Video') contentPreview = '🎥 Video';
  else if (message.MessageType === 'Audio') contentPreview = '🎵 Audio';
  else if (message.MessageType === 'Document') contentPreview = '📄 Document';
  else if (message.MessageType === 'Location') contentPreview = '📍 Location';
  else contentPreview = message.Content.length > 30 ? message.Content.substring(0, 30) + '...' : message.Content;
  
  return contentPreview;
};

// Utility methods
const toggleMediaOptions = () => {
  showMediaOptions.value = !showMediaOptions.value;
};

const openMediaGallery = () => {
  if (!selectedConversation.value) return;
  
  selectedConversationForMedia.value = {
    id: selectedConversation.value.Id,
    name: selectedConversation.value.DriverName,
    isGroup: selectedConversation.value.IsGroupConversation
  };
  showMediaGallery.value = true;
};

const closeMediaGallery = () => {
  showMediaGallery.value = false;
  selectedConversationForMedia.value = null;
};

const openGallery = () => {
  if (fileInput.value) {
    fileInput.value.accept = 'image/*,video/*,audio/*,.pdf,.doc,.docx,.txt,.zip,.rar,.7z,.tar,.gz,.xls,.xlsx,.ppt,.pptx';
    fileInput.value.click();
  }
};

const sendLocation = async () => {
  if (!selectedConversation.value) return;

  if (!navigator.geolocation) {
    alert('Geolocation is not supported by this browser');
    return;
  }

  navigator.geolocation.getCurrentPosition(
    async (position) => {
      const location = `${position.coords.latitude},${position.coords.longitude}`;
      
      try {
        const payload: any = {
          Content: 'Location shared',
          MessageType: 'Location',
          Location: location,
          ConversationId: selectedConversation.value.Id,
          IsFromDriver: false,
          WhatsAppMessageId: `web_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
          IsTemplateMessage: false
        };

        if (selectedConversation.value.IsGroupConversation) {
          payload.IsGroupMessage = true;
          payload.GroupId = selectedConversation.value.WhatsAppGroupId;
        } else {
          payload.DriverId = selectedConversation.value.DriverId;
        }

        const response = await api.post('/messages', payload);
        messages.value.push(response.data);
        await scrollToBottom();
        
        showMediaOptions.value = false;
      } catch (error) {
        console.error('Error sending location:', error);
        alert('Failed to send location');
      }
    },
    (error) => {
      alert('Unable to retrieve your location');
      console.error('Geolocation error:', error);
    }
  );
};

const sendDocument = () => {
  if (fileInput.value) {
    fileInput.value.accept = '.pdf,.doc,.docx,.txt,.rtf,.xls,.xlsx,.ppt,.pptx,.zip,.rar,.7z,.tar,.gz';
    fileInput.value.click();
  }
};

const getFileType = (mimeType: string) => {
  if (mimeType.startsWith('image/')) return 'Image';
  if (mimeType.startsWith('video/')) return 'Video';
  if (mimeType.startsWith('audio/')) return 'Audio';
  return 'Document';
};

const openAssignModal = () => {
  if (!selectedConversation.value) return;
  assignData.value = {
    ConversationId: selectedConversation.value.Id,
    DepartmentId: selectedConversation.value.DepartmentId || null,
    AssignedToUserId: selectedConversation.value.AssignedToUserId || null
  };
  showAssignModal.value = true;
};

const closeAssignModal = () => {
  showAssignModal.value = false;
};

const saveAssignment = async () => {
  if (!assignData.value.ConversationId) return;
  
  assigning.value = true;
  try {
    await api.put(`/conversations/${assignData.value.ConversationId}/assign`, assignData.value);
    
    await loadConversations();
    if (selectedConversation.value) {
      const response = await api.get(`/conversations/${selectedConversation.value.Id}`);
      selectedConversation.value = response.data;
    }
    
    closeAssignModal();
  } catch (error: any) {
    console.error('Error assigning conversation:', error);
    alert(`Failed to assign: ${error.response?.data?.message || error.message || 'Failed to assign'}`);
  } finally {
    assigning.value = false;
  }
};

const scrollToBottom = async (smooth = true) => {
  await nextTick();
  if (chatContainer.value) {
    isAutoScrolling.value = true;
    chatContainer.value.scrollTo({
      top: chatContainer.value.scrollHeight,
      behavior: smooth ? 'smooth' : 'auto'
    });
    
    // Hide button after scrolling
    setTimeout(() => {
      showScrollToBottomButton.value = false;
      isAutoScrolling.value = false;
    }, 300);
  }
};

// Handle scroll events to show/hide scroll-to-bottom button
const handleScroll = () => {
  console.log('🎯 SCROLL EVENT FIRED!'); // ADD THIS LINE
  
  if (isAutoScrolling.value) return;
  
  if (!chatContainer.value) return;

  const { scrollTop, scrollHeight, clientHeight } = chatContainer.value;
  
  // Calculate distance from bottom
  const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
  
  // Show button if user is more than 100px from bottom
  // AND if there are enough messages to scroll
  const shouldShowButton = distanceFromBottom > 100 && scrollHeight > clientHeight;
  
  console.log('📊 Scroll stats:', {  // ADD THIS LINE
    distanceFromBottom,
    scrollHeight,
    clientHeight,
    shouldShowButton
  });
  
  // Only update if value changed to avoid unnecessary re-renders
  if (showScrollToBottomButton.value !== shouldShowButton) {
    showScrollToBottomButton.value = shouldShowButton;
  }
};


const shouldShowDateSeparator = (message: MessageDto, index: number) => {
  if (index === 0) return true;
  
  const currentDate = new Date(message.SentAt).toDateString();
  const prevDate = new Date(messages.value[index - 1].SentAt).toDateString();
  
  return currentDate !== prevDate;
};

const formatMessageDate = (dateString: string) => {
  const date = new Date(dateString);
  const today = new Date();
  const yesterday = new Date(today);
  yesterday.setDate(yesterday.getDate() - 1);

  if (date.toDateString() === today.toDateString()) {
    return 'Today';
  } else if (date.toDateString() === yesterday.toDateString()) {
    return 'Yesterday';
  } else {
    return date.toLocaleDateString('en-US', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }
};

const formatMessageTime = (dateString: string) => {
  const date = new Date(dateString);
  return date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: true
  });
};



const sendQuickReply = (reply: string) => {
  if (!selectedConversation.value) return;
  newMessage.value = reply;
  sendMessage();
};

const toggleAnsweredStatus = async () => {
  if (!selectedConversation.value) return;
  
  try {
    const newStatus = !selectedConversation.value.IsAnswered;
    
    if (newStatus) {
      await api.put(`/conversations/${selectedConversation.value.Id}/mark-answered`);
    } else {
      await api.put(`/conversations/${selectedConversation.value.Id}/mark-unanswered`);
    }
    
    selectedConversation.value.IsAnswered = newStatus;
    await loadConversations();
    await loadUnansweredCount();
  } catch (error: any) {
    console.error('Error toggling answered status:', error);
    alert(`Failed to update status: ${error.response?.data?.message || error.message || 'Failed to update'}`);
  }
};

const showFileSizeLimits = () => {
  alert(`File Upload Limits:\n\n📷 Images: Up to 100MB (auto-compressed to 5MB)\n🎥 Videos: Up to 500MB (over 16MB sent as documents)\n🎵 Audio: Up to 100MB (over 16MB sent as documents)\n📄 Documents: Up to 100MB\n\nLarge files are automatically processed for WhatsApp compatibility.`);
};

const logout = () => {
  authStore.logout();
  router.push('/login');
};

const openImageModal = (imageUrl: string) => {
  selectedImage.value = imageUrl;
  showImageModal.value = true;
};

const closeImageModal = () => {
  showImageModal.value = false;
  selectedImage.value = '';
};

const isDefaultImageCaption = (caption: string) => {
  const defaultCaptions = ['Image', 'image', '📷 Image', 'Sent an image'];
  return defaultCaptions.includes(caption);
};

const closeCreateGroupModal = () => {
  showCreateGroupModal.value = false;
  newGroup.value = {
    WhatsAppGroupId: '',
    Name: '',
    Description: '',
    Participants: []
  };
};

// WhatsApp Message Interaction Methods - FIXED VERSION
const openMessageMenu = (message: MessageDto, event: MouseEvent) => {
  event.preventDefault();
  selectedMessageForMenu.value = message;
  menuPosition.value = { x: event.clientX, y: event.clientY };
  showMessageMenu.value = true;
};

const closeMessageMenu = () => {
  showMessageMenu.value = false;
  selectedMessageForMenu.value = null;
};

const copyMessage = async (message: MessageDto) => {
  try {
    if (message.MessageType === 'Text') {
      await navigator.clipboard.writeText(message.Content);
      alert('Message copied to clipboard!');
    } else {
      const mediaDescription = getMediaDescription(message);
      await navigator.clipboard.writeText(mediaDescription);
      alert('Media description copied to clipboard!');
    }
    closeMessageMenu();
  } catch (error) {
    console.error('Failed to copy message:', error);
    alert('Failed to copy message');
  }
};

const getMediaDescription = (message: MessageDto): string => {
  switch (message.MessageType) {
    case 'Image':
      return `📷 Image: ${message.Content || 'Image'}`;
    case 'Video':
      return `🎥 Video: ${message.Content || 'Video'}`;
    case 'Audio':
      return `🎵 Audio: ${message.Content || 'Audio'}`;
    case 'Document':
      return `📄 Document: ${message.FileName || 'Document'}`;
    case 'Location':
      return `📍 Location: ${message.Content || 'Location'}`;
    default:
      return message.Content;
  }
};

const forwardMessage = async (message: MessageDto) => {
  console.log('🚀 Starting forward process for message:', message.Id, message.Content);
  
  if (!message) {
    console.error('❌ No message provided for forwarding');
    alert('No message selected for forwarding');
    return;
  }
  
  // Store the message in a separate variable that won't be cleared by context menu closing
  messageToForward.value = message;
  customForwardMessage.value = '';
  selectedForwardConversations.value = [];
  loadingAvailableConversations.value = true;
  
  try {
    console.log('📥 Loading available conversations for forwarding...');
    
    // Load all conversations for forwarding
    const response = await api.get('/conversations');
    console.log('✅ Loaded conversations:', response.data);
    
    if (!response.data || !Array.isArray(response.data)) {
      console.error('❌ Invalid conversations data:', response.data);
      throw new Error('Invalid conversations data received');
    }
    
    // Filter out the current conversation and ensure we have valid conversations
    const currentConvId = selectedConversation.value?.Id;
    availableConversations.value = response.data.filter((conv: ConversationDto) => {
      const isValid = conv.Id && conv.Id !== currentConvId;
      console.log(`📞 Conversation ${conv.Id} - ${conv.DriverName}: ${isValid ? 'valid' : 'invalid (current)'}`);
      return isValid;
    });
    
    console.log(`✅ Filtered ${availableConversations.value.length} available conversations`);
    
    if (availableConversations.value.length === 0) {
      console.warn('⚠️ No available conversations for forwarding');
      alert('No other conversations available for forwarding. Please create another conversation first.');
      closeMessageMenu();
      return;
    }

    showForwardModal.value = true;
    console.log('✅ Forward modal opened with', availableConversations.value.length, 'available conversations');
    
  } catch (error: any) {
    console.error('❌ Error loading conversations for forwarding:', error);
    const errorMessage = error.response?.data?.message || error.message || 'Unknown error';
    alert(`Failed to load conversations: ${errorMessage}`);
    closeMessageMenu();
  } finally {
    loadingAvailableConversations.value = false;
  }
};

const isConversationSelected = (conversationId: number): boolean => {
  return selectedForwardConversations.value.includes(conversationId);
};

// Toggle conversation selection for forwarding
const toggleForwardConversation = (conversation: ConversationDto) => {
  if (!conversation?.Id) {
    console.warn('⚠️ Invalid conversation:', conversation);
    return;
  }
  
  const conversationId = conversation.Id;
  const index = selectedForwardConversations.value.indexOf(conversationId);
  
  if (index === -1) {
    // Add to selection
    selectedForwardConversations.value.push(conversationId);
    console.log(`✅ Added conversation ${conversationId} to selection`);
  } else {
    // Remove from selection
    selectedForwardConversations.value.splice(index, 1);
    console.log(`❌ Removed conversation ${conversationId} from selection`);
  }
  
  console.log('📋 Selected conversations:', selectedForwardConversations.value);
};

const cancelForward = () => {
  console.log('❌ Forward operation cancelled');
  showForwardModal.value = false;
  selectedForwardConversations.value = [];
  customForwardMessage.value = '';
  forwardingMessage.value = false;
  messageToForward.value = null; // Clear the forwarded message
};

// Execute the forward operation - UPDATED VERSION
const executeForward = async () => {
  console.log('🚀 Executing forward operation...');
  console.log('📋 Selected conversations:', selectedForwardConversations.value);
  console.log('💬 Custom message:', customForwardMessage.value);
  
  // Use messageToForward instead of selectedMessageForMenu
  if (!messageToForward.value) {
    console.error('❌ No message selected for forwarding');
    alert('No message selected for forwarding. Please try again.');
    return;
  }

  if (selectedForwardConversations.value.length === 0) {
    console.error('❌ No conversations selected');
    alert('Please select at least one conversation to forward the message to.');
    return;
  }

  const messageId = messageToForward.value.Id;
  console.log(`📨 Forwarding message ${messageId} to ${selectedForwardConversations.value.length} conversations`);

  forwardingMessage.value = true;

  try {
    const request: ForwardMessageRequest = {
      ConversationIds: selectedForwardConversations.value,
      CustomMessage: customForwardMessage.value.trim() || undefined
    };

    console.log('📤 Sending forward request:', request);

    const response = await api.post(`/messages/${messageId}/forward`, request);
    
    console.log('✅ Forward response received:', response.data);
    
    // Show success message
    const forwardedCount = response.data.forwardedMessages?.length || selectedForwardConversations.value.length;
    alert(`✅ Message successfully forwarded to ${forwardedCount} conversation(s)!`);
    
    // Close modal and reset state
    showForwardModal.value = false;
    selectedForwardConversations.value = [];
    customForwardMessage.value = '';
    messageToForward.value = null; // Clear the forwarded message
    
    // Refresh current conversation to show any forwarded messages (if current convo was included)
    if (selectedConversation.value) {
      console.log('🔄 Refreshing current conversation...');
      await selectConversation(selectedConversation.value);
    }
    
  } catch (error: any) {
    console.error('❌ Error forwarding message:', error);
    
    let errorMessage = 'Failed to forward message';
    if (error.response?.data) {
      errorMessage = error.response.data.message || errorMessage;
      if (error.response.data.error) {
        errorMessage += `: ${error.response.data.error}`;
      }
      if (error.response.data.details) {
        errorMessage += `\n\nDetails: ${error.response.data.details}`;
      }
    } else {
      errorMessage += `: ${error.message || 'Unknown error'}`;
    }
    
    alert(errorMessage);
    
  } finally {
    forwardingMessage.value = false;
  }
};

const getMessagePreview = (message: MessageDto): string => {
  if (!message) return '';
  
  switch (message.MessageType) {
    case 'Text':
      return message.Content.length > 50 
        ? message.Content.substring(0, 50) + '...' 
        : message.Content;
    case 'Image':
      return '📷 Image' + (message.Content ? `: ${message.Content}` : '');
    case 'Video':
      return '🎥 Video' + (message.Content ? `: ${message.Content}` : '');
    case 'Audio':
      return '🎵 Audio' + (message.Content ? `: ${message.Content}` : '');
    case 'Document':
      return `📄 ${message.FileName || 'Document'}`;
    case 'Location':
      return '📍 Location';
    default:
      return message.Content || 'Message';
  }
};

const toggleStarMessage = async (message: MessageDto) => {
  try {
    const request: StarMessageRequest = {
      IsStarred: !message.IsStarred
    };

    await api.put(`/messages/${message.Id}/star`, request);
    
    // Update the message in the local state
    const messageIndex = messages.value.findIndex(m => m.Id === message.Id);
    if (messageIndex !== -1) {
      messages.value[messageIndex].IsStarred = !message.IsStarred;
    }
    
    closeMessageMenu();
  } catch (error: any) {
    console.error('Error starring message:', error);
    alert(`Failed to update star status: ${error.response?.data?.message || error.message}`);
  }
};

const togglePinMessage = async (message: MessageDto) => {
  try {
    const request: PinMessageRequest = {
      IsPinned: !message.IsPinned
    };

    await api.put(`/messages/${message.Id}/pin`, request);
    
    // Update the message in the local state
    const messageIndex = messages.value.findIndex(m => m.Id === message.Id);
    if (messageIndex !== -1) {
      messages.value[messageIndex].IsPinned = !message.IsPinned;
      messages.value[messageIndex].PinnedAt = message.IsPinned ? undefined : new Date().toISOString();
    }
    
    closeMessageMenu();
  } catch (error: any) {
    console.error('Error pinning message:', error);
    alert(`Failed to update pin status: ${error.response?.data?.message || error.message}`);
  }
};

const deleteMessage = async (message: MessageDto) => {
  if (!confirm('Are you sure you want to delete this message? This action cannot be undone.')) {
    return;
  }

  try {
    await api.delete(`/messages/${message.Id}`);
    
    // Remove the message from the local state instead of just marking as deleted
    const messageIndex = messages.value.findIndex(m => m.Id === message.Id);
    if (messageIndex !== -1) {
      messages.value.splice(messageIndex, 1);
    }
    
    closeMessageMenu();
    alert('Message deleted successfully!');
  } catch (error: any) {
    console.error('Error deleting message:', error);
    alert(`Failed to delete message: ${error.response?.data?.message || error.message}`);
  }
};

const showMessageInfo = async (message: MessageDto) => {
  try {
    const response = await api.get(`/messages/${message.Id}/info`);
    messageInfo.value = response.data;
    showInfoModal.value = true;
  } catch (error: any) {
    console.error('Error getting message info:', error);
    alert(`Failed to get message info: ${error.response?.data?.message || error.message}`);
  }
  closeMessageMenu();
};

const reactToMessage = async (message: MessageDto, reaction: string) => {
  try {
    const request: ReactToMessageRequest = {
      Reaction: reaction
    };

    await api.post(`/messages/${message.Id}/react`, request);
    
    // Refresh the message to get updated reactions
    await refreshMessage(message.Id);
  } catch (error: any) {
    console.error('Error reacting to message:', error);
  }
};

const refreshMessage = async (messageId: number) => {
  try {
    const response = await api.get(`/messages/${messageId}`);
    const updatedMessage = response.data;
    
    const index = messages.value.findIndex(m => m.Id === messageId);
    if (index !== -1) {
      messages.value[index] = { ...messages.value[index], ...updatedMessage };
    }
  } catch (error) {
    console.error('Error refreshing message:', error);
  }
};

const updateMessageStatus = async (message: MessageDto, status: string) => {
  try {
    const request: UpdateMessageStatusRequest = {
      Status: status
    };

    await api.put(`/messages/${message.Id}/status`, request);
    
    message.Status = status;
  } catch (error) {
    console.error('Error updating message status:', error);
  }
};



const simulateMessageStatus = (message: MessageDto) => {
  if (message.IsFromDriver || message.Status === 'Read') return;

  setTimeout(() => {
    if (message.Status === 'Sent') {
      updateMessageStatus(message, 'Delivered');
    }
  }, 1000 + Math.random() * 2000);

  setTimeout(() => {
    if (message.Status === 'Delivered') {
      updateMessageStatus(message, 'Read');
    }
  }, 3000 + Math.random() * 3000);
};

// Helper methods for message interactions
const getStatusColor = (status: string): string => {
  switch (status) {
    case 'Read': return 'text-green-600';
    case 'Delivered': return 'text-blue-600';
    case 'Sent': return 'text-gray-600';
    default: return 'text-gray-600';
  }
};

const formatRelativeTime = (dateString: string): string => {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  
  return date.toLocaleDateString();
};

// Enhanced: Reply navigation with WhatsApp-like behavior
const scrollToRepliedMessage = async (messageId: number) => {
  console.log(`Attempting to scroll to original message: ${messageId}`);
  
  await nextTick();
  await new Promise(resolve => setTimeout(resolve, 100));
  
  const messageElement = document.getElementById(`message-${messageId}`);
  if (messageElement) {
    console.log(`Found original message element: message-${messageId}`);
    
    document.querySelectorAll('.message-container.highlighted').forEach(el => {
      el.classList.remove('highlighted');
    });
    
    messageElement.classList.add('highlighted');
    highlightedMessageId.value = messageId;
    
    messageElement.scrollIntoView({ 
      behavior: 'smooth', 
      block: 'center',
      inline: 'nearest'
    });
    
    setTimeout(() => {
      messageElement.classList.add('highlight-pulse');
    }, 300);
    
    setTimeout(() => {
      messageElement.classList.remove('highlighted', 'highlight-pulse');
      highlightedMessageId.value = null;
    }, 3000);
    
  } else {
    console.warn(`Original message element with id message-${messageId} not found`);
    
    const targetMessage = messages.value.find(m => m.Id === messageId);
    if (targetMessage) {
      console.log('Original message exists in data but not in DOM. The message might be outside current view.');
      
      const allMessageElements = document.querySelectorAll('[id^="message-"]');
      console.log(`Found ${allMessageElements.length} message elements in DOM`);
      
      await scrollToBottom();
      await new Promise(resolve => setTimeout(resolve, 800));
      
      const retryElement = document.getElementById(`message-${messageId}`);
      if (retryElement) {
        retryElement.classList.add('highlighted');
        highlightedMessageId.value = messageId;
        retryElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        
        setTimeout(() => {
          retryElement.classList.add('highlight-pulse');
        }, 300);
        
        setTimeout(() => {
          retryElement.classList.remove('highlighted', 'highlight-pulse');
          highlightedMessageId.value = null;
        }, 3000);
      } else {
        console.error('Could not find the original message even after scrolling');
        alert('Could not find the original message. It might be from an older conversation.');
      }
    }
  }
};

</script>

<style scoped>
/* Custom scrollbar */
.overflow-y-auto::-webkit-scrollbar {
  width: 6px;
}

.overflow-y-auto::-webkit-scrollbar-track {
  background: transparent;
}


.flex-1.flex-col.bg-gray-50 {
  min-height: 0 !important;
  height: 100%;
}

.flex-1.min-h-0.overflow-y-auto {
  min-height: 0 !important;
  flex: 1 1 0% !important;
 
}

/* Ensure parent has fixed height */
.h-\[calc\(100vh-180px\)\] {
  min-height: 0;
}

/* Custom scrollbar styling */
.custom-scrollbar::-webkit-scrollbar {
  width: 8px;
}

/* EMERGENCY: Force WhatsApp Web layout */
.h-screen.flex-col.bg-white {
  background: linear-gradient(180deg, #00a884 0%, #00a884 130px, #f0f2f5 130px, #f0f2f5 100%) !important;
}

/* WhatsApp exact colors */
.bg-gray-50 {
  background-color: #f0f2f5 !important;
}

.bg-white {
  background-color: #ffffff !important;
}

.bg-green-500 {
  background-color: #10b981 !important;
}

.border-gray-200 {
  border-color: #e9edef !important;
}

/* Remove all rounded corners for WhatsApp Web */
.rounded-lg,
.rounded-full,
.rounded,
.rounded-md {
  border-radius: 8px !important;
}

/* WhatsApp Web exact chat container */
.max-w-screen-2xl.mx-auto {
  max-width: 100% !important;
  margin-left: 0 !important;
  margin-right: 0 !important;
  border-radius: 0 !important;
}

/* Full height for sidebar */
.w-\[360px\] {
  height: 100vh !important;
  border-right: 1px solid #e9edef !important;
}

.custom-scrollbar::-webkit-scrollbar-track {
  background: #f1f1f1;
  border-radius: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #a8a8a8;
}

.overflow-y-auto::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.15);
  border-radius: 3px;
}

.overflow-y-auto::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.25);
}

/* Smooth transitions */
.transition-all {
  transition: all 0.3s ease;
}

/* Message animations */
.message-enter-active,
.message-leave-active {
  transition: all 0.3s ease;
}

.message-enter-from {
  opacity: 0;
  transform: translateY(10px);
}

.message-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

/* WhatsApp Message Interaction Styles */

/* Message context menu animations */
.fixed {
  animation: fadeIn 0.1s ease-out;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: scale(0.95);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

/* Message hover effects */
.message-container {
  transition: all 0.2s ease;
}

.message-container:hover {
  background: rgba(0, 0, 0, 0.02);
}

/* Reaction buttons */
.group:hover .group-hover\:opacity-100 {
  opacity: 1;
}

.message-container .bg-green-500 {
  border-radius: 7.5px 7.5px 0 7.5px !important;
}

.message-container .bg-white {
  border-radius: 7.5px 7.5px 7.5px 0 !important;
}

.action-dropdown-container {
  position: relative;
}

/* Status icons */
.status-sent { color: #6b7280; }
.status-delivered { color: #3b82f6; }
.status-read { color: #10b981; }

/* Custom scrollbar for modals */
.modal-scroll {
  scrollbar-width: thin;
  scrollbar-color: #cbd5e0 #f7fafc;
}

.modal-scroll::-webkit-scrollbar {
  width: 6px;
}

.modal-scroll::-webkit-scrollbar-track {
  background: #f7fafc;
  border-radius: 3px;
}

.modal-scroll::-webkit-scrollbar-thumb {
  background: #cbd5e0;
  border-radius: 3px;
}

.modal-scroll::-webkit-scrollbar-thumb:hover {
  background: #a0aec0;
}

/* Message deletion animation */
.message-deleting {
  animation: slideOut 0.3s ease-out forwards;
}

@keyframes slideOut {
  from {
    opacity: 1;
    transform: translateX(0);
    max-height: 200px;
  }
  to {
    opacity: 0;
    transform: translateX(-100%);
    max-height: 0;
    margin: 0;
    padding: 0;
  }
}

/* ENHANCED: WhatsApp-like message highlighting for reply navigation */
.message-container {
  transition: all 0.3s ease;
  position: relative;
}

.message-container.highlighted {
  animation: highlight-pulse 2s ease-in-out 3;
  border-radius: 12px;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.15) 0%, rgba(147, 197, 253, 0.25) 100%);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.5), 0 4px 12px rgba(59, 130, 246, 0.3);
  transform: scale(1.02);
  z-index: 10;
  margin: 8px 0;
  padding: 8px;
  border: 2px solid #3b82f6;
}

/* WhatsApp-like multiple pulse effect */
.message-container.highlight-pulse {
  animation: whatsapp-pulse 0.5s ease-in-out 3;
}

@keyframes highlight-pulse {
  0% {
    box-shadow: 0 0 0 0 rgba(59, 130, 246, 0.7);
    transform: scale(1.02);
  }
  50% {
    box-shadow: 0 0 0 12px rgba(59, 130, 246, 0);
    transform: scale(1.03);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(59, 130, 246, 0);
    transform: scale(1.02);
  }
}

/* WhatsApp-style multiple pulses */
@keyframes whatsapp-pulse {
  0% {
    box-shadow: 0 0 0 0 rgba(59, 130, 246, 0.4);
  }
  70% {
    box-shadow: 0 0 0 10px rgba(59, 130, 246, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(59, 130, 246, 0);
  }
}

/* Enhanced reply context styling */
.reply-context {
  cursor: pointer;
  transition: all 0.2s ease;
  border-left: 4px solid #3b82f6;
}

.reply-context:hover {
  background: rgba(59, 130, 246, 0.1);
  transform: translateX(4px);
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.2);
}

/* Staff badge styling */
.staff-badge {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  font-size: 0.7em;
  padding: 2px 6px;
  border-radius: 10px;
  font-weight: bold;
}

.driver-badge {
  background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
  color: white;
  font-size: 0.7em;
  padding: 2px 6px;
  border-radius: 10px;
  font-weight: bold;
}

/* Loading animation */
.animate-spin {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* Image hover effects */
img {
  transition: transform 0.2s ease;
}

img:hover {
  transform: scale(1.02);
}

/* Button hover effects */
button {
  transition: all 0.15s ease-in-out;
}

/* Mobile responsiveness */
@media (max-width: 768px) {
  /* Full-width sidebar on mobile */
  .w-\[360px\] {
    width: 100% !important;
  }
  
  /* Hide chat on mobile when sidebar is active */
  .flex-1.flex-col {
    display: none;
  }
  
  /* Show chat when conversation is selected */
  .flex-1.flex-col.active {
    display: flex;
  }
}

/* Avatar gradient styles */
.bg-gradient-to-br {
  background-image: linear-gradient(to bottom right, var(--tw-gradient-stops));
}

.from-green-400 {
  --tw-gradient-from: #4ade80;
  --tw-gradient-stops: var(--tw-gradient-from), var(--tw-gradient-to, rgba(74, 222, 128, 0));
}

.to-green-600 {
  --tw-gradient-to: #16a34a;
}

.text-blue-600 {
  color: #2563eb;
}

.text-blue-600:hover {
  color: #1d4ed8;
}

.underline {
  text-decoration: underline;
}

/* Ensure links are properly spaced */
.text-sm.break-words a {
  word-break: break-all;
  margin: 0 2px;
}

/* Download button styling */
.bg-blue-500 {
  background-color: #3b82f6;
}

.bg-blue-500:hover {
  background-color: #2563eb;
}

</style>