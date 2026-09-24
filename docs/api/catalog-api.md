# API Contract: Catalog Module

**Module:** Catalog  
**Base Path:** `/api/v1/catalog`  
**Trách nhiệm:** Quản lý cây danh mục kỹ năng, chủ đề mentoring, tag tìm kiếm.  
**Authentication:** JWT Bearer Token

### Quy tắc triển khai

- Các API đọc cho phép anonymous; API ghi yêu cầu role `Admin`. Payload thành công giữ nguyên các DTO bên dưới, lỗi dùng ProblemDetails (`400`, `404`, `409`).
- Tên bắt buộc, tối đa 150 ký tự. Slug được tạo từ tên, bỏ dấu tiếng Việt (bao gồm `đ`), dùng chữ Latin thường, chữ số và dấu `-`. Tên không tạo được slug hợp lệ bị từ chối. Slug duy nhất trong từng loại dữ liệu; trùng slug trả `409`, kể cả khi hai request chạy đồng thời. Slug của category đã xóa vẫn được giữ.
- Mô tả tối đa 4000 ký tự; `iconUrl` nếu có phải là URL HTTP/HTTPS tối đa 2048 ký tự; `sortOrder >= 0`.
- Category cha, category của skill và tag IDs phải tồn tại, category chưa bị xóa; tham chiếu không hợp lệ trả `400`. Cây category không được có chu trình (`409`) và có tối đa 16 cấp (`400`), bao gồm toàn bộ cây con khi đổi danh mục cha. Xóa mềm category còn danh mục con chưa xóa hoặc còn skill trả `409`; hãy chuyển các bản ghi phụ thuộc trước. Category đã xóa bị ẩn khỏi API và không thể cập nhật lại.
- `isActive` là trạng thái hiển thị trong DTO; API đọc trả cả bản ghi active và inactive, trừ category đã xóa. Đổi category sang inactive không tự thay đổi các skill hoặc danh mục con.
- `GET /categories` và `GET /tags` trả toàn bộ danh sách, một trang, không cắt dữ liệu. `pageSize` tối thiểu lần lượt là 100 và 50, tăng theo số item nếu cần; ở dạng cây, `totalCount` đếm root items. Envelope phân trang có thêm `hasPreviousPage` và `hasNextPage`. Thứ tự category: `sortOrder`, `name`, `id`; skill/tag: `name`, `id`.
- Phân trang skills yêu cầu `page >= 1`, `1 <= pageSize <= 100` và offset không vượt `Int32.MaxValue`; sai trả `400`. `search` tối đa 150 ký tự, tìm chuỗi con không phân biệt hoa thường; `%` và `_` là ký tự literal. Tối đa 100 `tagIds`; ID trùng được loại bỏ; bỏ trống hoặc `null` tương đương danh sách rỗng. PUT thay thế toàn bộ tags.
- Ghi dữ liệu dùng transaction Serializable. Xung đột cập nhật đồng thời trả `409 Catalog.ConcurrentChange`; client có thể gửi lại thao tác sau khi đọc lại dữ liệu.
- Tạo skill lưu `SkillCreatedIntegrationEvent` vào `catalog.outbox_messages` trong cùng transaction với skill và tags. **Dispatcher RabbitMQ/inbox consumer chưa được triển khai**: sự kiện được lưu bền vững nhưng chưa được chuyển tới module khác.
- `mentor_services` trong database blueprint chưa được triển khai trong đợt này vì 11 endpoint bên dưới chưa định nghĩa contract dịch vụ mentor.

---

## 1. Lấy danh sách Categories

**HTTP Method:** `GET`  
**URL:** `/api/v1/catalog/categories`  
**Mô tả:** Lấy danh sách danh mục (categories). Hỗ trợ hiển thị dạng cây hoặc danh sách phẳng.  
**Quyền truy cập:** `Anonymous`

### Query Parameters
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `isTree` | `boolean` | Không | Nếu `true` sẽ trả về dạng cây (có `children`). Mặc định: `false`. |

### Response Body (200 OK)
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Lập trình Web",
      "slug": "lap-trinh-web",
      "description": "Các kỹ năng phát triển ứng dụng Web",
      "parentId": null,
      "iconUrl": "https://example.com/icon/web.png",
      "sortOrder": 1,
      "isActive": true,
      "createdAtUtc": "2026-09-15T14:30:00Z",
      "children": [] 
    }
  ],
  "page": 1,
  "pageSize": 100,
  "totalCount": 1,
  "totalPages": 1
}
```

---

## 2. Lấy chi tiết Category

**HTTP Method:** `GET`  
**URL:** `/api/v1/catalog/categories/{categoryId}`  
**Mô tả:** Lấy thông tin chi tiết của một danh mục và các kỹ năng (skills) thuộc danh mục đó.  
**Quyền truy cập:** `Anonymous`

### Path Parameters
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `categoryId` | `UUID` | Có | ID của danh mục |

### Response Body (200 OK)
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Lập trình Web",
  "slug": "lap-trinh-web",
  "description": "Các kỹ năng phát triển ứng dụng Web",
  "parentId": null,
  "iconUrl": "https://example.com/icon/web.png",
  "sortOrder": 1,
  "isActive": true,
  "createdAtUtc": "2026-09-15T14:30:00Z",
  "skills": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "ReactJS",
      "slug": "reactjs"
    }
  ]
}
```

### Lỗi (404 Not Found)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Catalog.CategoryNotFound",
  "status": 404,
  "detail": "Category không tồn tại.",
  "instance": "/api/v1/catalog/categories/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

## 3. Tạo Category mới

**HTTP Method:** `POST`  
**URL:** `/api/v1/catalog/categories`  
**Mô tả:** Thêm mới một danh mục.  
**Quyền truy cập:** `Admin`

### Request Body
```json
{
  "name": "Mobile App Development",
  "description": "Lập trình di động",
  "parentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "iconUrl": "https://example.com/icon/mobile.png",
  "sortOrder": 2
}
```

### Response Body (201 Created)
```json
{
  "id": "new-uuid",
  "name": "Mobile App Development",
  "slug": "mobile-app-development",
  "description": "Lập trình di động",
  "parentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "iconUrl": "https://example.com/icon/mobile.png",
  "sortOrder": 2,
  "isActive": true,
  "createdAtUtc": "2026-09-15T14:30:00Z"
}
```

### Lỗi (400 Bad Request)
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Catalog.InvalidName",
  "status": 400,
  "detail": "Tên phải có từ 1 đến 150 ký tự và không chứa ký tự điều khiển.",
  "instance": "/api/v1/catalog/categories",
  "correlationId": "abc123"
}
```

---

## 4. Cập nhật Category

**HTTP Method:** `PUT`  
**URL:** `/api/v1/catalog/categories/{categoryId}`  
**Mô tả:** Cập nhật thông tin danh mục.  
**Quyền truy cập:** `Admin`

### Request Body
```json
{
  "name": "Mobile Development",
  "description": "Lập trình ứng dụng di động iOS/Android",
  "parentId": null,
  "iconUrl": "https://example.com/icon/mobile-dev.png",
  "sortOrder": 2,
  "isActive": true
}
```

### Response Body (204 No Content)
Trống.

---

## 5. Xóa Category

**HTTP Method:** `DELETE`  
**URL:** `/api/v1/catalog/categories/{categoryId}`  
**Mô tả:** Xóa mềm (soft delete) danh mục.  
**Quyền truy cập:** `Admin`

### Response Body (204 No Content)
Trống.

---

## 6. Lấy danh sách Skills

**HTTP Method:** `GET`  
**URL:** `/api/v1/catalog/skills`  
**Mô tả:** Lấy danh sách kỹ năng, hỗ trợ phân trang, lọc và tìm kiếm.  
**Quyền truy cập:** `Anonymous`

### Query Parameters
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `categoryId` | `UUID` | Không | Lọc theo ID danh mục |
| `search` | `string` | Không | Tìm kiếm theo tên hoặc mô tả |
| `page` | `int` | Không | Trang hiện tại (Mặc định: 1) |
| `pageSize` | `int` | Không | Số lượng trên 1 trang (Mặc định: 10) |

### Response Body (200 OK)
```json
{
  "items": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "ReactJS",
      "slug": "reactjs",
      "description": "Thư viện JavaScript xây dựng UI",
      "isActive": true,
      "createdAtUtc": "2026-09-15T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 50,
  "totalPages": 5
}
```

---

## 7. Lấy chi tiết Skill

**HTTP Method:** `GET`  
**URL:** `/api/v1/catalog/skills/{skillId}`  
**Mô tả:** Lấy thông tin chi tiết một kỹ năng.  
**Quyền truy cập:** `Anonymous`

### Response Body (200 OK)
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "ReactJS",
  "slug": "reactjs",
  "description": "Thư viện JavaScript",
  "isActive": true,
  "createdAtUtc": "2026-09-15T14:30:00Z",
  "tags": [
    {
      "id": "tag-uuid",
      "name": "Frontend",
      "slug": "frontend"
    }
  ]
}
```

---

## 8. Tạo Skill mới

**HTTP Method:** `POST`  
**URL:** `/api/v1/catalog/skills`  
**Mô tả:** Thêm mới kỹ năng. Sinh `SkillCreatedIntegrationEvent`.  
**Quyền truy cập:** `Admin`

### Request Body
```json
{
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "ReactJS",
  "description": "Xây dựng ứng dụng với React",
  "tagIds": ["tag-uuid-1", "tag-uuid-2"]
}
```

### Response Body (201 Created)
```json
{
  "id": "new-uuid",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "ReactJS",
  "slug": "reactjs",
  "description": "Xây dựng ứng dụng với React",
  "isActive": true,
  "createdAtUtc": "2026-09-15T14:30:00Z"
}
```

---

## 9. Cập nhật Skill

**HTTP Method:** `PUT`  
**URL:** `/api/v1/catalog/skills/{skillId}`  
**Mô tả:** Cập nhật thông tin kỹ năng.  
**Quyền truy cập:** `Admin`

### Request Body
```json
{
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "ReactJS Cơ Bản",
  "description": "Cập nhật mô tả",
  "isActive": true,
  "tagIds": ["tag-uuid-1"]
}
```

### Response Body (204 No Content)
Trống.

---

## 10. Lấy danh sách Tags

**HTTP Method:** `GET`  
**URL:** `/api/v1/catalog/tags`  
**Mô tả:** Lấy danh sách tag.  
**Quyền truy cập:** `Anonymous`

### Response Body (200 OK)
```json
{
  "items": [
    {
      "id": "tag-uuid-1",
      "name": "Frontend",
      "slug": "frontend"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 20,
  "totalPages": 1
}
```

---

## 11. Tạo Tag mới

**HTTP Method:** `POST`  
**URL:** `/api/v1/catalog/tags`  
**Mô tả:** Tạo một tag mới.  
**Quyền truy cập:** `Admin`

### Request Body
```json
{
  "name": "Backend"
}
```

### Response Body (201 Created)
```json
{
  "id": "new-uuid",
  "name": "Backend",
  "slug": "backend"
}
```
