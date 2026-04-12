namespace TourGuideApp.Models;

/// <summary>
/// Static state để truyền tour đã chọn từ HomePage sang MapPage.
/// </summary>
public static class MapTourState
{
    public static Tour? SelectedTour { get; set; }
    public static int? FocusPoiId { get; set; }
}
