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
2. Nếu bạn lưu secret theo Environment thì tạo đúng trong environment tương ứng (`production` hoặc `staging`), không đặt nhầm môi trường.
3. Nếu bạn lưu secret ở repository-level thì áp dụng cho tất cả môi trường.
4. Tạo secrets cho production:
	- Tối thiểu để deploy: `PROD_VPS_HOST`, `PROD_VPS_USER`, `PROD_VPS_SSH_KEY`
	- `PROD_VPS_HOST`
	- `PROD_VPS_USER`
	- `PROD_VPS_SSH_KEY`
	- `PROD_VPS_PORT` (tuỳ chọn, mặc định `22`)
	- `PROD_VPS_SSH_PASSPHRASE` (tuỳ chọn)
	- `PROD_VPS_SERVICE_NAME` (tuỳ chọn, nếu bỏ trống workflow sẽ restart app bằng process, không dùng systemd)
	- `PROD_VPS_HEALTHCHECK_URL` (tuỳ chọn)
	- `PROD_APP_ASPNETCORE_ENVIRONMENT` (tuỳ chọn, mặc định `Production`)
	- `PROD_APP_ASPNETCORE_URLS` (tuỳ chọn, mặc định `http://0.0.0.0:5005`)
	- `PROD_APP_MONGODB_CONNECTION_STRING` (bắt buộc)
	- `PROD_APP_MONGODB_DATABASE_NAME` (tuỳ chọn)
	- `PROD_APP_SEEDADMIN_USERNAME` (tuỳ chọn)
	- `PROD_APP_SEEDADMIN_EMAIL` (tuỳ chọn)
	- `PROD_APP_SEEDADMIN_PASSWORD` (tuỳ chọn)
5. Tạo secrets cho staging nếu cần deploy staging:
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
- Nếu không dùng `PROD_VPS_SERVICE_NAME`, workflow sẽ tự chạy `dotnet .../current/LoggingActivity.Web.dll` bằng process mode.
- Cổng `5005` chỉ cần app lắng nghe nội bộ để Nginx proxy (`127.0.0.1:5005`), không bắt buộc mở public internet.
- Process mode ưu tiên đọc biến runtime từ GitHub Secrets `PROD_APP_*`, không cần cấu hình thêm file trên server.
- Workflow sẽ fail sớm nếu thiếu `PROD_APP_MONGODB_CONNECTION_STRING`.
- SeedAdmin sẽ tự động tắt khi thiếu `PROD_APP_SEEDADMIN_*`, app vẫn khởi động bình thường.
- Nếu có `PROD_VPS_SERVICE_NAME` mà unit chưa tồn tại, workflow sẽ tự tạo systemd unit chuẩn và enable service.
- Nếu dùng systemd, `ExecStart` sẽ chạy wrapper script `shared/run-current.sh` để nạp env và chạy `current/LoggingActivity.Web.dll`.
- Biến môi trường runtime (`MongoDb__*`, `SeedAdmin__*`) nên set trong service để app khởi động ổn định sau mỗi lần deploy.

Thiết lập Nginx reverse proxy:

1. Copy file mẫu [tools/nginx.logging.conf](tools/nginx.logging.conf) lên server:
	- `/etc/nginx/sites-available/logging`
2. Đổi `server_name` trong file theo domain thật.
3. Đảm bảo đã có SSL certificate cho domain (file hiện dùng đường dẫn Let's Encrypt):

```bash
sudo certbot --nginx -d logging.tima.vn -d www.logging.tima.vn
```
4. Bật site và tắt site mặc định:

```bash
sudo ln -sf /etc/nginx/sites-available/logging /etc/nginx/sites-enabled/logging
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
```

Kiểm tra nhanh:

```bash
curl -I http://127.0.0.1:5005
curl -I http://your-domain.com
```

Ghi chú tự động hóa:

- Mỗi lần deploy thành công, workflow sẽ tự copy `tools/nginx.logging.conf` vào `/etc/nginx/sites-available/logging`, chạy `nginx -t`, rồi `systemctl reload nginx`.
- Server cần quyền `sudo` cho user deploy để thực thi các lệnh Nginx ở trên.

Sau mỗi deploy workflow sẽ in 80 dòng cuối của log app (`/var/www/logging/shared/logs/app.log`) để kiểm tra nhanh tình trạng runtime.

Tuỳ chọn: vẫn có thể override bằng file `/var/www/logging/shared/app.env`:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5005
MongoDb__ConnectionString=mongodb://<host>:27017/<db>
MongoDb__DatabaseName=<db>
SeedAdmin__UserName=admin
SeedAdmin__Email=admin@example.com
SeedAdmin__Password=<strong-password>
```

Tạo nhanh trên server:

```bash
mkdir -p /var/www/logging/shared
nano /var/www/logging/shared/app.env
chmod 600 /var/www/logging/shared/app.env
```

Lỗi thường gặp `ssh: unable to authenticate, attempted methods [none]`:

- Secret SSH key rỗng/sai tên hoặc đặt sai scope (repo vs environment).
- `PROD_VPS_USER` không đúng user đã được thêm public key trong `~/.ssh/authorized_keys`.
- Private key không khớp public key trên server.

Lỗi thường gặp `Unit ... service not found`:

- Secret `PROD_VPS_SERVICE_NAME` không khớp unit đang tồn tại trên server.
- Kiểm tra danh sách unit bằng lệnh `systemctl list-unit-files --type=service | grep -Ei 'logging|activity'`.

## Ghi chú

- App cần MongoDB để khởi động.
- Seed admin được tạo từ cấu hình `SeedAdmin:*`.
- `appsettings.Development.json` chỉ nên giữ placeholder an toàn.
