using TourGuideApp.Models;
using TourGuideApp.Utils;
using TourGuideApp.ViewModels;
using System.Diagnostics;

namespace TourGuideApp.Services;

/// <summary>
/// Singleton service quản lý việc phát âm thanh toàn ứng dụng.
/// Đảm bảo chỉ có 1 POI được phát tại một thời điểm và đồng bộ trạng thái lên UI.
/// </summary>
public class AudioPlaybackService
{
    private static AudioPlaybackService? _instance;
    public static AudioPlaybackService Instance => _instance ??= new AudioPlaybackService();

    private readonly TextToSpeechService _ttsService = new();
    private readonly ApiService _apiService = new();
    
    private CancellationTokenSource? _ttsToken;
    private string _currentLoadedLang = "";

    public POI? CurrentPlayingPoi { get; private set; }
    public bool IsPlaying { get; private set; }

    // Event để UI biết mà cập nhật (nếu cần, mặc định dùng INotifyPropertyChanged trên POI)
    public event Action? PlaybackStateChanged;

    private AudioPlaybackService()
    {
        _ttsService.OnFinished += OnTtsFinished;
        _ttsService.OnProgress += OnTtsProgress;
    }

    public async Task PlayAsync(POI poi)
    {
        if (poi == null) return;

        // 1. Nếu đang phát chính POI này -> Resume hoặc không làm gì
        if (CurrentPlayingPoi?.Id == poi.Id && IsPlaying) return;

        string currentLang = SettingService.Instance.Language;

        // ── RESUME: Cùng POI, cùng ngôn ngữ, đang pause ──────────────────────
        if (CurrentPlayingPoi?.Id == poi.Id && !IsPlaying && currentLang == _currentLoadedLang)
        {
            await StartSpeakingAsync(poi);
            return;
        }

        // ── PLAY MỚI hoặc đổi ngôn ngữ ─────────────────────────────────────
        Stop();

        CurrentPlayingPoi = poi;

        // --- ĐỒNG BỘ DỮ LIỆU TỪ API (Nếu online) ---
        if (ConnectivityService.IsConnected)
        {
            var freshPoi = await _apiService.GetPOIById(poi.Id);
            if (freshPoi != null)
            {
                poi.ScriptVi = freshPoi.ScriptVi;
                poi.ScriptEn = freshPoi.ScriptEn;
                poi.ScriptJa = freshPoi.ScriptJa;
                poi.ScriptZh = freshPoi.ScriptZh;
            }
        }

        string finalText = LanguageUtils.GetScript(poi, currentLang);
        
        if (currentLang != _currentLoadedLang)
        {
            _ttsService.LoadText(finalText);
            _currentLoadedLang = currentLang;
        }
        else
        {
            // Cùng ngôn ngữ nhưng play mới -> Force reset index
            _ttsService.ForceLoadText(finalText);
        }

        await StartSpeakingAsync(poi);
    }

    public void Pause()
    {
        if (CurrentPlayingPoi == null || !IsPlaying) return;

        _ttsToken?.Cancel();
        _ttsService.Pause();
        
        IsPlaying = false;
        CurrentPlayingPoi.IsPlaying = false;
        
        PlaybackStateChanged?.Invoke();
    }

    public void Stop()
    {
        _ttsToken?.Cancel();
        _ttsToken?.Dispose();
        _ttsToken = null;

        _ttsService.Stop();
        
        if (CurrentPlayingPoi != null)
        {
            CurrentPlayingPoi.IsPlaying = false;
            // Không reset progress về 0 để người dùng thấy vạch cũ, hoặc reset tùy ý bạn
        }

        IsPlaying = false;
        CurrentPlayingPoi = null;
        PlaybackStateChanged?.Invoke();
    }

    private async Task StartSpeakingAsync(POI poi)
    {
        IsPlaying = true;
        poi.IsPlaying = true;
        PlaybackStateChanged?.Invoke();

        _ttsToken?.Dispose();
        _ttsToken = new CancellationTokenSource();

        try
        {
            await _ttsService.SpeakAsync(_ttsToken.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioService] Error: {ex.Message}");
        }
        finally
        {
            // Nếu kết thúc mà không phải do Pause (IsPlaying vẫn true) -> xử lý xong
            if (IsPlaying && !_ttsService.IsPaused)
            {
                IsPlaying = false;
                poi.IsPlaying = false;
                PlaybackStateChanged?.Invoke();
            }
        }
    }

    private void OnTtsProgress(int current, int total)
    {
        if (CurrentPlayingPoi == null || total == 0) return;
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentPlayingPoi.AudioProgress = (double)current / total;
            CurrentPlayingPoi.AudioDuration = $"{current}/{total} câu";
        });
    }

    private async void OnTtsFinished()
    {
        if (CurrentPlayingPoi == null) return;

        var poi = CurrentPlayingPoi;
        Debug.WriteLine($"[AudioService] Finished -> Saving history for {poi.Name}");

        await HistoryStore.AddAsync(poi);

        IsPlaying = false;
        poi.IsPlaying = false;
        PlaybackStateChanged?.Invoke();
    }
}
