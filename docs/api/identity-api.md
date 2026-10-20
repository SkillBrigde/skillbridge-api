# Identity API Contract

Tài liệu đặc tả API cho module Identity của dự án SkillBridge (Kiến trúc .NET 10 Modular Monolith).

## Trạng thái triển khai (2026-10-16)

- Đã triển khai đủ 12 endpoint bên dưới, gồm `forgot-password` và `reset-password`. Khôi phục mật khẩu hiện có adapter hộp thư trong bộ nhớ chỉ dùng ở Development; chưa tích hợp email provider production.
- Migration `AddPasswordRecovery` thêm `identity.password_reset_tokens`. Mỗi tài khoản có tối đa một bản ghi token mới nhất; chỉ lưu SHA-256, gắn security stamp, có hạn 30 phút và dùng một lần. Reset thành công đổi password hash/security stamp, xóa trạng thái đăng nhập sai và thu hồi mọi refresh token trong cùng giao dịch. Token reset cũ cũng mất hiệu lực khi security stamp thay đổi.
- `UserRegisteredIntegrationEvent` được ghi cùng giao dịch tạo user vào `identity.outbox_messages`. Dispatcher và consumer Profiles chưa được triển khai, do đó chưa tự động tạo profile.
- Migration `CompleteIdentitySecurity` bổ sung schema xác thực sau migration đầu tiên. User từ schema cũ được giữ nguyên ID/thông tin, chuẩn hóa email và vô hiệu hóa vì chưa có mật khẩu. Email cũ trùng nhau sau chuẩn hóa cần được xử lý trước khi migration thành công; migration không tự gộp/xóa tài khoản.
- Mỗi JWT mang security stamp; khi đổi mật khẩu, vô hiệu hóa tài khoản hoặc đăng xuất bằng cookie thuộc tài khoản đang xác thực, mọi access token cũ mất hiệu lực ngay và mọi refresh token của tài khoản bị thu hồi. Đăng xuất áp dụng cho tất cả thiết bị/phiên của tài khoản.
- Refresh token và mã thay thế chỉ được lưu dưới dạng SHA-256; rotation là một lần sử dụng và giữ nguyên hạn tuyệt đối 7 ngày từ lần đăng nhập. Token hết hạn trả `401`; tái sử dụng token đã thu hồi (kể cả yêu cầu rotation đồng thời thua cuộc) trả `401` và vô hiệu hóa mọi access/refresh token của tài khoản. Client phải tuần tự hóa refresh và đăng nhập lại sau khi phát hiện replay.
- Nhóm `/auth` giới hạn 10 yêu cầu/phút cho mỗi địa chỉ IP và đường dẫn, trả `429` khi vượt giới hạn. BFF cần chuyển tiếp cookie `refreshToken` khi gọi refresh/logout qua HTTPS.

## Module Responsibility
Xác thực, phân quyền, đăng ký, đăng nhập, cấp phát JWT Tokens.

## Quy Tắc Chung
- **ID Format**: Định dạng UUID (Guid)
- **Timestamp Format**: ISO 8601 UTC (ví dụ: `2026-09-15T14:30:00Z`)
- **Phân trang**: Sử dụng chuẩn `PagedResult<T>` của BuildingBlocks
- **Bảo mật**: Các API yêu cầu xác thực sử dụng JWT Bearer Token.

### Standard Error Response (RFC 7807)
Các lỗi trả về sẽ tuân theo định dạng chuẩn RFC 7807 ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation.Failed",
  "status": 400,
  "detail": "Một hoặc nhiều trường dữ liệu không hợp lệ.",
  "instance": "/api/v1/identity/auth/register",
  "correlationId": "5f3a123b-a3b4-4b5c",
  "errors": {
    "Password": ["Mật khẩu phải chứa ít nhất 1 chữ hoa."]
  }
}
```

---

## Danh Sách Endpoints

### 1. Đăng ký tài khoản mới (Register)

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/register`
- **Description**: Đăng ký tài khoản mới (Mentor hoặc Mentee), lưu `UserRegisteredIntegrationEvent` vào outbox để consumer Profiles xử lý khi được triển khai.
- **Auth Requirement**: `Anonymous`

**Business Rules:**
- `email`: Phải đúng định dạng và duy nhất trong hệ thống.
- `password`: Tối thiểu 8 ký tự, phải có chữ hoa (uppercase), chữ thường (lowercase), số (number), và ký tự đặc biệt (special char).
- Giới hạn BCrypt: mật khẩu tối đa 72 byte UTF-8; quy tắc này cũng áp dụng khi đổi mật khẩu. `fullName` tối đa 200 ký tự, `email` tối đa 256 ký tự; dữ liệu văn bản lưu vào PostgreSQL không được chứa ký tự NUL.
- `role`: Chỉ nhận `Mentor` hoặc `Mentee`.

**Request Body:**

| Field | Type | Required | Description |
|---|---|---|---|
| email | string | Yes | Địa chỉ email người dùng |
| password | string | Yes | Mật khẩu (theo quy tắc trên) |
| fullName | string | Yes | Họ và tên |
| role | string | Yes | Vai trò (`Mentor` hoặc `Mentee`) |

```json
{
  "email": "user@example.com",
  "password": "Password123!",
  "fullName": "Nguyễn Văn A",
  "role": "Mentee"
}
```

**Responses:**
- `201 Created`: Đăng ký thành công.
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
  ```
- `400 Bad Request`: Lỗi validation dữ liệu.
- `409 Conflict`: Email đã tồn tại trong hệ thống.

---

### 2. Đăng nhập bằng Email/Password (Login)

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/login`
- **Description**: Xác thực bằng Email và Password. Khi đăng nhập thành công:
  - Trả về `accessToken` dạng JSON cho Client lưu vào memory.
  - Thiết lập HTTP-Only Cookie chứa `refreshToken` (thời hạn 7 ngày).
- **Auth Requirement**: `Anonymous`

**Business Rules:**
- Nhập sai mật khẩu liên tiếp 5 lần sẽ kích hoạt cơ chế Lockout tạm thời (15 phút).

**Request Body:**

| Field | Type | Required | Description |
|---|---|---|---|
| email | string | Yes | Email đã đăng ký |
| password | string | Yes | Mật khẩu |

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

**Responses:**
- `200 OK`: Đăng nhập thành công.
  - **Headers**: `Set-Cookie: refreshToken={token}; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/identity/auth; Max-Age=604800`
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1...",
    "expiresIn": 900
  }
  ```
- `400 Bad Request`: Thiếu email hoặc mật khẩu.
- `401 Unauthorized`: Email hoặc mật khẩu không chính xác, hoặc tài khoản đang bị khóa.

---

### 3. Làm mới Access Token (Refresh)

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/refresh`
- **Description**: Cấp mới Access Token bằng cách sử dụng Refresh Token từ cookie (Rotation Token).
- **Auth Requirement**: `Anonymous` (Nhưng yêu cầu cookie chứa Refresh Token hợp lệ).

**Request Body:** Không có. Token được đọc từ Cookie `refreshToken`.

Token thay thế giữ nguyên thời điểm hết hạn trong cơ sở dữ liệu của token ban đầu; thời gian giữ cookie không gia hạn phiên. Tái sử dụng token đã thu hồi vô hiệu hóa mọi phiên của tài khoản, kể cả token vừa được cấp bởi yêu cầu refresh đồng thời.

**Responses:**
- `200 OK`: Thành công.
  - **Headers**: `Set-Cookie: refreshToken={new_token}; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/identity/auth; Max-Age=604800`
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1...",
    "expiresIn": 900
  }
  ```
- `401 Unauthorized`: Cookie không hợp lệ, token hết hạn, hoặc bị thu hồi (revoked).

---

### 4. Đăng xuất (Logout)

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/logout`
- **Description**: Đăng xuất mọi phiên của tài khoản, vô hiệu hóa ngay các access token và refresh token, rồi xóa cookie hiện tại.
- **Auth Requirement**: `Authenticated`

**Request Body:** Không có.

Cookie phải chứa một refresh token thuộc tài khoản đang xác thực để thu hồi phiên. Cookie thiếu, không được nhận diện hoặc thuộc tài khoản khác vẫn trả `204` và xóa cookie, nhưng không thay đổi phiên trên máy chủ.

**Responses:**
- `204 No Content`: Thành công.
  - **Headers**: `Set-Cookie: refreshToken=; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/identity/auth; Max-Age=0`

---

### 5. Thông tin user hiện tại

- **Method**: `GET`
- **URL**: `/api/v1/identity/users/me`
- **Description**: Lấy thông tin cơ bản của người dùng đang đăng nhập.
- **Auth Requirement**: `Authenticated`

**Responses:**
- `200 OK`: Thành công.
  ```json
  {
    "id": "e3b0c442-989b-464c-86c3-12d3a4564266",
    "email": "nguyenvana@example.com",
    "fullName": "Nguyễn Văn A",
    "role": "Mentee",
    "isActive": true
  }
  ```
- `401 Unauthorized`: Token không hợp lệ.

---

### 6. Cập nhật thông tin cơ bản

- **Method**: `PUT`
- **URL**: `/api/v1/identity/users/me`
- **Description**: Cập nhật thông tin cơ bản của tài khoản đang đăng nhập.
- **Auth Requirement**: `Authenticated`

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| fullName | string | Yes | Họ và tên mới |

```json
{
  "fullName": "Nguyễn Văn A (Cập nhật)"
}
```

**Responses:**
- `204 No Content`: Thành công.
- `400 Bad Request`: Lỗi validation.

---

### 7. Đổi mật khẩu

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/change-password`
- **Description**: Đổi mật khẩu khi người dùng đang đăng nhập (đã biết mật khẩu cũ).
- **Auth Requirement**: `Authenticated`

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| currentPassword | string | Yes | Mật khẩu hiện tại |
| newPassword | string | Yes | Mật khẩu mới |

```json
{
  "currentPassword": "Password123!",
  "newPassword": "NewPassword123!"
}
```

**Responses:**
- `204 No Content`: Thành công.
- `400 Bad Request`: Mật khẩu hiện tại không đúng, hoặc mật khẩu mới vi phạm rule.

Thành công đồng thời thu hồi mọi refresh token của người dùng, xóa cookie hiện tại và đổi security stamp. Client phải đăng nhập lại.

---

### 8. Quên mật khẩu

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/forgot-password`
- **Description**: Yêu cầu tạo mã/token đặt lại mật khẩu và gửi qua email.
- **Auth Requirement**: `Anonymous`

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| email | string | Yes | Email đã đăng ký |

```json
{
  "email": "nguyenvana@example.com"
}
```

**Responses:**
- `204 No Content`: Yêu cầu hợp lệ đã được tiếp nhận; cùng phản hồi rỗng cho email tồn tại, không tồn tại, tài khoản vô hiệu hóa, cooldown hoặc lỗi gửi email. Không trả token qua HTTP.
- `400 Bad Request`: Email thiếu/sai định dạng, quá 256 ký tự hoặc chứa NUL.
- `429 Too Many Requests`: Vượt giới hạn chung của nhóm auth (10 yêu cầu/phút/IP/endpoint).
- `503 Service Unavailable`: Chưa có email provider dùng được; áp dụng cho mọi địa chỉ email trước khi truy vấn tài khoản.

Mỗi tài khoản được gửi tối đa một mã/phút. Mã gồm 32 byte ngẫu nhiên, biểu diễn thành 64 ký tự hex viết hoa; mã mới thay thế mã trước đó, hết hạn sau 30 phút.

`IPasswordResetEmailSender` là điểm tích hợp email. Adapter mặc định `DevelopmentPasswordResetEmailSender` chỉ hoạt động khi `IHostEnvironment.IsDevelopment()`; hộp thư giữ tối đa 1.000 địa chỉ trong bộ nhớ, hết hạn cùng token và mất khi tiến trình khởi động lại. Lập trình viên có thể đọc một lần bằng `Take(email)` qua debugger hoặc service được inject trong kiểm thử; không có endpoint lấy token và không ghi token vào log. Môi trường khác mặc định trả `503`; cần đăng ký adapter gửi email thật thay thế interface trước khi phát hành production. Link do adapter thật tạo phải dùng URL frontend được cấu hình tin cậy, không lấy host từ request.

---

### 9. Đặt lại mật khẩu

- **Method**: `POST`
- **URL**: `/api/v1/identity/auth/reset-password`
- **Description**: Đặt lại mật khẩu mới dựa trên token được gửi qua email.
- **Auth Requirement**: `Anonymous`

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| email | string | Yes | Email liên kết |
| token | string | Yes | Mã reset mật khẩu |
| newPassword | string | Yes | Mật khẩu mới |

```json
{
  "email": "nguyenvana@example.com",
  "token": "reset-token-received-via-email",
  "newPassword": "NewPassword123!"
}
```

**Responses:**
- `204 No Content`: Thành công.
- `400 Bad Request`: Lỗi validation hoặc token không hợp lệ/đã hết hạn.

Token sai tài khoản, đã dùng, hết hạn hoặc có security stamp cũ đều trả cùng lỗi `Identity.InvalidResetToken`. Mật khẩu mới theo quy tắc đăng ký (ít nhất 8 ký tự, đủ hoa/thường/số/ký tự đặc biệt, tối đa 72 byte UTF-8). Hai lần reset đồng thời chỉ có một lần thành công. Sau reset, cookie refresh hiện tại được xóa và người dùng phải đăng nhập lại trên tất cả thiết bị. Endpoint có `Cache-Control: no-store` và giới hạn auth chung.

---

### 10. Danh sách Users (Admin)

- **Method**: `GET`
- **URL**: `/api/v1/identity/users`
- **Description**: Xem danh sách toàn bộ người dùng, hỗ trợ phân trang và tìm kiếm.
- **Auth Requirement**: `Admin`

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| page | int | 1 | Trang số mấy |
| pageSize | int | 10 | Số lượng trên mỗi trang, từ 1 đến 100 |
| searchTerm | string | null | Từ khóa tìm kiếm theo email hoặc fullName, tối đa 256 ký tự |

`page` phải lớn hơn 0; tổ hợp `page`/`pageSize` vượt giới hạn offset 32-bit trả `400`. Người đã đăng nhập không có quyền Admin nhận `403` ở cả ba endpoint quản trị.

**Responses:**
- `200 OK`: Thành công.
  ```json
  {
    "items": [
      {
        "id": "e3b0c442-989b-464c-86c3-12d3a4564266",
        "email": "nguyenvana@example.com",
        "fullName": "Nguyễn Văn A",
        "role": "Mentee",
        "isActive": true,
        "createdAtUtc": "2026-09-14T08:30:00Z"
      }
    ],
    "page": 1,
    "pageSize": 10,
    "totalCount": 105,
    "totalPages": 11
  }
  ```

---

### 11. Khóa/Mở khóa User (Admin)

- **Method**: `PUT`
- **URL**: `/api/v1/identity/users/{userId}/status`
- **Description**: Cập nhật trạng thái hoạt động của một người dùng (Khóa hoặc kích hoạt).
- **Auth Requirement**: `Admin`

**Request Body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| isActive | boolean | Yes | Trạng thái (true: Kích hoạt, false: Khóa) |

```json
{
  "isActive": false
}
```

**Responses:**
- `204 No Content`: Thành công.
- `403 Forbidden`: Admin không được tự vô hiệu hóa tài khoản của mình.
- `404 Not Found`: Không tìm thấy userId.

---

### 12. Xem chi tiết 1 User (Admin)

- **Method**: `GET`
- **URL**: `/api/v1/identity/users/{userId}`
- **Description**: Xem chi tiết thông tin quản trị của một người dùng bất kỳ.
- **Auth Requirement**: `Admin`

**Responses:**
- `200 OK`: Thành công.
  ```json
  {
    "id": "e3b0c442-989b-464c-86c3-12d3a4564266",
    "email": "nguyenvana@example.com",
    "fullName": "Nguyễn Văn A",
    "role": "Mentee",
    "isActive": true,
    "twoFactorEnabled": false,
    "lockoutEndUtc": null,
    "accessFailedCount": 0,
    "createdAtUtc": "2026-09-14T08:30:00Z",
    "updatedAtUtc": "2026-09-14T09:30:00Z"
  }
  ```
- `404 Not Found`: Không tìm thấy userId.
