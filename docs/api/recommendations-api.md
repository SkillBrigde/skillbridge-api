# API Contract - Recommendations Module

**Module**: Recommendations  
**Base Path**: `/api/v1/recommendations`  
**Description**: API cho module Recommendations, cung cấp thuật toán gợi ý Mentor phù hợp nhất cho Mentee dựa trên kỹ năng, lịch sử, tương tác và đánh giá.

---

## 1. Gợi ý Mentors phù hợp cho Mentee

Lấy danh sách các Mentor được đề xuất cho Mentee hiện tại, sắp xếp theo điểm phù hợp (score).

- **Method**: `GET`
- **URL**: `/api/v1/recommendations/mentors`
- **Auth Role**: `Mentee`

### Query Parameters

| Tên | Kiểu dữ liệu | Bắt buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `page` | `int` | Không | Số trang (mặc định: 1) |
| `pageSize` | `int` | Không | Số dòng trên 1 trang (mặc định: 20) |

### Response (`200 OK`)

```json
{
  "items": [
    {
      "mentorId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "score": 0.92,
      "reasons": [
        "Phù hợp với kỹ năng bạn quan tâm (ReactJS)",
        "Được đánh giá cao (4.9/5)",
        "Phù hợp với mức giá bạn thường đặt"
      ],
      "metrics": {
        "averageRating": 4.9,
        "totalSessions": 120,
        "acceptanceRate": 0.95
      }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3
}
```

### Business Rules
- Score được tính dựa trên: skill match, rating, completion rate, response time, price range fit.
- Ghi log recommendation để phục vụ A/B testing thuật toán.

---

## 2. Top Mentors cho 1 kỹ năng

Lấy danh sách các Mentor xuất sắc nhất (dựa trên metrics) cho một kỹ năng cụ thể.

- **Method**: `GET`
- **URL**: `/api/v1/recommendations/mentors/for-skill/{skillId}`
- **Auth Role**: `Anonymous`, `Authenticated`

### Path Parameters
- `skillId` (`Guid`): ID của kỹ năng cần tìm Mentor.

### Response (`200 OK`)

```json
{
  "items": [
    {
      "mentorId": "a17ac10b-58cc-4372-a567-0e02b2c3d470",
      "metrics": {
        "averageRating": 5.0,
        "totalSessions": 45,
        "completionRate": 1.0
      }
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 50,
  "totalPages": 5
}
```

---

## 3. Ghi nhận tương tác

Lưu lại hành vi của Mentee (như click vào gợi ý, xem profile) để cải thiện thuật toán gợi ý (tự động cập nhật mentee interests).

- **Method**: `POST`
- **URL**: `/api/v1/recommendations/interactions`
- **Auth Role**: `Mentee`

### Request Body

| Trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `mentorId` | `Guid` | Có | ID của Mentor mà Mentee tương tác |
| `skillId` | `Guid?` | Không | ID kỹ năng liên quan đến tương tác |
| `interactionType` | `string` | Có | Loại tương tác: `Click`, `ViewProfile` |

```json
{
  "mentorId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "skillId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "interactionType": "ViewProfile"
}
```

### Response (`204 No Content`)

Không có dữ liệu trả về nếu thành công.

---

## 4. Xem Metrics của Mentor

Xem thông tin thống kê, độ tin cậy và điểm số các chỉ số của một Mentor.

- **Method**: `GET`
- **URL**: `/api/v1/recommendations/mentors/{mentorId}/metrics`
- **Auth Role**: `Admin`, `Mentor` (Mentor chỉ xem được của chính mình)

### Path Parameters
- `mentorId` (`Guid`): ID của Mentor.

### Response (`200 OK`)

```json
{
  "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mentorId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "acceptanceRate": 0.95,
  "completionRate": 0.98,
  "averageResponseTimeMinutes": 15.5,
  "repeatMenteeCount": 20,
  "totalSessions": 120,
  "averageRating": 4.8,
  "lastCalculatedAtUtc": "2026-09-14T02:00:00Z"
}
```

### Business Rules
- Metrics được tính lại theo batch (thường chạy định kỳ ngầm), không tính lại realtime khi truy vấn.
- Quyền truy cập: Admin xem được tất cả, Mentor chỉ xem được metrics của chính tài khoản mình.

### Errors (`403 Forbidden`)

Trường hợp Mentor xem metrics của Mentor khác:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Forbidden",
  "status": 403,
  "detail": "Bạn không có quyền truy cập dữ liệu này.",
  "instance": "/api/v1/recommendations/mentors/xxx/metrics"
}
```

---

## 5. Danh sách kỹ năng đang Hot

Lấy danh sách các kỹ năng đang có xu hướng (trending) được quan tâm hoặc đặt lịch nhiều trong thời gian gần đây.

- **Method**: `GET`
- **URL**: `/api/v1/recommendations/trending-skills`
- **Auth Role**: `Anonymous`, `Authenticated`

### Response (`200 OK`)

```json
[
  {
    "skillId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "System Design",
    "trendingScore": 85.5
  },
  {
    "skillId": "2fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "ReactJS",
    "trendingScore": 79.2
  }
]
```
