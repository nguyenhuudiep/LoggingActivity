# Logging Activity User

He thong ASP.NET Core MVC ket hop MongoDB de tiep nhan activity log tu he thong khac qua API, luu tru tap trung, tra cuu va thong ke.

## Chuc nang

- Dang nhap bang cookie authentication cho khu vuc quan tri.
- Quan ly tai khoan va phan quyen Admin, Auditor.
- Dashboard log co loc, phan trang va thong ke 7 ngay.
- Quan ly partner gui log voi API key rieng, chi can cau hinh ten partner va trang thai hoat dong.
- API tich hop su dung header `X-Api-Key` de gui activity log vao he thong.

## Cau hinh

Cap nhat file `LoggingActivity.Web/appsettings.json`:

- `MongoDb:ConnectionString`: chuoi ket noi MongoDB.
- `MongoDb:DatabaseName`: ten database.
- `SeedAdmin:*`: tai khoan admin duoc tao tu dong luc app khoi dong lan dau.

Khong luu credential that trong `appsettings.Development.json` hoac source control. Project da bat `User Secrets`, nen voi moi truong local hay dat secret bang lenh:

```powershell
dotnet user-secrets set "MongoDb:ConnectionString" "<your-mongodb-connection-string>" --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Neu can cho server hoac CI/CD, uu tien dung environment variable:

```powershell
$env:MongoDb__ConnectionString = "<your-mongodb-connection-string>"
```

`appsettings.Development.json` chi nen chua cac gia tri khong nhay cam nhu `DatabaseName`.

## Chay ung dung

```powershell
dotnet restore
dotnet run --project .\LoggingActivity.Web\LoggingActivity.Web.csproj
```

Tai khoan mac dinh:

- Username: `admin`
- Password: `Admin@123456`

## API tich hop

Partner duoc cau hinh trong menu quan tri va he thong tu sinh API key rieng. Khong can nhap ma doi tac hoac email lien he.

Them header:

```http
X-Api-Key: your-integration-api-key
```

Endpoint:

```http
POST /api/partner/activity
GET /api/partner/activity?page=1&pageSize=10
GET /api/partner/statistics
```

`POST /api/partner/activity` la luong ghi log chinh. He thong khong con tu dong ghi nhan request hay click tu giao dien quan tri hien tai.