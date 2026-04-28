namespace TourGuideApp.Services;

public static class QrCodeService
{
    /// <summary>
    /// Phân tích mã QR hoặc URI để lấy POI ID.
    /// Chấp nhận cả chuỗi text thô từ QR scanner hoặc Uri từ App Link.
    /// </summary>
    public static int? ParsePoiId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // 1. Hỗ trợ định dạng cũ: https://poi:123
        if (input.StartsWith("https://poi:"))
        {
            if (int.TryParse(input.Replace("https://poi:", ""), out int id)) return id;
        }

        try
        {
            if (!input.Contains("://") && !input.StartsWith("https://"))
            {
                // Nếu là chuỗi thô không phải URI, thử parse trực tiếp
                if (int.TryParse(input, out int rawId)) return rawId;
                return null;
            }

            var uri = new Uri(input);
            return ParsePoiId(uri);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Phân tích Uri để lấy POI ID (Dùng cho OnAppLinkRequestReceived)
    /// </summary>
    public static int? ParsePoiId(Uri uri)
    {
        if (uri == null) return null;

        // A. Hỗ trợ Custom Scheme: tourguideapp://poi/123 hoặc tourguideapp://guest?poi=123
        if (string.Equals(uri.Scheme, "tourguideapp", StringComparison.OrdinalIgnoreCase))
        {
            // Trường hợp: tourguideapp://poi/123
            if (string.Equals(uri.Host, "poi", StringComparison.OrdinalIgnoreCase))
            {
                string lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? "";
                if (int.TryParse(lastSegment, out int id)) return id;
            }
            
            // Trường hợp: tourguideapp:///poi/123 (3 slashes)
            if (string.IsNullOrEmpty(uri.Host) && uri.LocalPath.StartsWith("/poi/"))
            {
                string lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? "";
                if (int.TryParse(lastSegment, out int id)) return id;
            }

            // Trường hợp: tourguideapp://guest?poi=123
            return GetQueryParam(uri.Query, "poi");
        }

        // B. Hỗ trợ URL chuẩn: https://tourguide.vn/poi/123
        if (uri.Host.Contains("tourguide.vn"))
        {
            if (uri.AbsolutePath.Contains("/poi/"))
            {
                string lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? "";
                if (int.TryParse(lastSegment, out int id)) return id;
            }
        }

        // C. Hỗ trợ GitHub Pages: https://tujenaa.github.io/Project-Csharp/?poi=123
        if (uri.Host.EndsWith("github.io", StringComparison.OrdinalIgnoreCase))
        {
            // Thử lấy từ Query: ?poi=123
            int? queryId = GetQueryParam(uri.Query, "poi");
            if (queryId != null) return queryId;
            
            // Thư lấy từ Path: /Project-Csharp/poi/123
            if (uri.AbsolutePath.Contains("/poi/"))
            {
                string lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? "";
                if (int.TryParse(lastSegment, out int pathId)) return pathId;
            }
        }

        return null;
    }

    private static int? GetQueryParam(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        try
        {
            // Manual parsing: ?key=value&key2=value2
            string cleaned = query.TrimStart('?');
            var pairs = cleaned.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=');
                if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(kv[1], out int val)) return val;
                }
            }
        }
        catch { }
        return null;
    }
}
