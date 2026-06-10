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

Workflow deploy Linux VPS:

- Điều phối môi trường: [.github/workflows/deploy-linux-vps.yml](.github/workflows/deploy-linux-vps.yml)
- Logic reusable: [.github/workflows/deploy-linux-vps-reusable.yml](.github/workflows/deploy-linux-vps-reusable.yml)

### CI/CD qua GitHub Actions (SSH private key)

Workflow đã sẵn cơ chế triển khai theo mô hình chuẩn:

1. Zero-downtime theo release thư mục: mỗi lần deploy tạo release mới trong `releases`, sau đó đổi symlink `current`.
2. Rollback tự động: nếu health check fail sau restart service, workflow tự quay về release trước.
3. Reusable workflow cho nhiều môi trường: file điều phối gọi chung workflow tái sử dụng cho production hoặc staging.

Workflow có thêm các thiết lập an toàn:

- Chống deploy chồng nhau (`concurrency`)
- Timeout cho từng job
- Cache NuGet để build nhanh hơn
- Fail rõ ràng nếu artifact không tồn tại, service không active, hoặc health check không đạt

Thiết lập trong GitHub repo:

1. Vào `Settings -> Secrets and variables -> Actions`.
2. Tạo secrets cho production:
	- `PROD_VPS_HOST`
	- `PROD_VPS_PORT`
	- `PROD_VPS_USER`
	- `PROD_VPS_SSH_KEY`
	- `PROD_VPS_SSH_PASSPHRASE` (nếu key không có passphrase thì để trống)
	- `PROD_VPS_SERVICE_NAME` (ví dụ `logging-activity`)
	- `PROD_VPS_HEALTHCHECK_URL` (ví dụ `https://your-domain.com/`)
3. Tạo secrets cho staging nếu cần deploy staging:
	- `STAGING_VPS_HOST`
	- `STAGING_VPS_PORT`
	- `STAGING_VPS_USER`
	- `STAGING_VPS_SSH_KEY`
	- `STAGING_VPS_SSH_PASSPHRASE`
	- `STAGING_VPS_APP_ROOT`
	- `STAGING_VPS_SERVICE_NAME`
	- `STAGING_VPS_HEALTHCHECK_URL`

Luồng chạy:

- Push nhánh `main`: tự deploy production.
- Chạy tay `workflow_dispatch`: chọn `production`, `staging` hoặc `both`.

Lưu ý server:

- Production deploy cố định tại thư mục `/var/www/logging`.
- Service systemd nên chạy từ symlink `current` để tận dụng zero-downtime release switching.
- `ExecStart` nên trỏ vào `.../current/LoggingActivity.Web.dll`.
- Biến môi trường runtime (`MongoDb__*`, `SeedAdmin__*`) nên set trong service để app khởi động ổn định sau mỗi lần deploy.

## Ghi chú

- App cần MongoDB để khởi động.
- Seed admin được tạo từ cấu hình `SeedAdmin:*`.
- `appsettings.Development.json` chỉ nên giữ placeholder an toàn.
