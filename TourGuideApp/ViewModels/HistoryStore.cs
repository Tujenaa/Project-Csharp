using System.Collections.ObjectModel;
using TourGuideApp.Models;

namespace TourGuideApp.ViewModels
{
    public static class HistoryStore
    {
        public static ObservableCollection<POI> History { get; } = new();

        public static void Add(POI poi)
        {
            if (poi == null) return;

            History.Insert(0, poi); // thêm lên đầu list
        }

        public static void Clear()
        {
            History.Clear();
        }
    }
}