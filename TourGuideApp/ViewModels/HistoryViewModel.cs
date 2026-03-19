using System.Collections.ObjectModel;

namespace TourGuideApp.ViewModels
{
    public class HistoryViewModel
    {
        public ObservableCollection<string> HistoryList { get; set; }

        public HistoryViewModel()
        {
            HistoryList = new ObservableCollection<string>();
        }

        public void ClearHistory()
        {
            HistoryList.Clear();
        }
    }
}