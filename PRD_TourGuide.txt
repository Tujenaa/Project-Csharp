# PRD - Thuyết minh tự động phố ẩm thực Vĩnh Khánh

## 1. Thông tin tài liệu
- Product: Tour Guide
- Phiên bản PRD: 1.0  
- Thời gian: 24-03-2026  

## 2. Tóm tắt sản phẩm
### 2.1 Giới thiệu
Tour Guide (GPS Audio Guide)là một ứng dụng di động được xây dựng bằng .NET MAUI, cho phép tự động phát nội dung thuyết minh về các điểm tham quan (POI) gần người dùng thông qua GPS và cơ chế geofence. Ứng dụng hỗ trợ hoạt động offline bằng SQLite và sử dụng công nghệ Text-to-Speech (TTS).

Hệ thống bao gồm:
- Ứng dụng di động dành cho người dùng (khách tham quan)
- Trang web quản trị (ASP.NET Core Razor Pages) để quản lý dữ liệu POI và nội dung.

### 2.2 Đối tượng người dùng
Hệ thống phục vụ ba nhóm người dùng chính:
- Khách tham quan: sử dụng ứng dụng để nghe thuyết minh khi tham quan
- Quản trị viên: quản lý dữ liệu POI, nội dung audio, và cấu hình hệ thống
- Đơn vị du lịch: sử dụng dữ liệu phân tích để cải thiện chất lượng dịch vụ

## 3. Tiêu Chí Thành Công

Hệ thống được xem là thành công khi:
- GPS tracking hoạt động ổn định
- Geofence chính xác
- TTS phát đúng nội dung
- Không xảy ra phát trùng
- Ứng dụng chạy được offline

## 4. Các hạn chế của hệ thống hiện tại 
- Chưa xây dựng lộ trình tham quan rõ ràng cho người dùng
- Giao diện (UI/UX) chưa được tối ưu, còn đơn giản
- Chưa có chức năng tìm kiếm và lọc POI
- Chưa có hệ thống đề xuất (recommendation)
# END