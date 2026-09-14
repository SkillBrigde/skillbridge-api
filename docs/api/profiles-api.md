# Tài liệu API Contract - Module Profiles

Module Profiles chịu trách nhiệm quản lý hồ sơ chi tiết của Mentor (kỹ năng, kinh nghiệm, giá/giờ, chứng chỉ) và Mentee (mục tiêu học tập).

## Thông tin chung

- **Base URL**: `/api/v1/profiles`
- **Xác thực**: JWT Bearer Token (trừ các endpoint có ghi `Anonymous`)
- **Định dạng thời gian**: ISO 8601 UTC (VD: `2026-09-15T14:30:00Z`)
- **Kiểu dữ liệu ID**: UUID (Guid)

## Lỗi tiêu chuẩn (RFC 7807)

Các response lỗi sử dụng định dạng Problem Details (RFC 7807):

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Tên lỗi",
  "status": 400,
  "detail": "Thông báo chi tiết về lỗi",
  "instance": "/api/v1/profiles/...",
  "extensions": {
    "correlationId": "abc12345-6789",
    "errors": {
      "fieldName": ["Mô tả lỗi validation"]
    }
  }
}
```

---

## 1. Mentor Profile

### 1.1 Lấy profile Mentor hiện tại
- **URL**: `/api/v1/profiles/mentors/me`
- **Method**: `GET`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Lấy thông tin chi tiết hồ sơ của Mentor đang đăng nhập.

**Response** (200 OK)
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "headline": "Senior Software Engineer at TechCorp",
  "bio": "Hơn 10 năm kinh nghiệm trong lĩnh vực phát triển phần mềm...",
  "hourlyRate": 500000,
  "currency": "VND",
  "timezone": "Asia/Ho_Chi_Minh",
  "ratingAverage": 4.8,
  "totalReviews": 15,
  "totalSessions": 30,
  "verificationStatus": "Verified",
  "isAcceptingMentees": true,
  "avatarUrl": "https://example.com/avatar.jpg",
  "skills": [
    {
      "id": "223e4567-e89b-12d3-a456-426614174001",
      "skillId": "323e4567-e89b-12d3-a456-426614174002",
      "name": ".NET Core",
      "proficiencyLevel": "Expert",
      "yearsOfExperience": 8
    }
  ],
  "experiences": [
    {
      "id": "423e4567-e89b-12d3-a456-426614174003",
      "company": "TechCorp",
      "position": "Senior Software Engineer",
      "startDate": "2020-01-01T00:00:00Z",
      "endDate": null,
      "description": "Phát triển hệ thống backend...",
      "isCurrent": true
    }
  ],
  "certifications": [
    {
      "id": "523e4567-e89b-12d3-a456-426614174004",
      "name": "AWS Certified Solutions Architect",
      "issuingOrganization": "Amazon Web Services",
      "issueDate": "2022-05-10T00:00:00Z",
      "expiryDate": "2025-05-10T00:00:00Z",
      "credentialUrl": "https://aws.amazon.com/verify",
      "credentialId": "AWS-12345"
    }
  ],
  "createdAtUtc": "2024-01-15T08:00:00Z",
  "updatedAtUtc": "2024-02-20T10:30:00Z"
}
```

### 1.2 Cập nhật profile Mentor
- **URL**: `/api/v1/profiles/mentors/me`
- **Method**: `PUT`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Cập nhật thông tin cơ bản của Mentor (không bao gồm skill, experience, certification).

**Request Body**
```json
{
  "headline": "Senior Software Engineer at TechCorp",
  "bio": "Hơn 10 năm kinh nghiệm...",
  "hourlyRate": 500000,
  "currency": "VND",
  "timezone": "Asia/Ho_Chi_Minh",
  "isAcceptingMentees": true,
  "avatarUrl": "https://example.com/avatar.jpg"
}
```

**Quy tắc nghiệp vụ:**
- `hourlyRate`: Tối thiểu 0, tối đa 10,000,000 VND hoặc 500 USD.
- `currency`: Chỉ chấp nhận "VND" hoặc "USD".

**Responses**
- `204 No Content`: Cập nhật thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ (VD: giá tiền vượt giới hạn).
- `401 Unauthorized`: Chưa xác thực.

### 1.3 Xem profile Mentor (Public)
- **URL**: `/api/v1/profiles/mentors/{mentorId}`
- **Method**: `GET`
- **Quyền truy cập**: `Anonymous`, `Authenticated`
- **Mô tả**: Lấy thông tin công khai của một Mentor theo ID.

**Response** (200 OK)
Cấu trúc tương tự như endpoint `1.1` (có thể ẩn một số thông tin nhạy cảm nếu có).

**Responses**
- `200 OK`: Lấy thông tin thành công.
- `404 Not Found`: Không tìm thấy Mentor.

### 1.4 Tìm kiếm/lọc danh sách Mentors (Public, phân trang)
- **URL**: `/api/v1/profiles/mentors`
- **Method**: `GET`
- **Quyền truy cập**: `Anonymous`, `Authenticated`
- **Mô tả**: Tìm kiếm và lọc danh sách Mentor, có phân trang.

**Query Parameters**
| Tên tham số | Kiểu dữ liệu | Mô tả |
|---|---|---|
| `searchTerm` | string | Tìm theo tên, headline, bio |
| `skillId` | UUID | Lọc theo ID kỹ năng |
| `minRate` | decimal | Giá tối thiểu |
| `maxRate` | decimal | Giá tối đa |
| `verificationStatus` | string | "Unverified", "Pending", "Verified" |
| `minRating` | decimal | Đánh giá sao tối thiểu |
| `sortBy` | string | Sắp xếp theo: "rating", "rate", "reviews" |
| `sortDesc` | boolean | Sắp xếp giảm dần (true) hay tăng dần (false) |
| `page` | int | Số trang (mặc định 1) |
| `pageSize` | int | Số kết quả mỗi trang (mặc định 10) |

**Response** (200 OK)
```json
{
  "items": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "headline": "Senior Software Engineer",
      "hourlyRate": 500000,
      "currency": "VND",
      "ratingAverage": 4.8,
      "totalReviews": 15,
      "verificationStatus": "Verified",
      "avatarUrl": "https://example.com/avatar.jpg"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 50,
  "totalPages": 5
}
```

### 1.5 Thêm kỹ năng
- **URL**: `/api/v1/profiles/mentors/me/skills`
- **Method**: `POST`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Thêm một kỹ năng mới cho Mentor.

**Request Body**
```json
{
  "skillId": "323e4567-e89b-12d3-a456-426614174002",
  "proficiencyLevel": "Expert",
  "yearsOfExperience": 8
}
```

**Quy tắc nghiệp vụ:**
- `proficiencyLevel`: Thuộc tập "Beginner", "Intermediate", "Advanced", "Expert".
- `yearsOfExperience`: Lớn hơn hoặc bằng 0.

**Responses**
- `201 Created`: Thêm thành công.
- `400 Bad Request`: Kỹ năng đã tồn tại hoặc data không hợp lệ.
- `401 Unauthorized`: Chưa xác thực.

### 1.6 Xóa kỹ năng
- **URL**: `/api/v1/profiles/mentors/me/skills/{skillId}`
- **Method**: `DELETE`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Bỏ một kỹ năng khỏi hồ sơ. (Lưu ý: skillId ở đây là ID của bản ghi `mentor_skills` hoặc ID kỹ năng từ Catalog, tuỳ thiết kế hệ thống).

**Responses**
- `204 No Content`: Xóa thành công.
- `401 Unauthorized`: Chưa xác thực.
- `404 Not Found`: Kỹ năng không tồn tại trong hồ sơ của Mentor này.

### 1.7 Thêm kinh nghiệm
- **URL**: `/api/v1/profiles/mentors/me/experiences`
- **Method**: `POST`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Thêm kinh nghiệm làm việc mới.

**Request Body**
```json
{
  "company": "TechCorp",
  "position": "Senior Software Engineer",
  "startDate": "2020-01-01T00:00:00Z",
  "endDate": null,
  "description": "Phát triển hệ thống backend...",
  "isCurrent": true
}
```

**Responses**
- `201 Created`: Thêm thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ.
- `401 Unauthorized`: Chưa xác thực.

### 1.8 Sửa kinh nghiệm
- **URL**: `/api/v1/profiles/mentors/me/experiences/{experienceId}`
- **Method**: `PUT`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Cập nhật thông tin kinh nghiệm làm việc.

**Request Body**
(Tương tự như Thêm kinh nghiệm)

**Responses**
- `204 No Content`: Cập nhật thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ.
- `401 Unauthorized`: Chưa xác thực.
- `404 Not Found`: Không tìm thấy kinh nghiệm.

### 1.9 Xóa kinh nghiệm
- **URL**: `/api/v1/profiles/mentors/me/experiences/{experienceId}`
- **Method**: `DELETE`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Xóa kinh nghiệm làm việc.

**Responses**
- `204 No Content`: Xóa thành công.
- `401 Unauthorized`: Chưa xác thực.
- `404 Not Found`: Không tìm thấy.

### 1.10 Thêm chứng chỉ
- **URL**: `/api/v1/profiles/mentors/me/certifications`
- **Method**: `POST`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Thêm chứng chỉ mới.

**Request Body**
```json
{
  "name": "AWS Certified Solutions Architect",
  "issuingOrganization": "Amazon Web Services",
  "issueDate": "2022-05-10T00:00:00Z",
  "expiryDate": "2025-05-10T00:00:00Z",
  "credentialUrl": "https://aws.amazon.com/verify",
  "credentialId": "AWS-12345"
}
```

**Responses**
- `201 Created`: Thêm thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ.
- `401 Unauthorized`: Chưa xác thực.

### 1.11 Xóa chứng chỉ
- **URL**: `/api/v1/profiles/mentors/me/certifications/{certificationId}`
- **Method**: `DELETE`
- **Quyền truy cập**: `Mentor`
- **Mô tả**: Xóa chứng chỉ.

**Responses**
- `204 No Content`: Xóa thành công.
- `401 Unauthorized`: Chưa xác thực.
- `404 Not Found`: Không tìm thấy.

---

## 2. Mentee Profile

### 2.1 Lấy profile Mentee hiện tại
- **URL**: `/api/v1/profiles/mentees/me`
- **Method**: `GET`
- **Quyền truy cập**: `Mentee`
- **Mô tả**: Lấy thông tin chi tiết hồ sơ của Mentee đang đăng nhập.

**Response** (200 OK)
```json
{
  "id": "623e4567-e89b-12d3-a456-426614174005",
  "careerGoal": "Trở thành Senior Backend Developer trong 3 năm tới",
  "currentLevel": "Intermediate",
  "timezone": "Asia/Ho_Chi_Minh",
  "budgetMin": 100000,
  "budgetMax": 500000,
  "currency": "VND",
  "avatarUrl": "https://example.com/avatar-mentee.jpg",
  "createdAtUtc": "2024-01-15T08:00:00Z",
  "updatedAtUtc": "2024-02-20T10:30:00Z"
}
```
**Responses**
- `200 OK`: Thành công.
- `401 Unauthorized`: Chưa xác thực.

### 2.2 Cập nhật profile Mentee
- **URL**: `/api/v1/profiles/mentees/me`
- **Method**: `PUT`
- **Quyền truy cập**: `Mentee`
- **Mô tả**: Cập nhật thông tin profile của Mentee.

**Request Body**
```json
{
  "careerGoal": "Trở thành Senior Backend Developer trong 3 năm tới",
  "currentLevel": "Intermediate",
  "timezone": "Asia/Ho_Chi_Minh",
  "budgetMin": 100000,
  "budgetMax": 500000,
  "currency": "VND",
  "avatarUrl": "https://example.com/avatar-mentee.jpg"
}
```

**Quy tắc nghiệp vụ:**
- `currentLevel`: Thuộc tập "Beginner", "Intermediate", "Advanced".
- `budgetMin` phải nhỏ hơn hoặc bằng `budgetMax`.

**Responses**
- `204 No Content`: Cập nhật thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ.
- `401 Unauthorized`: Chưa xác thực.

---

## 3. Admin

### 3.1 Duyệt/từ chối Mentor
- **URL**: `/api/v1/profiles/mentors/{mentorId}/verification`
- **Method**: `PUT`
- **Quyền truy cập**: `Admin`
- **Mô tả**: Thay đổi trạng thái xác minh của hồ sơ Mentor.

**Request Body**
```json
{
  "verificationStatus": "Verified"
}
```

**Quy tắc nghiệp vụ:**
- `verificationStatus`: Bắt buộc thuộc "Unverified", "Pending", hoặc "Verified".

**Responses**
- `204 No Content`: Cập nhật thành công.
- `400 Bad Request`: Dữ liệu không hợp lệ.
- `401 Unauthorized`: Chưa xác thực.
- `403 Forbidden`: Không có quyền Admin.
- `404 Not Found`: Không tìm thấy Mentor.
