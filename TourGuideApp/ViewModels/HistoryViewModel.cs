using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using TourGuideApp.Models;
using TourGuideApp.Services;

namespace TourGuideApp.ViewModels
{
    /// <summary>
    /// ViewModel cho màn hình lịch sử nghe.
    /// Dữ liệu được đẩy vào từ HistoryStore khi TTS phát xong.
    /// </summary>
    public class HistoryViewModel : INotifyPropertyChanged
    {
        // ── Binding properties ────────────────────────────────────────────────

        public ObservableCollection<HistoryGroup> HistoryGroups { get; } = new();

        private int _totalListened;
        public int TotalListened
        {
            get => _totalListened;
            private set { _totalListened = value; OnPropertyChanged(nameof(TotalListened)); }
        }

        private int _totalDuration;
        public int TotalDuration
        {
            get => _totalDuration;
            private set { _totalDuration = value; OnPropertyChanged(nameof(TotalDuration)); }
        }

        private bool _isHistoryEmpty = true;
        public bool IsHistoryEmpty
        {
            get => _isHistoryEmpty;
            private set { _isHistoryEmpty = value; OnPropertyChanged(nameof(IsHistoryEmpty)); }
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand ClearHistoryCommand { get; }
        public ICommand ReplayCommand { get; }

        public HistoryViewModel()
        {
            ClearHistoryCommand = new Command(ClearHistory);
            ReplayCommand = new Command<HistoryItem>(OnReplay);

            HistoryStore.OnItemAdded += HandleItemAdded;
            Rebuild();
        }

        // ── Public methods ────────────────────────────────────────────────────

        public void ClearHistory()
        {
            HistoryStore.Clear();
            Rebuild();
        }

        /// Được HistoryPage gắn/tháo qua OnAppearing/OnDisappearing
        public void HandleItemAdded(HistoryItem item)
        {
            MainThread.BeginInvokeOnMainThread(Rebuild);
        }

        // ── Private ───────────────────────────────────────────────────────────

        void OnReplay(HistoryItem? item)
        {
            if (item?.Poi == null) return;
            _ = Shell.Current.GoToAsync("placeDetail",
                new Dictionary<string, object> { { "poi", item.Poi } });
        }

        void Rebuild()
        {
            HistoryGroups.Clear();

            var allItems = HistoryStore.GetAll();

            var grouped = allItems
                .GroupBy(h => h.ListenedAt.Date)
                .OrderByDescending(g => g.Key);

            foreach (var g in grouped)
            {
                string key = DateLabel(g.Key);
                var group = new HistoryGroup(key);
                foreach (var item in g.OrderByDescending(h => h.ListenedAt))
                    group.Add(item);
                HistoryGroups.Add(group);
            }

            TotalListened = allItems.Count;
            TotalDuration = allItems.Count * 3;
            IsHistoryEmpty = allItems.Count == 0;
        }

        static string DateLabel(DateTime date)
        {
            var today = DateTime.Today;
            if (date == today) return "HÔM NAY";
            if (date == today.AddDays(-1)) return "HÔM QUA";
            return date.ToString("dd/MM/yyyy");
        }

        public async Task LoadFromApi()
        {
            if (SessionService.CurrentUser == null) return;

            var api = new ApiService();
            var data = await api.GetHistory(SessionService.CurrentUser.Id);

            HistoryGroups.Clear();

            var grouped = data
                .GroupBy(h => h.PlayTime.Date)
                .OrderByDescending(g => g.Key);

            foreach (var g in grouped)
            {
                var group = new HistoryGroup(g.Key.ToString("dd/MM/yyyy"));

                foreach (var item in g)
                {
                    group.Add(new HistoryItem
                    {
                        Poi = new POI
                        {
                            Id = item.PoiId,
                            Name = item.PoiName ?? ""
                        },
                        ListenedAt = item.PlayTime
                    });
                }

                HistoryGroups.Add(group);
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Nhóm lịch sử theo ngày — dùng cho CollectionView IsGrouped="True"</summary>
    public class HistoryGroup : ObservableCollection<HistoryItem>
    {
        public string Key { get; }
        public HistoryGroup(string key) { Key = key; }
    }

    
}