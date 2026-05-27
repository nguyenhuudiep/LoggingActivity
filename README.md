# Logging Activity

Hệ thống ASP.NET Core MVC kết hợp MongoDB để tiếp nhận activity log từ hệ thống khác qua API, lưu trữ tập trung, tra cứu, thống kê và cảnh báo theo ngưỡng cấu hình.

## Chức năng chính

- Đăng nhập bằng cookie authentication cho khu vực quản trị.
- Quản lý tài khoản và phân quyền `Admin`, `Auditor`.
- Quản lý partner tích hợp với API key riêng cho từng partner.
- Quản lý danh mục action log và cấu hình cảnh báo theo số log tối đa trong ngày.
- Dashboard tổng quan log theo ngày, partner và action.
- Màn hình `Log và thống kê` có lọc dữ liệu, phân trang và cảnh báo active trong ngày.
- Màn hình `Lịch sử cảnh báo` lưu lại toàn bộ cảnh báo đã phát sinh theo thời điểm.

## Cấu hình

File `LoggingActivity.Web/appsettings.json` chỉ giữ placeholder an toàn cho MongoDB connection string. Không lưu connection string thật trong source control.

Thiết lập các giá trị sau bằng user-secrets hoặc environment variables:

- `MongoDb:ConnectionString`: chuỗi kết nối MongoDB.
- `MongoDb:DatabaseName`: tên database nếu chuỗi kết nối chưa chứa sẵn database.
- `SeedAdmin:*`: tài khoản admin được tạo tự động khi khởi động lần đầu.

Project đã bật `User Secrets`, nên với môi trường local có thể cấu hình bằng lệnh:

```powershell
dotnet user-secrets set "MongoDb:ConnectionString" "<your-mongodb-connection-string>" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet user-secrets set "SeedAdmin:UserName" "admin" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet user-secrets set "SeedAdmin:Password" "<your-strong-password>" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Nếu connection string của bạn chưa có phần database ở cuối URI, thêm tiếp:

```powershell
dotnet user-secrets set "MongoDb:DatabaseName" "logging_activity_db" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Nếu trước đó đã có secret cũ, có thể kiểm tra hoặc ghi đè bằng:

```powershell
dotnet user-secrets list --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Nếu chạy trên server hoặc CI/CD, ưu tiên dùng environment variable:

```powershell
$env:MongoDb__ConnectionString = "<your-mongodb-connection-string>"
$env:SeedAdmin__UserName = "admin"
$env:SeedAdmin__Email = "admin@example.com"
$env:SeedAdmin__Password = "<your-strong-password>"
```

Chỉ cần thêm `MongoDb__DatabaseName` nếu connection string không tự chứa database name.

## Deploy qua GitHub Actions lên Linux VPS

Repo đã có workflow mẫu tại [.github/workflows/deploy-linux-vps.yml](.github/workflows/deploy-linux-vps.yml). Workflow này sẽ:

- build và publish project `LoggingActivity.Web`
- upload artifact publish
- copy artifact lên Linux VPS qua SSH
- restart service bằng `systemctl`

### 1. Tạo GitHub Secrets

Trong GitHub repo, vào `Settings -> Secrets and variables -> Actions` và tạo các secret sau:

- `VPS_HOST`: IP hoặc domain của server Linux
- `VPS_PORT`: cổng SSH, thường là `22`
- `VPS_USER`: user dùng để deploy
- `VPS_SSH_KEY`: private key SSH của user deploy
- `VPS_APP_PATH`: thư mục chứa app trên server, ví dụ `/var/www/loggingactivity`
- `VPS_SERVICE_NAME`: tên service systemd, ví dụ `loggingactivity`

### 2. Cấu hình app trên server

Ví dụ tạo file service tại `/etc/systemd/system/loggingactivity.service`:

```ini
[Unit]
Description=Logging Activity Web
After=network.target

[Service]
WorkingDirectory=/var/www/loggingactivity
ExecStart=/usr/bin/dotnet /var/www/loggingactivity/LoggingActivity.Web.dll
Restart=always
RestartSec=5
SyslogIdentifier=loggingactivity
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5137
Environment=MongoDb__ConnectionString=<your-mongodb-connection-string>
Environment=MongoDb__DatabaseName=logging_activity_db
Environment=SeedAdmin__UserName=admin
Environment=SeedAdmin__Email=admin@example.com
Environment=SeedAdmin__Password=<your-strong-password>

[Install]
WantedBy=multi-user.target
```

Sau đó trên VPS chạy:

```bash
sudo systemctl daemon-reload
sudo systemctl enable loggingactivity
sudo systemctl start loggingactivity
```

### 3. Luồng deploy

- push code lên nhánh `main`, hoặc chạy workflow thủ công bằng `workflow_dispatch`
- GitHub Actions sẽ build, publish, copy file lên VPS và restart service
- kiểm tra log service bằng lệnh `sudo journalctl -u loggingactivity -f`

### 4. Reverse proxy gợi ý

Nên chạy app sau Nginx. Ví dụ upstream tới `http://127.0.0.1:5137` rồi public domain qua Nginx để xử lý TLS/HTTPS ổn định hơn.

## Chạy ứng dụng

```powershell
dotnet restore
dotnet build .\LoggingActivity.Web\LoggingActivity.Web.csproj
dotnet run --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Tài khoản seed admin được tạo từ cấu hình `SeedAdmin:*` trong user-secrets hoặc environment variables.

## API tích hợp

Partner được cấu hình trong menu quản trị và hệ thống tự sinh API key riêng. Khi gọi API, partner phải truyền header:

```http
X-Api-Key: your-partner-api-key
```

Base endpoint:

```http
POST /api/partner/activity
GET  /api/partner/activity?page=1&pageSize=10
GET  /api/partner/statistics
```

### 1. Gửi activity log

API chính để hệ thống đối tác đẩy log vào hệ thống:

```bash
curl -X POST "http://localhost:5137/api/partner/activity" \
	-H "Content-Type: application/json" \
	-H "X-Api-Key: YOUR_PARTNER_API_KEY" \
	-d '{
		"userId": 260001,
		"userName": "nguyen.van.a",
		"action": "Login",
		"description": "Nội dung mô tả từ đối tác",
		"endpoint": "/auth/login"
	}'
```

Payload mẫu:

```json
{
	"userId": 260001,
	"userName": "nguyen.van.a",
	"action": "Login",
	"description": "Nội dung mô tả từ đối tác",
	"endpoint": "/auth/login"
}
```

Response thành công:

```json
{
	"message": "Đã ghi nhận activity log."
}
```

Lưu ý:

- `action` phải tồn tại và đang active trong màn `Action log`.
- Hệ thống hiện chuẩn hóa mô tả log hiển thị về định dạng: `Partner {partnerName}, username {userName} thực hiện thao tác {action}.`
- Hệ thống không tự ghi log click hoặc request từ giao diện quản trị.

### 2. Lấy danh sách log của partner

API này chỉ trả về dữ liệu của partner đang gọi bằng chính API key đó.

```bash
curl "http://localhost:5137/api/partner/activity?page=1&pageSize=10&from=2026-05-26&to=2026-05-26&action=Login" \
	-H "X-Api-Key: YOUR_PARTNER_API_KEY"
```

Response mẫu:

```json
{
	"items": [
		{
			"partnerId": "6a152cdb5e3857756e533609",
			"partnerName": "ERP",
			"externalUserId": 260001,
			"userName": "nguyen.van.a",
			"action": "Login",
			"description": "Partner ERP, username nguyen.van.a thực hiện thao tác Login.",
			"createdAtUtc": "2026-05-26T09:00:00Z"
		}
	],
	"totalCount": 1,
	"page": 1,
	"pageSize": 10,
	"totalPages": 1
}
```

### 3. Lấy thống kê log của partner

```bash
curl "http://localhost:5137/api/partner/statistics?from=2026-05-20&to=2026-05-26" \
	-H "X-Api-Key: YOUR_PARTNER_API_KEY"
```

Response mẫu:

```json
{
	"totalLogs": 120,
	"todayLogs": 18,
	"integratedLogs": 120,
	"dailyActivity": [
		{ "label": "2026-05-20", "value": 12 },
		{ "label": "2026-05-21", "value": 14 }
	],
	"topActions": [
		{ "label": "Login", "value": 35 },
		{ "label": "Search", "value": 22 }
	]
}
```

## Ghi chú vận hành

- Cảnh báo active trong ngày được tính theo `userId + action + ngày`.
- Lịch sử cảnh báo được lưu riêng để tra cứu lại theo mốc thời gian.
- Màn hình hướng dẫn tích hợp API cũng có sẵn trực tiếp trong dashboard qua nút `Chi tiết` ở box `API tích hợp`.