using System.Collections.Generic;
using TourGuideApp.Models;
using TourGuideApp.Services;

namespace TourGuideApp.ViewModels
{
    /// <summary>
    /// Model đại diện cho một mục lịch sử nghe.
    /// </summary>
    public class HistoryItem
    {
        public POI Poi { get; init; } = null!;
        public DateTime ListenedAt { get; init; }

        // Forwarded properties for XAML binding
        public string Name => Poi?.Name ?? "";
        public string? ImageUrl => Poi?.MainImage;
    }

    /// <summary>
    /// Singleton store cho lịch sử nghe — in-memory, dùng chung toàn app.
    /// Khi TTS phát xong một POI → gọi HistoryStore.Add(poi).
    /// </summary>
    public static class HistoryStore
    {
        private static readonly List<HistoryItem> _items = new();
        private static readonly object _lock = new();

        /// Raised trên bất kỳ thread nào khi có mục mới được thêm vào
        public static event Action<HistoryItem>? OnItemAdded;

        public static async Task AddAsync(POI poi)
        {
            if (poi == null) return;

            int userId = Preferences.Get("user_id", -1);
            if (userId <= 0)
            {
                // Khách không lưu lịch sử
                return;
            }

            var item = new HistoryItem
            {
                Poi = poi,
                ListenedAt = DateTime.Now
            };

            lock (_lock) { _items.Insert(0, item); }

            OnItemAdded?.Invoke(item);

            await new ApiService().SaveHistory(poi.Id, userId);
        }

        public static async Task LoadFromApiAsync()
        {
            int userId = Preferences.Get("user_id", -1);
            if (userId <= 0) return;

            var items = await new ApiService().GetHistory(userId);

            if (items == null) return;

            lock (_lock)
            {
                _items.Clear();
                foreach (var item in items.OrderBy(i => (DateTime)i.PlayTime))
                {
                    _items.Insert(0, new HistoryItem
                    {
                        Poi = new POI
                        {
                            Id = item.PoiId,
                            Name = item.PoiName ?? "",
                            Images = !string.IsNullOrEmpty(item.PoiImage) ? new List<string> { item.PoiImage } : new()
                        },
                        ListenedAt = item.PlayTime
                    });
                }
            }
        }

        public static List<HistoryItem> GetAll()
        {
            lock (_lock) { return new List<HistoryItem>(_items); }
        }

        public static void Clear()
        {
            lock (_lock) { _items.Clear(); }
        }
    }
}