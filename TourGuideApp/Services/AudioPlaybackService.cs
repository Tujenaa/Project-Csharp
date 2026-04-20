using TourGuideApp.Models;
using TourGuideApp.Utils;
using TourGuideApp.ViewModels;
using System.Diagnostics;

namespace TourGuideApp.Services;

/// <summary>
/// Singleton service quản lý việc phát âm thanh toàn ứng dụng.
/// Đảm bảo chỉ có 1 POI được phát tại một thời điểm và đồng bộ trạng thái lên UI.
/// Ghi lịch sử khi người dùng nghe tích lũy đủ 10 giây (bao gồm cả resume).
/// </summary>
public class AudioPlaybackService
{
    private static AudioPlaybackService? _instance;
    public static AudioPlaybackService Instance => _instance ??= new AudioPlaybackService();

    private readonly TextToSpeechService _ttsService = new();
    private readonly ApiService _apiService = new();

    private CancellationTokenSource? _ttsToken;
    private string _currentLoadedLang = "";

    // ── Thông báo không có dữ liệu ───────────────────────────────────────────
    /// <summary>
    /// Thông báo hiển thị lên UI khi POI không có script cho ngôn ngữ hiện tại.
    /// null = có script bình thường, không cần hiển thị thông báo.
    /// </summary>
    public string? NoScriptMessage { get; private set; }

    /// <summary>Fired khi NoScriptMessage thay đổi để UI biết cần cập nhật.</summary>
    public event Action? NoScriptMessageChanged;

    // ── Theo dõi thời gian nghe tích lũy ─────────────────────────────────────
    private readonly Stopwatch _listenStopwatch = new();
    private const double HistoryThresholdSeconds = 5.0;
    private const int PauseTimeoutSeconds = 60;
    private const int QueueGapSeconds = 3;
    private bool _listenThresholdReached = false;
    private bool _historyRecorded = false;
    private readonly System.Timers.Timer _uiTimer;
    private CancellationTokenSource? _pauseTimeoutCts;

    // ── Hàng đợi phát tuần tự ────────────────────────────────────────────────
    private readonly List<(POI Poi, bool IsAutoPlay)> _playQueue = new();
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private bool _isManualInterrupt = false;

    public POI? CurrentPlayingPoi { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsCurrentPlayAuto { get; private set; }
    public int ListenSeconds => (int)_listenStopwatch.Elapsed.TotalSeconds;

    public event Action? PlaybackStateChanged;
    public event Action<int>? ListenSecondsChanged;
    
    /// <summary>
    /// Delegate dùng để kiểm tra xem một POI có còn nằm trong bán kính cho phép phát tiếp hay không.
    /// Trả về true nếu hợp lệ, false nếu cần skip.
    /// </summary>
    public Func<POI, bool>? LocationValidator { get; set; }

    private AudioPlaybackService()
    {
        _ttsService.OnFinished += OnTtsFinished;
        _ttsService.OnProgress += OnTtsProgress;

        _uiTimer = new System.Timers.Timer(1000);
        _uiTimer.AutoReset = true;
        _uiTimer.Elapsed += (_, _) =>
        {
            var poi = CurrentPlayingPoi;
            if (poi != null) CheckAndRecordHistory(poi);
            MainThread.BeginInvokeOnMainThread(() =>
                ListenSecondsChanged?.Invoke(ListenSeconds));
        };
    }

    public async Task EnqueueRangeAsync(IEnumerable<POI> pois, bool isAutoPlay = true)
    {
        lock (_playQueue)
        {
            foreach (var poi in pois)
                _playQueue.Add((poi, isAutoPlay));
        }

        await ProcessQueueAsync();
    }

    public void ClearQueue()
    {
        lock (_playQueue)
        {
            _playQueue.Clear();
        }
        Debug.WriteLine("[AudioService] Play queue cleared.");
    }

    public async Task EnqueueAsync(POI poi, bool isAutoPlay = true)
    {
        lock (_playQueue)
        {
            _playQueue.Add((poi, isAutoPlay));
        }
        await ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        // Giai đoạn 1: Khắc phục Race Condition bằng SemaphoreSlim
        if (!await _queueLock.WaitAsync(0)) return;

        try
        {
            while (true)
            {
                (POI Poi, bool IsAutoPlay) next;
                lock (_playQueue)
                {
                    if (_playQueue.Count == 0) break;
                    next = _playQueue[0];
                    _playQueue.RemoveAt(0);
                }

                // Giai đoạn 3: Chờ nếu đang phát (bao gồm cả phát thủ công ngắt quãng)
                while (IsPlaying)
                {
                    await Task.Delay(500);
                }

                // Kiểm tra điều kiện vị trí nếu là AutoPlay
                if (next.IsAutoPlay && LocationValidator != null && !LocationValidator(next.Poi))
                {
                    Debug.WriteLine($"[AudioService] Skipping '{next.Poi.Name}' - no longer in range.");
                    continue;
                }

                await PlayAsync(next.Poi, next.IsAutoPlay, isInternal: true);

                // Chờ cho đến khi âm thanh hiện tại kết thúc (có thể là điểm hiện tại hoặc điểm chen ngang thủ công)
                while (IsPlaying)
                    await Task.Delay(500);

                // Điểm tiếp theo trong hàng đợi (nếu có)
                POI? nextInQueue = null;
                lock (_playQueue)
                {
                    if (_playQueue.Count > 0) nextInQueue = _playQueue[0].Poi;
                }

                // Giai đoạn 4: Xử lý sau khi bị ngắt quãng thủ công
                if (_isManualInterrupt)
                {
                    _isManualInterrupt = false;

                    if (nextInQueue != null)
                    {
                        bool continueQueue = await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (Application.Current?.MainPage == null) return true;
                            return await Application.Current.MainPage.DisplayAlert(
                                LocalizationService.Get("notification"),
                                LocalizationService.Get("continue_journey_prompt", nextInQueue.Name),
                                LocalizationService.Get("continue"),
                                LocalizationService.Get("stop"));
                        });

                        if (!continueQueue)
                        {
                            ClearQueue();
                            break;
                        }
                    }
                }

                if (nextInQueue != null)
                {
                    // 1. Tải dữ liệu cho điểm tiếp theo song song
                    _ = SyncPoiDataAsync(nextInQueue);

                    // 2. Chờ khoảng nghỉ 3 giây
                    Debug.WriteLine($"[AudioService] Waiting {QueueGapSeconds}s before next POI: {nextInQueue.Name}...");
                    await Task.Delay(TimeSpan.FromSeconds(QueueGapSeconds));
                }
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task PlayAsync(POI poi, bool isAutoPlay = false, bool isInternal = false)
    {
        if (poi == null) return;

        _pauseTimeoutCts?.Cancel();

        // Xử lý ưu tiên điểm thủ công: Nếu đang trong hàng đợi xử lý mà user bấm Play thủ công
        if (!isInternal)
        {
            _isManualInterrupt = true;
            // Xóa POI này khỏi hàng đợi nếu nó đang nằm chờ (để tránh phát lặp lại)
            lock (_playQueue)
            {
                _playQueue.RemoveAll(x => x.Poi.Id == poi.Id);
            }
        }

        if (CurrentPlayingPoi?.Id == poi.Id && IsPlaying) return;

        string currentLang = SettingService.Instance.Language;

        // ── RESUME: Cùng POI, cùng ngôn ngữ, đang pause ──────────────────────
        if (CurrentPlayingPoi?.Id == poi.Id && !IsPlaying && currentLang == _currentLoadedLang)
        {
            _listenStopwatch.Start();
            _uiTimer.Start();
            await StartSpeakingAsync(poi);
            return;
        }
        // ── PLAY MỚI hoặc đổi ngôn ngữ ──────────────────────────────────────
        await StopAsync(clearQueue: !isInternal);

        CurrentPlayingPoi = poi;
        IsCurrentPlayAuto = isAutoPlay;
        _listenStopwatch.Reset();
        _historyRecorded = false;
        _listenThresholdReached = false;

        // Xóa thông báo cũ
        SetNoScriptMessage(null);

        // --- ĐỒNG BỘ DỮ LIỆU TỪ API (Chỉ thực hiện nếu chưa được SyncPoiDataAsync tải trước) ---
        await SyncPoiDataAsync(poi);

        string finalText = LanguageUtils.GetScript(poi, currentLang);

        // ── Không có script → hiển thị thông báo, KHÔNG phát TTS ────────────
        if (string.IsNullOrWhiteSpace(finalText))
        {
            Debug.WriteLine($"[AudioService] No script for lang='{currentLang}', showing UI message.");
            SetNoScriptMessage(BuildNoScriptMessage(currentLang));

            IsPlaying = false;
            poi.IsPlaying = false;
            CurrentPlayingPoi = null;
            PlaybackStateChanged?.Invoke();
            return;
        }

        // ── Có script → phát TTS bình thường ─────────────────────────────────
        if (currentLang != _currentLoadedLang)
        {
            _ttsService.LoadText(finalText);
            _currentLoadedLang = currentLang;
        }
        else
        {
            _ttsService.ForceLoadText(finalText);
        }

        _listenStopwatch.Start();
        _uiTimer.Start();
        await StartSpeakingAsync(poi);
    }

    /// <summary>
    /// Tạo chuỗi thông báo song ngữ (Tiếng Việt + ngôn ngữ hiện tại nếu khác VI).
    /// Ví dụ khi chọn tiếng Nhật:
    ///   "Không có dữ liệu thuyết minh cho địa điểm này.
    ///    この地点の解説データはありません。"
    /// </summary>
    public async Task SyncPoiDataAsync(POI poi)
    {
        if (poi == null) return;
        
        if (ConnectivityService.IsConnected)
        {
            try
            {
                var freshPoi = await _apiService.GetPOIById(poi.Id);
                if (freshPoi != null)
                    poi.Audios = freshPoi.Audios;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioService] SyncPoiDataAsync failed: {ex.Message}");
            }
        }
    }

    private static string BuildNoScriptMessage(string lang)
    {
        return LocalizationService.Get("no_script_available");
    }

    private void SetNoScriptMessage(string? message)
    {
        NoScriptMessage = message;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            NoScriptMessageChanged?.Invoke();

            if (!string.IsNullOrEmpty(message))
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        LocalizationService.Get("notification"), 
                        message, 
                        LocalizationService.Get("ok"));
                }
            }
        });
    }

    public void Pause()
    {
        if (CurrentPlayingPoi == null || !IsPlaying) return;

        _listenStopwatch.Stop();
        _uiTimer.Stop();

        _ttsToken?.Cancel();
        _ttsService.Pause();

        IsPlaying = false;
        CurrentPlayingPoi.IsPlaying = false;

        Debug.WriteLine($"[AudioService] Paused. Accumulated: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");

        PlaybackStateChanged?.Invoke();
        StartPauseTimeout();
    }

    public async Task StopAsync(bool clearQueue = true)
    {
        _pauseTimeoutCts?.Cancel();
        _pauseTimeoutCts = null;

        if (clearQueue)
            ClearQueue();

        if (CurrentPlayingPoi != null && !_historyRecorded && ListenSeconds > 0)
        {
            _historyRecorded = true;
            var poiToRecord = CurrentPlayingPoi;
            var duration = ListenSeconds;
            Debug.WriteLine($"[AudioService] Stopping -> Recording history for '{poiToRecord.Name}' ({duration}s)");
            await HistoryStore.AddAsync(poiToRecord, duration);
        }

        _listenStopwatch.Stop();
        _listenStopwatch.Reset();
        _uiTimer.Stop();

        _ttsToken?.Cancel();
        _ttsToken?.Dispose();
        _ttsToken = null;

        _ttsService.Stop();

        if (CurrentPlayingPoi != null)
            CurrentPlayingPoi.IsPlaying = false;

        IsPlaying = false;
        CurrentPlayingPoi = null;
        _historyRecorded = false;
        _listenThresholdReached = false;

        // Xóa thông báo khi stop hẳn
        SetNoScriptMessage(null);

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
            if (IsPlaying && !_ttsService.IsPaused)
            {
                _listenStopwatch.Stop();
                _uiTimer.Stop();
                IsPlaying = false;
                poi.IsPlaying = false;
                PlaybackStateChanged?.Invoke();
            }
        }
    }

    private void OnTtsProgress(int current, int total)
    {
        if (CurrentPlayingPoi == null || total == 0) return;

        var poi = CurrentPlayingPoi;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (poi == null) return;
            poi.AudioProgress = (double)current / total;

            int seconds = ListenSeconds;
            int mins = seconds / 60;
            int secs = seconds % 60;
            poi.AudioDuration = $"{mins:D2}:{secs:D2}";
        });
    }

    private async void OnTtsFinished()
    {
        if (CurrentPlayingPoi == null) return;

        var poi = CurrentPlayingPoi;
        _listenStopwatch.Stop();
        _uiTimer.Stop();

        Debug.WriteLine($"[AudioService] Finished. Total: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");

        if (!_historyRecorded)
        {
            _historyRecorded = true;
            var duration = ListenSeconds;
            Debug.WriteLine($"[AudioService] Finished -> Recording history for '{poi.Name}' ({duration}s)");
            await HistoryStore.AddAsync(poi, duration);
        }

        IsPlaying = false;
        poi.IsPlaying = false;
        PlaybackStateChanged?.Invoke();
    }

    private void CheckAndRecordHistory(POI poi)
    {
        if (_listenThresholdReached) return;
        if (_listenStopwatch.Elapsed.TotalSeconds < HistoryThresholdSeconds) return;

        _listenThresholdReached = true;
        Debug.WriteLine($"[AudioService] Reached {HistoryThresholdSeconds}s threshold for '{poi.Name}'.");
    }

    private async void StartPauseTimeout()
    {
        _pauseTimeoutCts?.Cancel();
        _pauseTimeoutCts = new CancellationTokenSource();
        var token = _pauseTimeoutCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(PauseTimeoutSeconds), token);

            if (CurrentPlayingPoi != null)
            {
                Debug.WriteLine($"[AudioService] Pause timeout for '{CurrentPlayingPoi.Name}'. Finalizing.");
                await StopAsync();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[AudioService] Pause timeout cancelled.");
        }
    }
}