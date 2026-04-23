# Phát hành / cài đặt TaskManagerPlus

## Vì sao copy mỗi `TaskManagerPlus.exe` ra ngoài lại không chạy?

Vì app **phụ thuộc** vào các file đi kèm (các `.dll` + thư mục `Help/` + `Localization/` + file `.config`). Nếu thiếu, Windows/.NET sẽ báo lỗi và app không khởi động được.

## Cách phát hành cho người dùng

### 1) Portable (không cần installer)

Phát hành **nguyên folder** hoặc `.zip` trong `dist/`:
- `dist/TaskManagerPlus-win-x64-Release-1.0.0-YYYY-MM-DD/`
- `dist/TaskManagerPlus-win-x64-Release-1.0.0-YYYY-MM-DD.zip`

Người dùng chỉ cần giải nén và chạy `TaskManagerPlus.exe` **trong folder đó**.

### 2) Installer (khuyến nghị nếu muốn “cài đặt” đúng kiểu Windows)

Repo đã có script để build installer bằng **Inno Setup**:
- Hướng dẫn: `installer/BUILD.md`
- File script Inno: `installer/TaskManagerPlus.iss`

Khi build xong sẽ ra:
- `dist/TaskManagerPlus-Setup-1.0.0.exe`

Installer sẽ tự copy toàn bộ file phụ thuộc vào `Program Files`, tạo shortcut, có uninstaller.

## Lưu ý

- App đang target **.NET Framework 4.7.2**: máy người dùng cần cài .NET Framework 4.7.2 (hoặc mới hơn) thì mới chạy được.
- App manifest đang yêu cầu quyền admin (`requireAdministrator`), nên khi chạy sẽ hiện UAC.

