using System;
using System.Collections.Generic;
using TourGuideApp.Models;
using TourGuideApp.Services;
using TourGuideApp.ViewModels;

using System.Collections.ObjectModel;

public class HomeViewModel
{
    private readonly ApiService _poiService = new();

    public ObservableCollection<POI> TopPOIs { get; set; } = new();
    public ObservableCollection<POI> AllPOIs { get; set; } = new();

    public async Task LoadData()
    {
        var top = await _poiService.GetTopPOI();
        var all = await _poiService.GetPOI();

        TopPOIs.Clear();
        foreach (var item in top)
            TopPOIs.Add(item);

        AllPOIs.Clear();
        foreach (var item in all)
            AllPOIs.Add(item);
    }
}
