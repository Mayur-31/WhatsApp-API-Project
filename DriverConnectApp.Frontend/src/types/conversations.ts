export interface ConversationDto {
  Id: number;
  DriverId?: number;
  DriverName: string;
  DriverPhone: string;
  Topic: string;
  LastMessageAt?: string;
  CreatedAt: string;
  MessageCount: number;
  LastMessagePreview: string;
  IsAnswered: boolean;
  DepartmentId?: number;
  DepartmentName?: string;
  AssignedToUserId?: string;
  AssignedToUserName?: string;
  UnreadCount: number;

  // GROUP CONVERSATION FIELDS (NEW)
  IsGroupConversation: boolean;
  GroupName?: string;
  WhatsAppGroupId?: string;
  GroupMemberCount?: number;
  
  // NEW: Actual Group ID for API calls
  GroupId?: number;
  // 24-hour window fields
  lastInboundMessageAt?: string;
  canSendNonTemplateMessages: boolean;
  nonTemplateMessageStatus?: string;
  hoursRemaining?: number;
  minutesRemaining?: number;
  windowExpiresAt?: string;
}

export interface ConversationDetailDto {
  Id: number;
  DriverId?: number;
  DriverName: string;
  DriverPhone: string;
  Topic: string;
  LastMessageAt?: string;
  CreatedAt: string;
  IsAnswered: boolean;
  DepartmentId?: number;
  DepartmentName?: string;
  AssignedToUserId?: string;
  Messages: MessageDto[];

  // GROUP CONVERSATION FIELDS (NEW)
  IsGroupConversation: boolean;
  GroupName?: string;
  WhatsAppGroupId?: string;
  
  // NEW: Actual Group ID for management operations
  GroupId?: number;

  // Group participants
  Participants?: GroupParticipantDto[];

  lastInboundMessageAt?:  DateTime;
  canSendNonTemplateMessages:  boolean;
  hoursRemaining?:  number;
  minutesRemaining?: number;
  windowExpiresAt?: DateTime;
  nonTemplateMessageStatus?: string;
  
}

export interface MessageDto {
  Id: number;
  ConversationId: number;
  Content: string;
  MessageType: string;
  MediaUrl?: string;
  FileName?: string;
  FileSize?: number;
  MimeType?: string;
  Location?: string;
  ContactName?: string;
  ContactPhone?: string;
  IsFromDriver: boolean;
  SentAt: string;
  FormattedDate: string;
  FormattedTime: string;
  WhatsAppMessageId?: string;
  JobId?: string;
  Context?: string;
  Priority?: string;
  ThreadId?: number;

  // GROUP MESSAGE FIELDS
  IsGroupMessage: boolean;
  SenderPhoneNumber?: string;
  SenderName?: string;

  // NEW: Staff user information
  SentByUserId?: string;
  SentByUserName?: string;

  // ENHANCED: Reply functionality for ALL messages
  ReplyToMessageId?: number;
  ReplyToMessageContent?: string;
  ReplyToSenderName?: string;
  ReplyToMessage?: MessageDto; 

  Status: string; // "Sent", "Delivered", "Read"
  IsStarred: boolean;
  IsPinned: boolean;
  PinnedAt?: string;
  ForwardCount: number;
  ForwardedFromMessageId?: number;
  ForwardedFromMessage?: MessageDto;
  IsDeleted: boolean;
  DeletedAt?: string;
  DeletedByUserId?: string;

  // Reactions
  Reactions: MessageReactionDto[];

  // Recipients (for group messages)
  Recipients: MessageRecipientDto[];

  // Helper properties for UI
  CanDelete: boolean;
  StatusIcon: string;
  PinnedInfo?: string;
}

export interface MessageReactionDto {
  Id: number;
  MessageId: number;
  UserId?: string;
  DriverId?: number;
  Reaction: string;
  ReactedAt: string;
  UserName?: string;
  DriverName?: string;
  ReactorName: string;
}

export interface MessageRecipientDto {
  Id: number;
  MessageId: number;
  DriverId?: number;
  GroupParticipantId?: number;
  Status: string;
  DeliveredAt?: string;
  ReadAt?: string;
  HasSeen: boolean;
  SeenAt?: string;
  ParticipantName?: string;
  PhoneNumber?: string;
  DriverName?: string;
}

export interface MessageInfoResponse {
  Message: MessageDto;
  Recipients: MessageRecipientDto[];
  Reactions: MessageReactionDto[];
  TotalRecipients: number;
  DeliveredCount: number;
  ReadCount: number;
}

export interface MediaItemDto {
  Id: number;
  MessageId: number;
  ConversationId: number;
  Type: string; // "image", "video", "document", "link"
  Url?: string;
  ThumbnailUrl?: string;
  FileName?: string;
  FileSize?: number;
  MimeType?: string;
  Title?: string;
  Description?: string;
  SentAt: string;
  SenderName?: string;
  IsFromDriver: boolean;
  Duration?: string;
  Dimensions?: string;
}

export interface ConversationMediaResponse {
  Images: MediaItemDto[];
  Videos: MediaItemDto[];
  Documents: MediaItemDto[];
  Links: MediaItemDto[];
  TotalItems: number;
  ConversationName: string;
  IsGroupConversation: boolean;
}

// Request models
export interface UpdateMessageStatusRequest {
  Status: string;
  DriverId?: number;
  GroupParticipantId?: number;
}

export interface ReactToMessageRequest {
  Reaction: string;
}

export interface ForwardMessageRequest {
  ConversationIds: number[];
  CustomMessage?: string;
}

export interface PinMessageRequest {
  IsPinned: boolean;
}

export interface StarMessageRequest {
  IsStarred: boolean;
}

export interface GroupParticipantDto {
  Id: number;
  GroupId: number;
  DriverId?: number;
  PhoneNumber?: string;
  ParticipantName?: string;
  DriverName?: string;
  DriverPhone?: string;
  JoinedAt: string;
  IsActive: boolean;
  Role: string;
}

export interface GroupDto {
  Id: number;
  WhatsAppGroupId: string;
  Name: string;
  Description?: string;
  CreatedAt: string;
  LastActivityAt?: string;
  IsActive: boolean;
  ConversationCount: number;
  
  // NEW: Participants information
  Participants: GroupParticipantDto[];
  ParticipantCount: number;
}

export interface DepartmentDto {
  Id: number;
  Name: string;
  Description?: string;
  IsActive: boolean;
  CreatedAt: string;
}

export interface UserDto {
  Id: string;
  UserName: string;
  Email: string;
  FullName: string;
  IsActive: boolean;
  Roles: string[];
  DepartmentId?: number;
  DepotId?: number;
  DriverId?: number;
  CreatedAt: string;
  LastLoginAt?: string;
}

export interface DepotDto {
  Id: number;
  Name: string;
  Location?: string;
  Address?: string;
  PostalCode?: string;
  City?: string;
  IsActive: boolean;
  CreatedAt: string;
}

export interface MessageRequest {
  Content: string;
  IsFromDriver: boolean;
  ConversationId?: number;
  DriverId?: number;
  WhatsAppMessageId?: string;
  Context?: string;
  JobId?: string;
  Location?: string;
  Priority?: string;
  ThreadId?: number;

  // GROUP MESSAGE FIELDS
  IsGroupMessage?: boolean;
  GroupId?: string;
  MessageType?: string;
  MediaUrl?: string;
  FileName?: string;
  FileSize?: number;
  MimeType?: string;
  SenderPhoneNumber?: string;
  SenderName?: string;

  // ENHANCED: Reply functionality for ALL messages
  ReplyToMessageId?: number;
  ReplyToMessageContent?: string;
  ReplyToSenderName?: string;

  isTemplateMessage?: boolean;
  templateName?: string;
  templateParameters?: Record<string, string>;
}

// NEW: Group creation request
export interface CreateGroupRequest {
  WhatsAppGroupId: string;
  Name: string;
  Description?: string;
  Participants: GroupParticipantRequest[];
}

export interface GroupParticipantRequest {
  DriverId?: number;
  PhoneNumber?: string;
  ParticipantName?: string;
  Role?: string;
}

export interface UpdateGroupRequest {
  Name?: string;
  Description?: string;
  IsActive?: boolean;
}

export interface AddParticipantsRequest {
  Participants: GroupParticipantRequest[];
}

export interface RemoveParticipantsRequest {
  ParticipantIds: number[];
}

