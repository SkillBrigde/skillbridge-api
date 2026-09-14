# Learning Module API Contract

**Trách nhiệm:** Quản lý buổi học trực tuyến: phòng video call, tài liệu chia sẻ, ghi chú buổi học.

## Database Schema
- **`learning_sessions`**: id (UUID), booking_id, mentor_id, mentee_id, room_url, status (Scheduled/InProgress/Completed/NoShow/Cancelled), scheduled_start_utc, scheduled_end_utc, actual_start_utc, actual_end_utc, duration_seconds, created_at_utc
- **`session_attendances`**: id, session_id, user_id, joined_at_utc, left_at_utc
- **`session_notes`**: id, session_id, author_id, content, is_private, created_at_utc, updated_at_utc
- **`session_materials`**: id, session_id, uploaded_by, file_name, file_url, file_size_bytes, content_type, created_at_utc
- **`session_action_items`**: id, session_id, assigned_to, title, description, is_completed, due_date, created_at_utc

## Integration Events
- **Lắng nghe:** `BookingConfirmedIntegrationEvent` → tự động tạo session
- **Phát hành:** `SessionStartedIntegrationEvent`, `SessionCompletedIntegrationEvent`

---

## Các Endpoints (REST API)

### 1. Danh sách buổi học
- **URL:** `/api/v1/learning/sessions`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated` (Mentor, Mentee, Admin)
- **Description:** Lấy danh sách các buổi học (hỗ trợ phân trang, lọc theo trạng thái).

#### Request Parameters
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| status | string | Không | Lọc theo trạng thái (Scheduled, InProgress, v.v.) |
| page | int | Không | Trang hiện tại (Mặc định: 1) |
| pageSize | int | Không | Số lượng trên mỗi trang (Mặc định: 10) |

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookingId": "7a885f64-5717-4562-b3fc-2c963f66afa6",
      "mentorId": "5b885f64-5717-4562-b3fc-2c963f66afa6",
      "menteeId": "1c885f64-5717-4562-b3fc-2c963f66afa6",
      "status": "Scheduled",
      "scheduledStartUtc": "2026-09-15T14:30:00Z",
      "scheduledEndUtc": "2026-09-15T15:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 50,
  "totalPages": 5
}
```

### 2. Chi tiết buổi học
- **URL:** `/api/v1/learning/sessions/{sessionId}`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated` (Chỉ người tham gia hoặc Admin)
- **Description:** Xem chi tiết thông tin buổi học theo ID.

#### Response (200 OK)
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "roomUrl": "https://meet.skillbridge.com/room/abc-xyz",
  "status": "Scheduled",
  "scheduledStartUtc": "2026-09-15T14:30:00Z",
  "scheduledEndUtc": "2026-09-15T15:30:00Z"
}
```
#### Error Response (404 Not Found)
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "detail": "Session not found",
  "instance": "/api/v1/learning/sessions/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### 3. Bắt đầu buổi học
- **URL:** `/api/v1/learning/sessions/{sessionId}/start`
- **Method:** `POST`
- **Auth Requirement:** `Mentor` (Mentor phụ trách buổi học)
- **Description:** Mentor bắt đầu buổi học, chuyển trạng thái sang InProgress.

#### Response (204 No Content)

### 4. Kết thúc buổi học
- **URL:** `/api/v1/learning/sessions/{sessionId}/end`
- **Method:** `POST`
- **Auth Requirement:** `Mentor`
- **Description:** Mentor kết thúc buổi học, chuyển trạng thái sang Completed.

#### Response (204 No Content)

### 5. Tham gia buổi học
- **URL:** `/api/v1/learning/sessions/{sessionId}/join`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`
- **Description:** Ghi nhận thời gian user tham gia (attendance).

#### Response (204 No Content)

### 6. Rời buổi học
- **URL:** `/api/v1/learning/sessions/{sessionId}/leave`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`
- **Description:** Ghi nhận thời gian user rời buổi học.

#### Response (204 No Content)

### 7. Danh sách ghi chú
- **URL:** `/api/v1/learning/sessions/{sessionId}/notes`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated`
- **Description:** Lấy danh sách ghi chú của buổi học.

#### Response (200 OK)
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "content": "Nhớ ôn lại bài số 1",
      "authorId": "5b885f64-5717-4562-b3fc-2c963f66afa6",
      "isPrivate": false,
      "createdAtUtc": "2026-09-15T14:40:00Z"
    }
  ]
}
```

### 8. Thêm ghi chú
- **URL:** `/api/v1/learning/sessions/{sessionId}/notes`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`
- **Description:** Thêm ghi chú mới cho buổi học.

#### Request Body
```json
{
  "content": "Nội dung ghi chú",
  "isPrivate": true
}
```
#### Response (201 Created)

### 9. Sửa ghi chú
- **URL:** `/api/v1/learning/sessions/{sessionId}/notes/{noteId}`
- **Method:** `PUT`
- **Auth Requirement:** `Authenticated` (Chỉ tác giả ghi chú)

#### Request Body
```json
{
  "content": "Nội dung đã sửa",
  "isPrivate": false
}
```
#### Response (204 No Content)

### 10. Xóa ghi chú
- **URL:** `/api/v1/learning/sessions/{sessionId}/notes/{noteId}`
- **Method:** `DELETE`
- **Auth Requirement:** `Authenticated` (Chỉ tác giả ghi chú)
#### Response (204 No Content)

### 11. Danh sách tài liệu
- **URL:** `/api/v1/learning/sessions/{sessionId}/materials`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated`
#### Response (200 OK)

### 12. Upload tài liệu
- **URL:** `/api/v1/learning/sessions/{sessionId}/materials`
- **Method:** `POST`
- **Auth Requirement:** `Authenticated`
#### Response (201 Created)

### 13. Xóa tài liệu
- **URL:** `/api/v1/learning/sessions/{sessionId}/materials/{materialId}`
- **Method:** `DELETE`
- **Auth Requirement:** `Authenticated` (Chỉ người upload)
#### Response (204 No Content)

### 14. Danh sách bài tập
- **URL:** `/api/v1/learning/sessions/{sessionId}/action-items`
- **Method:** `GET`
- **Auth Requirement:** `Authenticated`
#### Response (200 OK)

### 15. Tạo bài tập
- **URL:** `/api/v1/learning/sessions/{sessionId}/action-items`
- **Method:** `POST`
- **Auth Requirement:** `Mentor`
#### Request Body
```json
{
  "assignedTo": "1c885f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Làm bài thực hành 1",
  "description": "Chi tiết bài tập",
  "dueDate": "2026-09-20T23:59:59Z"
}
```
#### Response (201 Created)

### 16. Đánh dấu hoàn thành bài tập
- **URL:** `/api/v1/learning/sessions/{sessionId}/action-items/{itemId}`
- **Method:** `PATCH`
- **Auth Requirement:** `Mentee` (Người được giao)
#### Request Body
```json
{
  "isCompleted": true
}
```
#### Response (204 No Content)
