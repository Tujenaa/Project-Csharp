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

    // ── Theo dõi thời gian nghe tích lũy ─────────────────────────────────────
    /// <summary>
    /// Đo thời gian nghe thực tế. Start khi play/resume, Stop khi pause/stop.
    /// Giữ nguyên giá trị Elapsed khi pause để cộng dồn qua nhiều lần resume.
    /// </summary>
    private readonly Stopwatch _listenStopwatch = new();

    /// <summary>Ngưỡng thời gian nghe tích lũy để tính 1 lượt nghe (mặc định 10s).</summary>
    private const double HistoryThresholdSeconds = 5.0;

    /// <summary>Thời gian chờ tối đa khi tạm dừng trước khi kết thúc phiên (120s).</summary>
    private const int PauseTimeoutSeconds = 60;

    /// <summary>
    /// Đánh dấu người dùng đã nghe đủ ngưỡng tối thiểu (HistoryThresholdSeconds).
    /// Lịch sử chỉ được ghi khi dừng hẳn hoặc nghe xong, KHÔNG ghi ngay tại đây.
    /// </summary>
    private bool _listenThresholdReached = false;

    /// <summary>
    /// Flag tránh ghi lịch sử trùng cho cùng 1 POI trong 1 phiên nghe.
    /// Reset về false khi chuyển sang POI khác hoặc Play lại từ đầu.
    /// </summary>
    private bool _historyRecorded = false;

    /// <summary>
    /// Timer cập nhật <see cref="ListenSeconds"/> lên UI mỗi giây khi đang phát.
    /// </summary>
    private readonly System.Timers.Timer _uiTimer;

    /// <summary>Hỗ trợ hủy bộ đếm timeout khi người dùng resume hoặc chuyển POI.</summary>
    private CancellationTokenSource? _pauseTimeoutCts;

    // ── Public state ──────────────────────────────────────────────────────────

    // ── Hàng đợi phát tuần tự ────────────────────────────────────────────────
    private readonly Queue<POI> _playQueue = new();
    private bool _isProcessingQueue = false;

    public POI? CurrentPlayingPoi { get; private set; }
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Số giây nghe tích lũy hiện tại (làm tròn xuống).
    /// Bind lên UI để hiển thị realtime, ví dụ: "Đã nghe: 8s / 10s".
    /// </summary>
    public int ListenSeconds => (int)_listenStopwatch.Elapsed.TotalSeconds;

    // Events
    public event Action? PlaybackStateChanged;

    /// <summary>Fired mỗi giây khi đang phát, kèm số giây hiện tại.</summary>
    public event Action<int>? ListenSecondsChanged;

    private AudioPlaybackService()
    {
        _ttsService.OnFinished += OnTtsFinished;
        _ttsService.OnProgress += OnTtsProgress;

        // Timer tick mỗi 1000ms, chỉ chạy khi đang phát
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

    /// <summary>Thêm nhiều POI vào hàng đợi và bắt đầu phát tuần tự nếu chưa phát.</summary>
    public async Task EnqueueRangeAsync(IEnumerable<POI> pois)
    {
        foreach (var poi in pois)
            _playQueue.Enqueue(poi);

        await ProcessQueueAsync();
    }

    /// <summary>Thêm 1 POI vào hàng đợi và bắt đầu phát nếu chưa phát.</summary>
    public async Task EnqueueAsync(POI poi)
    {
        _playQueue.Enqueue(poi);
        await ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessingQueue) return;
        _isProcessingQueue = true;

        try
        {
            while (_playQueue.Count > 0)
            {
                // Nếu đang phát thủ công (không phải từ queue), dừng xử lý queue
                if (IsPlaying) break;

                var next = _playQueue.Dequeue();
                await PlayAsync(next);

                // Chờ cho đến khi POI này phát xong trước khi chuyển sang POI tiếp theo
                while (IsPlaying)
                    await Task.Delay(500);
            }
        }
        finally
        {
            _isProcessingQueue = false;
        }
    }

    public async Task PlayAsync(POI poi)
    {
        if (poi == null) return;

        // Hủy bộ đếm kết thúc phiên cũ nếu người dùng nhấn Play POI bất kỳ
        _pauseTimeoutCts?.Cancel();

        // 1. Nếu đang phát chính POI này -> không làm gì
        if (CurrentPlayingPoi?.Id == poi.Id && IsPlaying) return;

        string currentLang = SettingService.Instance.Language;

        // ── RESUME: Cùng POI, cùng ngôn ngữ, đang pause ──────────────────────
        if (CurrentPlayingPoi?.Id == poi.Id && !IsPlaying && currentLang == _currentLoadedLang)
        {
            // Tiếp tục đếm thời gian từ lần trước (Stopwatch giữ nguyên Elapsed)
            _listenStopwatch.Start();
            _uiTimer.Start();
            await StartSpeakingAsync(poi);
            return;
        }

        // ── PLAY MỚI hoặc đổi ngôn ngữ ─────────────────────────────────────
        await StopAsync();

        CurrentPlayingPoi = poi;

        // Reset bộ đếm thời gian và flag lịch sử cho POI mới
        _listenStopwatch.Reset();
        _historyRecorded = false;
        _listenThresholdReached = false;

        // --- ĐỒNG BỘ DỮ LIỆU TỪ API (Nếu online) ---
        if (ConnectivityService.IsConnected)
        {
            var freshPoi = await _apiService.GetPOIById(poi.Id);
            if (freshPoi != null)
            {
                poi.Audios = freshPoi.Audios;
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

        _listenStopwatch.Start();
        _uiTimer.Start();
        await StartSpeakingAsync(poi);
    }

    public void Pause()
    {
        if (CurrentPlayingPoi == null || !IsPlaying) return;

        // Dừng đếm thời gian (giữ Elapsed để resume cộng tiếp)
        _listenStopwatch.Stop();
        _uiTimer.Stop();

        _ttsToken?.Cancel();
        _ttsService.Pause();

        IsPlaying = false;
        CurrentPlayingPoi.IsPlaying = false;

        Debug.WriteLine($"[AudioService] Paused. Accumulated listen time: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");

        PlaybackStateChanged?.Invoke();

        // Bắt đầu đếm ngược kết thúc phiên (120s)
        StartPauseTimeout();
    }

    public async Task StopAsync()
    {
        // 1. Hủy bộ đếm timeout nếu có
        _pauseTimeoutCts?.Cancel();
        _pauseTimeoutCts = null;

        // 2. Chốt lịch sử nếu chưa ghi và đã nghe > 0s (kể cả chưa đạt ngưỡng 5s)
        // Lấy thời gian thực tế tại thời điểm dừng hẳn (pause timeout hoặc chuyển POI)
        if (CurrentPlayingPoi != null && !_historyRecorded && ListenSeconds > 0)
        {
            _historyRecorded = true;
            var poiToRecord = CurrentPlayingPoi;
            var duration = ListenSeconds;
            Debug.WriteLine($"[AudioService] Stopping session -> Recording history for '{poiToRecord.Name}' ({duration}s)");
            await HistoryStore.AddAsync(poiToRecord, duration); // await đúng cách, không fire-and-forget
        }

        // Dừng và reset bộ đếm khi stop hẳn
        _listenStopwatch.Stop();
        _listenStopwatch.Reset();
        _uiTimer.Stop();

        _ttsToken?.Cancel();
        _ttsToken?.Dispose();
        _ttsToken = null;

        _ttsService.Stop();

        if (CurrentPlayingPoi != null)
        {
            CurrentPlayingPoi.IsPlaying = false;
        }

        IsPlaying = false;
        CurrentPlayingPoi = null;
        _historyRecorded = false;
        _listenThresholdReached = false;
        PlaybackStateChanged?.Invoke();
    }

    // Giữ lại Stop() synchronous cho các nơi gọi không async, nhưng delegate sang StopAsync
    public void Stop() => _ = StopAsync();

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
            // Nếu kết thúc tự nhiên (không phải Pause) -> dừng đếm và cập nhật UI
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

        // Capture trước khi vào lambda để tránh race condition
        // (CurrentPlayingPoi có thể bị StopAsync() set null trên thread khác)
        var poi = CurrentPlayingPoi;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (poi == null) return; // double-check sau khi lên main thread
            poi.AudioProgress = (double)current / total;

            // Định dạng thời gian nghe: mm:ss
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

        Debug.WriteLine($"[AudioService] Finished. Total listen time: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");

        // Ghi lịch sử nếu chưa ghi.
        // Nghe xong hoàn toàn -> luôn ghi với thời gian thực tế, kể cả script rất ngắn < ngưỡng
        if (!_historyRecorded)
        {
            _historyRecorded = true;
            var duration = ListenSeconds; // thời gian thực tế đã nghe hết
            Debug.WriteLine($"[AudioService] Finished -> Recording history for '{poi.Name}' ({duration}s)");
            await HistoryStore.AddAsync(poi, duration);
        }

        IsPlaying = false;
        poi.IsPlaying = false;
        PlaybackStateChanged?.Invoke();
    }

    // ── Ghi lịch sử khi đủ 10s ───────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra thời gian nghe tích lũy. Nếu đủ ngưỡng thì đánh dấu đủ điều kiện ghi lịch sử.
    /// Lịch sử thực sự chỉ được ghi khi dừng hẳn (Stop) hoặc nghe xong (OnTtsFinished),
    /// để đảm bảo lưu đúng thời gian thực tế chứ không phải thời điểm đạt ngưỡng.
    /// </summary>
    private void CheckAndRecordHistory(POI poi)
    {
        if (_listenThresholdReached) return;
        if (_listenStopwatch.Elapsed.TotalSeconds < HistoryThresholdSeconds) return;

        _listenThresholdReached = true;
        Debug.WriteLine($"[AudioService] Reached {HistoryThresholdSeconds}s threshold for '{poi.Name}'. Will record on stop/finish.");
    }

    /// <summary>
    /// Bắt đầu đếm ngược thời gian chờ khi tạm dừng.
    /// Nếu hết thời gian mà chưa Resume, phiên nghe sẽ kết thúc.
    /// </summary>
    private async void StartPauseTimeout()
    {
        _pauseTimeoutCts?.Cancel();
        _pauseTimeoutCts = new CancellationTokenSource();
        var token = _pauseTimeoutCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(PauseTimeoutSeconds), token);

            // Nếu đến được đây nghĩa là bộ đếm đã chạy hết (không bị cancel bởi Resume hoặc Play mới)
            if (CurrentPlayingPoi != null)
            {
                Debug.WriteLine($"[AudioService] Pause timeout ({PauseTimeoutSeconds}s) reached for '{CurrentPlayingPoi.Name}'. Finalizing session.");
                await StopAsync(); // await để đảm bảo HistoryStore.AddAsync chạy xong
            }
        }
        catch (OperationCanceledException)
        {
            // Bị hủy do người dùng Resume hoặc chọn POI khác
            Debug.WriteLine("[AudioService] Pause timeout cancelled.");
        }
    }
}