# API Contract: Scheduling Module

**Module:** Scheduling  
**Base Path:** `/api/v1/scheduling`  
**Trách nhiệm:** Lịch biểu sẵn sàng (Availability Slots) của Mentor, ngày nghỉ, lịch bận.  
**Authentication:** JWT Bearer Token

**Quy tắc nghiệp vụ:**
- Slots tự động sinh từ `availability_rules`
- Khi tạo `time-off`, hệ thống sẽ tự động hủy các slots đang ở trạng thái `Available` nằm trong khoảng thời gian đó.
- `buffer_time_minutes`: khoảng thời gian nghỉ giữa 2 buổi mentoring.
- `lead_time_hours`: người học (mentee) phải đặt lịch trước ít nhất số giờ quy định này.

**Quy ước thời gian dùng cho các API Scheduling:**
- `timezone` dùng mã IANA hoặc `UTC`; availability rule lưu giờ địa phương theo múi giờ của Mentor.
- Slots thực tế được lưu bằng UTC và được sinh sẵn đến giới hạn `maxDaysInAdvance`; thay đổi settings, rule hoặc time-off sẽ tái sinh phần lịch bị ảnh hưởng trong cùng transaction.
- `startDate`, `endDate` của truy vấn slots và time-off đều bao gồm cả ngày đầu lẫn ngày cuối theo múi giờ Mentor.
- Khi đổi giờ mùa hè (DST), giờ địa phương không tồn tại sẽ bị bỏ qua; giờ bị lặp dùng lần xuất hiện sớm hơn để không sinh trùng slot.
- Các availability rule đang hoạt động trong cùng một thứ không được chồng lấn.
- Time-off khóa slot `Available`; nếu chồng lên slot `Held` hoặc `Booked`, yêu cầu bị từ chối và không tự hủy lịch đã giữ/đặt.

---

## 1. Lấy cài đặt lịch (Settings)

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `GET`  
**URL:** `/api/v1/scheduling/settings`  
**Mô tả:** Lấy thông tin cấu hình lịch của Mentor đang đăng nhập.  
**Quyền truy cập:** `Mentor`

### Response Body (200 OK)
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "mentorId": "mentor-uuid",
  "sessionDurationMinutes": 60,
  "bufferTimeMinutes": 15,
  "leadTimeHours": 24,
  "maxDaysInAdvance": 30,
  "timezone": "Asia/Ho_Chi_Minh"
}
```

### Responses

- `200 OK`: Lấy cài đặt lịch thành công.
- `401 Unauthorized`: Chưa đăng nhập.
- `403 Forbidden`: Tài khoản không có vai trò Mentor.
- `404 Not Found`: Mentor chưa cấu hình cài đặt lịch.
- `409 Conflict`: Mentor chưa tạo hồ sơ Profiles.

---

## 2. Cập nhật cài đặt lịch

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `PUT`  
**URL:** `/api/v1/scheduling/settings`  
**Mô tả:** Cập nhật cài đặt lịch của Mentor đang đăng nhập.  
**Quyền truy cập:** `Mentor`

### Request Body
```json
{
  "sessionDurationMinutes": 60,
  "bufferTimeMinutes": 15,
  "leadTimeHours": 24,
  "maxDaysInAdvance": 30,
  "timezone": "Asia/Ho_Chi_Minh"
}
```

### Response Body (204 No Content)
Trống.

### Quy tắc nghiệp vụ

- `sessionDurationMinutes`: từ 15 đến 240 phút, là bội số của 15.
- `bufferTimeMinutes`: từ 0 đến 120 phút, là bội số của 5.
- `leadTimeHours`: từ 0 đến 720 giờ và không vượt quá `maxDaysInAdvance * 24`.
- `maxDaysInAdvance`: từ 1 đến 365 ngày.
- `timezone`: mã múi giờ IANA hợp lệ (ví dụ `Asia/Ho_Chi_Minh`) hoặc `UTC`.
- Lần `PUT` đầu tiên tạo settings; các lần tiếp theo cập nhật cùng bản ghi.

### Responses

- `204 No Content`: Tạo hoặc cập nhật thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ.
- `401 Unauthorized`: Chưa đăng nhập.
- `403 Forbidden`: Tài khoản không có vai trò Mentor.
- `409 Conflict`: Mentor chưa tạo hồ sơ Profiles.

---

## 3. Lấy danh sách Availability Rules

**HTTP Method:** `GET`  
**URL:** `/api/v1/scheduling/availability-rules`  
**Mô tả:** Danh sách các quy tắc lặp lại hàng tuần của Mentor.  
**Quyền truy cập:** `Mentor`

### Response Body (200 OK)
```json
{
  "items": [
    {
      "id": "rule-uuid-1",
      "mentorId": "mentor-uuid",
      "dayOfWeek": 1,
      "startTime": "09:00:00",
      "endTime": "11:00:00",
      "isActive": true
    }
  ],
  "page": 1,
  "pageSize": 100,
  "totalCount": 1,
  "totalPages": 1
}
```

---

## 4. Thêm Availability Rule

**HTTP Method:** `POST`  
**URL:** `/api/v1/scheduling/availability-rules`  
**Mô tả:** Thêm một khung giờ rảnh hàng tuần.  
**Quyền truy cập:** `Mentor`

### Request Body
```json
{
  "dayOfWeek": 1,
  "startTime": "09:00:00",
  "endTime": "11:00:00"
}
```

### Response Body (201 Created)
```json
{
  "id": "new-rule-uuid",
  "mentorId": "mentor-uuid",
  "dayOfWeek": 1,
  "startTime": "09:00:00",
  "endTime": "11:00:00",
  "isActive": true
}
```

---

## 5. Sửa Availability Rule

**HTTP Method:** `PUT`  
**URL:** `/api/v1/scheduling/availability-rules/{ruleId}`  
**Mô tả:** Cập nhật rule rảnh.  
**Quyền truy cập:** `Mentor`

### Request Body
```json
{
  "dayOfWeek": 1,
  "startTime": "10:00:00",
  "endTime": "12:00:00",
  "isActive": true
}
```

### Response Body (204 No Content)
Trống.

---

## 6. Xóa Availability Rule

**HTTP Method:** `DELETE`  
**URL:** `/api/v1/scheduling/availability-rules/{ruleId}`  
**Mô tả:** Xóa một quy tắc thời gian rảnh.  
**Quyền truy cập:** `Mentor`

### Response Body (204 No Content)
Trống.

---

## 7. Xem danh sách Slots trống của Mentor

**HTTP Method:** `GET`  
**URL:** `/api/v1/scheduling/mentors/{mentorId}/slots`  
**Mô tả:** Lấy danh sách các khung giờ (slots) thực tế của một mentor, trong khoảng ngày.  
**Quyền truy cập:** `Public` (hoặc `Authenticated`/`Mentee`)

### Query Parameters
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `startDate` | `string` | Có | Ngày bắt đầu (ISO 8601, VD: `2026-09-15`) |
| `endDate` | `string` | Có | Ngày kết thúc (ISO 8601, VD: `2026-09-22`) |

### Response Body (200 OK)
```json
{
  "items": [
    {
      "id": "slot-uuid-1",
      "mentorId": "mentor-uuid",
      "startAtUtc": "2026-09-15T02:00:00Z",
      "endAtUtc": "2026-09-15T03:00:00Z",
      "status": "Available",
      "bookingId": null,
      "createdAtUtc": "2026-09-14T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 100,
  "totalCount": 1,
  "totalPages": 1
}
```

---

## 8. Đăng ký ngày nghỉ (Time-off)

**HTTP Method:** `POST`  
**URL:** `/api/v1/scheduling/time-offs`  
**Mô tả:** Mentor thêm thời gian vắng mặt. Tự động chuyển các slots có trong thời gian này từ `Available` sang `Locked`. Sinh sự kiện `SlotReleasedIntegrationEvent` (nếu cần xử lý thêm).  
**Quyền truy cập:** `Mentor`

### Request Body
```json
{
  "startDate": "2026-09-20",
  "endDate": "2026-09-21",
  "reason": "Nghỉ lễ"
}
```

### Response Body (201 Created)
```json
{
  "id": "timeoff-uuid",
  "mentorId": "mentor-uuid",
  "startDate": "2026-09-20",
  "endDate": "2026-09-21",
  "reason": "Nghỉ lễ",
  "createdAtUtc": "2026-09-15T14:30:00Z"
}
```

---

## 9. Hủy đăng ký ngày nghỉ

**HTTP Method:** `DELETE`  
**URL:** `/api/v1/scheduling/time-offs/{timeOffId}`  
**Mô tả:** Hủy thời gian nghỉ. Các slots có thể được phục hồi.  
**Quyền truy cập:** `Mentor`

### Response Body (204 No Content)
Trống.

---

## 10. Lấy danh sách ngày nghỉ (Time-offs)

**HTTP Method:** `GET`  
**URL:** `/api/v1/scheduling/time-offs`  
**Mô tả:** Lấy danh sách các lịch vắng mặt của Mentor.  
**Quyền truy cập:** `Mentor`

### Response Body (200 OK)
```json
{
  "items": [
    {
      "id": "timeoff-uuid",
      "mentorId": "mentor-uuid",
      "startDate": "2026-09-20",
      "endDate": "2026-09-21",
      "reason": "Nghỉ lễ",
      "createdAtUtc": "2026-09-15T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1,
  "totalPages": 1
}
```
