# Logging Activity

Hệ thống ASP.NET Core MVC + MongoDB để tiếp nhận activity log từ partner, lưu trữ tập trung, tra cứu, thống kê và cảnh báo theo ngưỡng.

## Tính năng chính

- Đăng nhập quản trị bằng cookie authentication.
- Quản lý user, nhóm quyền, partner, log action và alert rule.
- Nhận log qua API partner và xử lý bất đồng bộ bằng queue.
- Dashboard, lọc log, lịch sử cảnh báo và thống kê theo ngày/partner/action.

## Chạy local

```powershell
dotnet restore
dotnet run --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

## Cấu hình

Không lưu secret thật trong source control. Dùng một trong các cách sau:

- `LoggingActivity.Web/appsettings.Local.json`
- `LoggingActivity.Web/appsettings.Development.local.json`
- user-secrets
- environment variables

Các key thường dùng:

- `MongoDb:ConnectionString` hoặc `MongoDb__ConnectionString`
- `MongoDb:DatabaseName` hoặc `MongoDb__DatabaseName`
- `SeedAdmin:UserName`, `SeedAdmin:Email`, `SeedAdmin:Password`

Ví dụ user-secrets:

```powershell
dotnet user-secrets set "MongoDb:ConnectionString" "<your-mongodb-connection-string>" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet user-secrets set "SeedAdmin:UserName" "admin" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet user-secrets set "SeedAdmin:Password" "<your-strong-password>" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Nếu URI chưa có database name, thêm `MongoDb:DatabaseName` tương ứng.

## API partner

Header bắt buộc:

```http
X-Api-Key: your-partner-api-key
```

Endpoint chính:

```http
POST /api/partner/activity
GET  /api/partner/activity?page=1&pageSize=10
GET  /api/partner/statistics
```

Payload gửi log là JSON gồm `userId`, `userName`, `action`, `description`, `endpoint`; hệ thống sẽ chuẩn hóa và đưa vào queue để xử lý nền.

## Deploy

Workflow mẫu deploy Linux VPS nằm ở [.github/workflows/deploy-linux-vps.yml](.github/workflows/deploy-linux-vps.yml). Dùng file đó làm điểm khởi đầu nếu cần triển khai tự động.

## Ghi chú

- App cần MongoDB để khởi động.
- Seed admin được tạo từ cấu hình `SeedAdmin:*`.
- `appsettings.Development.json` chỉ nên giữ placeholder an toàn.
