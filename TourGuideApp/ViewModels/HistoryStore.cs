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
        //public string? ImageUrl => Poi?.ImageUrl;
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

            var item = new HistoryItem
            {
                Poi = poi,
                ListenedAt = DateTime.Now
            };

            lock (_lock) { _items.Insert(0, item); }

            OnItemAdded?.Invoke(item);

            if (SessionService.CurrentUser != null)
            {
                await new ApiService().SaveHistory(
                    poi.Id,
                    SessionService.CurrentUser.Id
                );
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