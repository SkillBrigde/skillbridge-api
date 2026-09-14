# Messaging Module API Contract

**Trách nhiệm:** Tin nhắn giữa Mentor-Mentee, hội thoại, SignalR chat realtime.

## Database Schema
- **`conversations`**: id (UUID), participant_1_id, participant_2_id, last_message_preview, last_message_at_utc, created_at_utc
- **`messages`**: id (UUID), conversation_id, sender_id, content, type (Text/Attachment/System/SessionInvite), is_edited, edited_at_utc, created_at_utc
- **`message_reads`**: id, conversation_id, user_id, last_read_message_id, last_read_at_utc
- **`message_attachments`**: id, message_id, file_name, file_url, file_size_bytes, content_type

## Integration Events
- **Phát hành:** `MessageSentIntegrationEvent`

---

## Các Endpoints (REST API)

### 1. Danh sách hội thoại
- **URL:** `/api/v1/messaging/conversations`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated`
- **Description:** Lấy danh sách hội thoại của user hiện tại (phân trang).

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "participantId": "5b885f64-5717-4562-b3fc-2c963f66afa6",
      "lastMessagePreview": "Chào bạn",
      "lastMessageAtUtc": "2026-09-15T14:30:00Z",
      "unreadCount": 2
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 5,
  "totalPages": 1
}
```

### 2. Tạo hoặc lấy hội thoại
- **URL:** `/api/v1/messaging/conversations`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`
- **Description:** Lấy hội thoại với user cụ thể. Nếu chưa có thì tạo mới.

#### Request Body
```json
{
  "targetUserId": "5b885f64-5717-4562-b3fc-2c963f66afa6"
}
```
#### Response (201 Created / 200 OK)

### 3. Lịch sử tin nhắn
- **URL:** `/api/v1/messaging/conversations/{conversationId}/messages`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated` (Thành viên hội thoại)
- **Description:** Lấy danh sách tin nhắn trong hội thoại, dùng cursor-based paging.

### 4. Gửi tin nhắn
- **URL:** `/api/v1/messaging/conversations/{conversationId}/messages`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`

#### Request Body
```json
{
  "content": "Xin chào",
  "type": "Text"
}
```
#### Response (201 Created)

### 5. Sửa tin nhắn
- **URL:** `/api/v1/messaging/conversations/{conversationId}/messages/{messageId}`
- **Method:** `PUT`
- **Auth Requirement:** `Authenticated` (Chỉ người gửi)

### 6. Xóa tin nhắn
- **URL:** `/api/v1/messaging/conversations/{conversationId}/messages/{messageId}`
- **Method:** `DELETE`
- **Auth Requirement:** `Authenticated` (Chỉ người gửi)

### 7. Đánh dấu đã đọc
- **URL:** `/api/v1/messaging/conversations/{conversationId}/read`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`

#### Request Body
```json
{
  "messageId": "2fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
#### Response (204 No Content)

### 8. Số tin nhắn chưa đọc
- **URL:** `/api/v1/messaging/unread-count`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated`

#### Response (200 OK)
```json
{
  "count": 5
}
```

---

## SignalR Hub (`/hubs/chat`)

### Server to Client Events
- `ReceiveMessage(MessageDto)`
- `MessageEdited(MessageId, Content)`
- `MessageDeleted(MessageId)`
- `UserTyping(ConversationId, UserId)`

### Client to Server Invocations
- `SendMessage(ConversationId, Content)`
- `MarkAsRead(ConversationId, MessageId)`
- `StartTyping(ConversationId)`
- `StopTyping(ConversationId)`
