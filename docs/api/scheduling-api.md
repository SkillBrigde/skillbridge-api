# API Contract: Scheduling Module

**Module:** Scheduling  
**Base Path:** `/api/v1/scheduling`  
**Trách nhiệm:** Lịch biểu sẵn sàng (Availability Slots) của Mentor, ngày nghỉ, lịch bận.  
**Authentication:** JWT Bearer Token

**Phạm vi hiện tại:** Đã triển khai đủ 10 REST API: Settings (2), Availability Rules (4), Slots (1), Time-offs (3). Booking dùng cổng giữ/xác nhận/nhả slot nội bộ tại Host; không có API công khai cho client tự đánh dấu đã đặt.

**Quy tắc nghiệp vụ:**
- Slots tự động sinh từ `availability_rules`
- Khi tạo `time-off`, hệ thống chuyển các slot khả dụng trong khoảng đó sang `Locked`; không xóa định danh slot.
- `buffer_time_minutes`: khoảng thời gian nghỉ giữa 2 buổi mentoring.
- `lead_time_hours`: người học (mentee) phải đặt lịch trước ít nhất số giờ quy định này.

**Quy ước thời gian dùng cho các API Scheduling:**
- `timezone` dùng mã IANA hoặc `UTC`; availability rule lưu giờ địa phương theo múi giờ của Mentor.
- Slots lưu UTC; cửa sổ sinh gồm hôm nay đến hôm nay + `maxDaysInAdvance - 1` theo timezone Mentor. Ghi settings/rule/time-off và đọc danh sách slots đều làm mới cửa sổ này. Hiện không có tác vụ nền sinh slot. Thay đổi lịch và sinh lại slot cùng transaction, cùng khóa PostgreSQL theo Mentor.
- `startDate`, `endDate` của truy vấn slots và time-off đều bao gồm cả ngày đầu lẫn ngày cuối theo múi giờ Mentor.
- Khi đổi giờ mùa hè (DST), giờ địa phương không tồn tại sẽ bị bỏ qua; giờ bị lặp dùng lần xuất hiện sớm hơn để không sinh trùng slot. Bỏ qua slot nếu thời điểm đầu/cuối không tồn tại hoặc thời lượng UTC khác thời lượng cấu hình.
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

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `GET`  
**URL:** `/api/v1/scheduling/availability-rules`  
**Mô tả:** Danh sách các quy tắc lặp lại hàng tuần của Mentor.  
**Quyền truy cập:** `Mentor`

### Query Parameters

- `page`: mặc định `1`, tối thiểu `1`; vị trí trang không vượt quá `Int32.MaxValue`.
- `pageSize`: mặc định `100`, từ `1` đến `100`.
- Chỉ trả rule của Mentor hiện tại, gồm cả rule ngừng hoạt động; sắp xếp theo `dayOfWeek`, `startTime`, `id`.
- `dayOfWeek`: `0` là Chủ nhật, `1` là Thứ hai, …, `6` là Thứ bảy.
- Mentor có hồ sơ nhưng chưa có rule nhận danh sách rỗng.

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
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### Responses

- `200 OK`: Danh sách rule của Mentor hiện tại.
- `400 Bad Request`: Tham số phân trang không hợp lệ.
- `401 Unauthorized`: Chưa đăng nhập.
- `403 Forbidden`: Không có vai trò Mentor.
- `409 Conflict`: Chưa tạo hồ sơ Mentor.

---

## 4. Thêm Availability Rule

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `POST`  
**URL:** `/api/v1/scheduling/availability-rules`  
**Mô tả:** Thêm một khung giờ rảnh hàng tuần.  
**Quyền truy cập:** `Mentor`

### Quy tắc nghiệp vụ

- Mentor phải có hồ sơ Profiles và Scheduling Settings hợp lệ trước khi thêm rule.
- `dayOfWeek`, `startTime`, `endTime` đều bắt buộc; không nhận trường JSON ngoài contract.
- `dayOfWeek` từ `0` (Chủ nhật) đến `6` (Thứ bảy).
- `startTime`, `endTime` là giờ địa phương trong timezone của Settings, định dạng `HH:mm:ss`, không nhận phần lẻ giây; không gửi UTC offset.
- Giờ bắt đầu phải nhỏ hơn giờ kết thúc trong cùng ngày; khoảng qua đêm cần tách rule.
- Rule mới mặc định `isActive = true`. Chỉ rule đang hoạt động bị kiểm tra chồng giờ theo cùng Mentor và ngày trong tuần.
- Khoảng giờ được hiểu là `[startTime, endTime)`: hai rule tiếp giáp được phép, ví dụ `09:00–11:00` và `11:00–12:00`.
- Ghi rule được tuần tự hóa trong transaction theo Mentor để chống tạo chồng giờ khi request chạy đồng thời.

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

### Responses

- `201 Created`: Rule mới, kèm header `Location` trỏ đến định danh rule (định danh dùng cho PUT/DELETE).
- `400 Bad Request`: Dữ liệu thiếu hoặc không hợp lệ.
- `401 Unauthorized`: Chưa đăng nhập.
- `403 Forbidden`: Không có vai trò Mentor.
- `409 Conflict`: Chưa có hồ sơ/Settings hoặc chồng giờ với rule đang hoạt động.

---

## 5. Sửa Availability Rule

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `PUT`  
**URL:** `/api/v1/scheduling/availability-rules/{ruleId}`  
**Mô tả:** Cập nhật rule rảnh.  
**Quyền truy cập:** `Mentor`

### Quy tắc nghiệp vụ

- Quy tắc về ngày, giờ, timezone và Settings giống POST.
- Bắt buộc gửi đủ `dayOfWeek`, `startTime`, `endTime`, `isActive`.
- Chỉ cập nhật rule thuộc Mentor hiện tại. ID không tồn tại và ID thuộc Mentor khác đều trả `404`.
- `isActive = false` được phép trùng giờ; bật lại phải kiểm tra chồng giờ với các rule đang hoạt động.
- Gửi lại cùng dữ liệu trả `204`; kiểm tra chồng giờ loại trừ chính rule đang sửa.

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

### Responses

- `204 No Content`: Cập nhật thành công.
- `400 Bad Request`: Dữ liệu thiếu hoặc không hợp lệ.
- `401 Unauthorized`: Chưa đăng nhập.
- `403 Forbidden`: Không có vai trò Mentor.
- `404 Not Found`: Rule không tồn tại hoặc không thuộc Mentor hiện tại.
- `409 Conflict`: Chưa có hồ sơ/Settings hoặc chồng giờ với rule đang hoạt động.

---

## 6. Xóa Availability Rule

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `DELETE`  
**URL:** `/api/v1/scheduling/availability-rules/{ruleId}`  
**Mô tả:** Xóa một quy tắc thời gian rảnh.  
**Quyền truy cập:** `Mentor`

### Quy tắc nghiệp vụ

- Chỉ xóa rule thuộc Mentor hiện tại; không tồn tại và không thuộc quyền sở hữu đều trả `404`.
- Xóa cả rule đang hoạt động hoặc ngừng hoạt động; GET không trả lại rule đã xóa.
- Yêu cầu hồ sơ và Settings, dùng cùng khóa transaction theo Mentor như POST/PUT.
- Gọi lại sau khi xóa trả `404`.

### Response Body (204 No Content)
Trống.

### Responses

- `204 No Content`: Xóa thành công.
- `400 Bad Request`: Rule ID rỗng.
- `401 Unauthorized`: Chưa đăng nhập.
- `403 Forbidden`: Không có vai trò Mentor.
- `404 Not Found`: Rule không tồn tại hoặc không thuộc Mentor hiện tại.
- `409 Conflict`: Chưa có hồ sơ/Settings.

---

## 7. Xem danh sách Slots trống của Mentor

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `GET`  
**URL:** `/api/v1/scheduling/mentors/{mentorId}/slots`  
**Mô tả:** Lấy danh sách các khung giờ (slots) thực tế của một mentor, trong khoảng ngày.  
**Quyền truy cập:** `Public`

### Query Parameters
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `startDate` | `string` | Có | Ngày bắt đầu (ISO 8601, VD: `2026-09-15`) |
| `endDate` | `string` | Có | Ngày kết thúc (ISO 8601, VD: `2026-09-22`) |
| `page` | `int` | Không | Mặc định 1, tối thiểu 1 |
| `pageSize` | `int` | Không | Mặc định 100, từ 1 đến 100 |

Chỉ trả slot `Available`, chưa bắt đầu và đáp ứng `leadTimeHours`; thứ tự `startAtUtc`, `id`. Ngày tính theo timezone Mentor, hai đầu bao gồm; khoảng truy vấn tối đa 366 ngày, năm 2000–9998. `mentorId` là ID hồ sơ Profiles, không phải ID tài khoản. `bookingId` luôn null ở phản hồi công khai. ID slot giữ nguyên khi sinh lại cùng khung giờ.

Lỗi: `400` tham số không hợp lệ; `404` Mentor chưa có settings. Ngoài cửa sổ đặt lịch nhận danh sách rỗng.

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

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `POST`  
**URL:** `/api/v1/scheduling/time-offs`  
**Mô tả:** Mentor thêm thời gian vắng mặt. Tự động chuyển các slots có trong thời gian này từ `Available` sang `Locked`. Không tự hủy booking hoặc phát sự kiện nhả booking.
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

**Trạng thái:** `IMPLEMENTED`

**HTTP Method:** `DELETE`  
**URL:** `/api/v1/scheduling/time-offs/{timeOffId}`  
**Mô tả:** Hủy thời gian nghỉ. Các slots có thể được phục hồi.  
**Quyền truy cập:** `Mentor`

### Response Body (204 No Content)
Trống.

---

## 10. Lấy danh sách ngày nghỉ (Time-offs)

**Trạng thái:** `IMPLEMENTED`

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


## Giới hạn và tích hợp ND-10

- Time-off bắt buộc ngày đầu/cuối, từ hôm nay đến tối đa 365 ngày tới theo timezone Mentor; `reason` tùy chọn, tối đa 1000 ký tự, không chứa ký tự điều khiển. Không nhận trường JSON ngoài contract. Khoảng nghỉ chồng nhau trả `409`, kể cả tạo đồng thời.
- GET Time-offs chỉ trả dữ liệu của Mentor đăng nhập, sắp theo `startDate`, `id`; `page=1`, `pageSize=50`, tối đa 100. Không có bộ lọc ngày ở endpoint này.
- DELETE Time-off khác chủ hoặc không tồn tại trả `404`; xóa thành công khôi phục slot theo settings/rules hiện tại.
- POST/DELETE Time-off yêu cầu hồ sơ Mentor và settings. `401` chưa đăng nhập; `403` sai vai trò; `409` thiếu hồ sơ/settings hoặc ảnh hưởng slot được bảo vệ.
- Mọi cập nhật settings/rules ảnh hưởng slot `Held` chưa hết hạn hoặc `Booked` trả `409 Scheduling.ProtectedSlot`; toàn bộ thay đổi được rollback. Slot không còn hợp lệ được lưu `Unavailable`, giữ lịch sử và ID; không trả cho client.
- Cổng nội bộ `SlotReservations` giữ nguyên tử 1–52 slot của cùng Mentor, kiểm tra thời lượng, lead time và hạn giữ tối đa 10 phút. Giữ thiếu một slot thì rollback cả gói. Xác nhận chỉ thành công khi đủ slot, đúng booking và chưa hết hạn. Chỉ Host được nối cổng này sang Booking; client không được truyền trạng thái thanh toán để xác nhận.
- Hold hết hạn được thu hồi khi làm mới lịch; chưa có worker chủ động. Booking cần tự kiểm tra hạn thanh toán và có cơ chế retry/đối soát trước khi nhận thanh toán thật. Phần này chưa chứng minh luồng Booking/Payments đầu cuối.

Quy tắc DST dựa trên `TimeZoneInfo.IsInvalidTime`, `IsAmbiguousTime` và `GetAmbiguousTimeOffsets` của .NET: [tài liệu Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.isambiguoustime?view=net-10.0).
