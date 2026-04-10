# Hướng dẫn tạo Migration và Cập nhật Database cho Cơ sở dữ liệu phân tán

Tài liệu này hướng dẫn bạn cách sử dụng Package Manager Console (PMC) trong Visual Studio để tạo ra 2 cơ sở dữ liệu vật lý riêng biệt (`BankA_DB` và `BankB_DB`) dựa trên các DbContext đã cấu hình.

## Yêu cầu trước khi chạy lệnh
1. Mở Visual Studio.
2. Mở **Package Manager Console** (Vào menu `Tools` > `NuGet Package Manager` > `Package Manager Console`).
3. Đảm bảo SQL Server đang chạy (LocalDB hoặc thể hiện đã cấu hình).

---

## 1. Thiết lập Database cho BankA (BankA_DB)

Trong Package Manager Console, chạy lần lượt 2 lệnh sau:

### Lệnh 1: Tạo Migration (Vì BankA đã có Database, ta tạo migration mới để cập nhật dữ liệu)
```powershell
Add-Migration InitialCreate -Project BankA.Api -Context BankA.Api.Data.BankDbContext
```
**Giải thích:** Lệnh này sẽ quét thư mục `BankA.Api` và file `BankDbContext.cs` để tạo ra thư mục `Migrations` chứa các file định nghĩa bảng cho Bank A.

### Lệnh 2: Đẩy Migration xuống Database
```powershell
Update-Database -Project BankA.Api -Context BankA.Api.Data.BankDbContext
```
**Giải thích:** Lệnh này tiến hành thực thi file migration vừa tạo, tạo Database `BankA_DB` trong SQL Server và insert sẵn 2 dòng dữ liệu (A001, A002) với số dư là 100,000.

---

## 2. Thiết lập Database cho BankB (BankB_DB)

Sau khi hoàn tất BankA, tiếp tục chạy 2 lệnh sau cho BankB:

### Lệnh 1: Tạo Migration ban đầu
```powershell
Add-Migration InitialCreate -Project BankB.Api -Context BankB.Api.Data.BankDbContext
```
**Giải thích:** Tương tự, lệnh này tạo thư mục `Migrations` bên trong project `BankB.Api`.

### Lệnh 2: Đẩy Migration xuống Database
```powershell
Update-Database -Project BankB.Api -Context BankB.Api.Data.BankDbContext
```
**Giải thích:** EF Core sẽ dựa trên connection string của `BankB.Api` để tạo một Database mới hoàn toàn tên là `BankB_DB`. Tiếp theo, EF Core sẽ tự động insert sẵn 2 tài khoản (B001, B002) với số dư 100,000.

---

## Kiểm tra kết quả
- Mở **SQL Server Management Studio (SSMS)**.
- Connect vào `localhost` (hoặc tên Server bạn đã dùng trong DB connection string).
- Refresh lại cây thư mục `Databases`.
- Bạn sẽ thấy 2 cơ sở dữ liệu xuất hiện:
  - `BankA_DB`: Chứa bảng `Accounts` với record `A001` và `A002`.
  - `BankB_DB`: Chứa bảng `Accounts` với record `B001` và `B002`.
