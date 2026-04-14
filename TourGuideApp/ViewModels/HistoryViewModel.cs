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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        private int _totalListened;
        public int TotalListened
        {
            get => _totalListened;
            private set { _totalListened = value; OnPropertyChanged(nameof(TotalListened)); }
        }

        private string _totalDuration = "0";
        public string TotalDuration
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
        public ICommand LoadDataCommand { get; }

        public HistoryViewModel()
        {
            ClearHistoryCommand = new Command(ClearHistory);
            ReplayCommand = new Command<HistoryItem>(OnReplay);
            LoadDataCommand = new Command(async () => await LoadDataAsync());

            HistoryStore.OnItemAdded += HandleItemAdded;

            // Làm mới bản dịch khi ngôn ngữ thay đổi
            LocalizationDataManager.Instance.PropertyChanged += (s, e) => 
            {
                Rebuild(); // Gọi Rebuild để tính toán lại DateLabel
            };

            Rebuild();
        }

        public async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                await HistoryStore.LoadFromApiAsync();
                Rebuild();
            }
            finally
            {
                IsLoading = false;
            }
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
            TotalDuration = (allItems.Sum(i => i.ListenDuration) / 60.0).ToString("F1"); // Hiển thị 1 chữ số thập phân
            IsHistoryEmpty = allItems.Count == 0;
            
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsNotLoggedIn));
        }

        static string DateLabel(DateTime date)
        {
            var today = DateTime.Today;
            if (date == today) return LocalizationService.Get("today").ToUpper();
            if (date == today.AddDays(-1)) return LocalizationService.Get("yesterday").ToUpper();
            return date.ToString("dd/MM/yyyy");
        }

        public bool IsLoggedIn => AuthService.IsLoggedIn;
        public bool IsNotLoggedIn => !AuthService.IsLoggedIn;

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