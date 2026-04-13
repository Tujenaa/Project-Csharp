using System;
using System.Collections.Generic;

namespace TourGuideApp.Services
{
    public static class LocalizationService
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            ["vi"] = new()
            {
                ["tour_start_title"] = "Bắt đầu tham quan",
                ["tour_start_msg"] = "Chào mừng bạn đến với tour: {0}. Lộ trình đã được tối ưu từ vị trí của bạn.",
                ["autoplay_title"] = "Thuyết minh tự động",
                ["autoplay_msg"] = "Bạn đang ở gần {0}. Bạn có muốn bắt đầu nghe thuyết minh tự động không?",
                ["ok"] = "OK",
                ["start_now"] = "Bắt đầu ngay",
                ["skip"] = "Bỏ qua",
                ["success"] = "Thành công",
                ["failed"] = "Thất bại",
                ["profile_updated"] = "Cập nhật thông tin thành công 🎉",
                ["name_required"] = "Họ tên không được để trống.",
                ["error"] = "Lỗi",
                ["saved"] = "Đã lưu thành công!",
                ["notification"] = "Thông báo",
                ["choose_language"] = "Chọn ngôn ngữ",
                ["cancel"] = "Huỷ",
                ["confirm_title"] = "Xác nhận",
                ["logout_msg"] = "Bạn có muốn đăng xuất không?",
                ["yes"] = "Có",
                ["no"] = "Không",
                ["feature_under_development"] = "Chức năng này đang được phát triển",
                ["permission_title"] = "Quyền truy cập",
                ["camera_permission_msg"] = "Ứng dụng cần quyền Camera để quét mã QR.",
                ["not_found_title"] = "Không tìm thấy",
                ["qr_not_recognized_msg"] = "Không có thông tin cho mã QR này.",
                ["out_of_route_title"] = "Ngoài lộ trình tour",
                ["out_of_route_msg"] = "Địa điểm \"{0}\" không thuộc tour hiện tại.\nBạn có muốn hủy tour và chỉ đường đến đây không?",
                ["cancel_tour_and_route"] = "Hủy tour & Chỉ đường",
                ["keep_tour"] = "Giữ lại tour",
                ["password_too_short"] = "Mật khẩu phải có ít nhất 6 ký tự.",
                ["password_not_match"] = "Mật khẩu xác nhận không khớp.",
                ["updating_password"] = "Đang cập nhật mật khẩu...",
                ["password_changed_success"] = "Cập nhật mật khẩu thành công 🎉",
                ["change_password_failed"] = "Đổi mật khẩu thất bại. Vui lòng kiểm tra lại mật khẩu cũ.",
                ["old_password_required"] = "Vui lòng nhập mật khẩu cũ."
            },
            ["en"] = new()
            {
                ["tour_start_title"] = "Start Tour",
                ["tour_start_msg"] = "Welcome to tour: {0}. The route has been optimized from your location.",
                ["autoplay_title"] = "Automatic Narration",
                ["autoplay_msg"] = "You are near {0}. Would you like to start the automatic narration?",
                ["ok"] = "OK",
                ["start_now"] = "Start Now",
                ["skip"] = "Skip",
                ["success"] = "Success",
                ["failed"] = "Failed",
                ["profile_updated"] = "Profile updated successfully 🎉",
                ["name_required"] = "Name cannot be empty.",
                ["error"] = "Error",
                ["saved"] = "Saved successfully!",
                ["notification"] = "Notification",
                ["choose_language"] = "Select Language",
                ["cancel"] = "Cancel",
                ["confirm_title"] = "Confirm",
                ["logout_msg"] = "Are you sure you want to logout?",
                ["yes"] = "Yes",
                ["no"] = "No",
                ["feature_under_development"] = "This feature is under development",
                ["permission_title"] = "Access Permission",
                ["camera_permission_msg"] = "The application needs Camera permission to scan QR codes.",
                ["not_found_title"] = "Not Found",
                ["qr_not_recognized_msg"] = "No information available for this QR code.",
                ["out_of_route_title"] = "Out of Route",
                ["out_of_route_msg"] = "Location \"{0}\" is not part of the current tour.\nDo you want to cancel the tour and get directions here?",
                ["cancel_tour_and_route"] = "Cancel Tour & Get Directions",
                ["keep_tour"] = "Keep Tour",
                ["password_too_short"] = "Password must be at least 6 characters.",
                ["password_not_match"] = "Passwords do not match.",
                ["updating_password"] = "Updating password...",
                ["password_changed_success"] = "Password updated successfully 🎉",
                ["change_password_failed"] = "Password change failed. Please check your old password.",
                ["old_password_required"] = "Old password is required."
            }
        };

        public static string Get(string key, params object[] args)
        {
            string lang = SettingService.Instance.Language ?? "vi";
            
            // Nếu không có ngôn ngữ trong dict, fallback về tiếng Anh (en)
            if (!Translations.ContainsKey(lang))
                lang = "en";

            if (Translations[lang].TryGetValue(key, out var translation))
            {
                try
                {
                    return string.Format(translation, args);
                }
                catch
                {
                    return translation;
                }
            }

            // Nếu không tìm thấy key ở ngôn ngữ hiện tại, thử tìm ở tiếng Việt
            if (lang != "vi" && Translations["vi"].TryGetValue(key, out var viTranslation))
            {
                try
                {
                    return string.Format(viTranslation, args);
                }
                catch
                {
                    return viTranslation;
                }
            }

            return key;
        }
    }
}
