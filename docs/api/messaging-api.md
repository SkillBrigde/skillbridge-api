# Messaging Module API Contract

**Trách nhiệm:** Tin nhắn giữa Mentor-Mentee, hội thoại, SignalR chat realtime.

## Trạng thái triển khai VH-04 (2026-10-16)

- Đã triển khai tám REST API trên nhánh `dang/messaging-rest`; phần Hub được triển khai tiếp trên nhánh riêng sau khi REST qua checks.
- Tất cả REST API yêu cầu JWT còn hiệu lực. Các ID participant/sender là Identity user ID; server lấy người gửi từ JWT, không nhận sender ID do client tự khai.
- Hội thoại gồm hai user khác nhau, đang tồn tại/active khi tạo. Mỗi cặp chỉ có một hội thoại kể cả khi tạo đồng thời theo hai chiều. Recipient bị vô hiệu hóa thì gửi mới trả 409; JWT của user bị vô hiệu hóa trả 401.
- GET/POST messages, read và sửa/xóa đều kiểm tra membership. Người ngoài trả 404. Thành viên khác người gửi sửa/xóa tin nhắn trả 403. ID message thuộc hội thoại khác trả 404.
- Hiện chỉ nhận `type: "Text"` (mặc định nếu bỏ trường type); content trim, 1–4000 ký tự, không chứa NUL. `Attachment`, `System`, `SessionInvite` trả 400; chưa có contract upload hoặc producer tin nhắn hệ thống đáng tin cậy nên chưa triển khai các loại này.
- PUT nhận `{ "content": "Nội dung mới" }`, trả 200 MessageDto. DELETE trả 204, idempotent đối với tin nhắn đã xóa của người gửi; lịch sử giữ tombstone `isDeleted=true`, `content=""`. Sửa tin đã xóa trả 409.
- Danh sách hội thoại: page mặc định 1, tối đa 100000; pageSize mặc định 10, từ 1–100. Sắp xếp theo hoạt động gần nhất rồi ID giảm dần. Preview tối đa 200 ký tự, được cập nhật khi sửa/xóa.
- Lịch sử tin nhắn: pageSize mặc định 30, từ 1–100; cursor là chuỗi sequence nguyên dương của tin cuối trang trước. Trả `{items,nextCursor}`, thứ tự sequence giảm dần; nextCursor null khi hết trang. Sequence chỉ tăng trong từng hội thoại; ghi được serialize bằng khóa row PostgreSQL.
- POST read chỉ nhận message trong đúng hội thoại; cursor đọc chỉ tăng, đọc lại tin cũ không làm tăng unread. Unread không tính tin của chính mình hoặc tin đã xóa.
- Migration riêng `messaging`, không FK đến Identity. Message và `MessageSentIntegrationEvent` trong outbox được lưu cùng transaction; chưa có RabbitMQ dispatcher/inbox. Không đánh dấu production integration cho phần này.
- Host cung cấp adapter `IdentityMessagingUserDirectory`, giữ nguyên ranh giới project module. REST không phụ thuộc SignalR: port notifier mặc định không phát realtime; phần Hub bổ sung implementation sau.

### MessageDto

```json
{
  "id": "11111111-1111-4111-8111-111111111111",
  "conversationId": "22222222-2222-4222-8222-222222222222",
  "senderId": "33333333-3333-4333-8333-333333333333",
  "sequence": 1,
  "content": "Xin chào",
  "type": "Text",
  "isEdited": false,
  "isDeleted": false,
  "editedAtUtc": null,
  "createdAtUtc": "2026-10-16T03:00:00Z"
}
```

Lỗi validation dùng Problem Details 400 với errors theo trường. Phân trang/cursor sai
trả 400; không tìm thấy tài nguyên được phép truy cập trả 404. Conversation POST
trả ConversationDto và 201 nếu tạo mới, 200 nếu đã có. Các bảng attachment bên dưới
là blueprint, chưa được tạo bởi migration hiện tại.

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
