using TourGuideApp.Models;
using TourGuideApp.Utils;
using TourGuideApp.ViewModels;
using System.Diagnostics;

namespace TourGuideApp.Services;

/// <summary>
/// Singleton service quản lý việc phát âm thanh toàn ứng dụng.
/// Đảm bảo chỉ có 1 POI được phát tại một thời điểm và đồng bộ trạng thái lên UI.
///
/// Luật ghi lịch sử:
///   - Ghi tại mốc 10s tích lũy (CheckAndRecordHistory, gọi từ _uiTimer mỗi giây).
///   - Khi audio kết thúc tự nhiên và chưa ghi -> ghi ngay (dù < 10s).
///   - Khi Stop() do chuyển POI / timeout: chỉ ghi nếu chưa ghi VÀ đã nghe >= 10s.
///     (Nếu < 10s và chưa kết thúc tự nhiên thì không tính lượt nghe.)
///
/// Luồng Pause / Timeout:
///   - Pause -> StartPauseTimeout (120s).
///   - Resume trước 120s -> hủy timeout, tiếp tục cộng dồn thời gian.
///   - Hết 120s -> Stop() -> reset hoàn toàn (nghe lại sẽ phát từ đầu).
///
/// Luồng chuyển POI:
///   - PlayAsync(poiMới) -> Stop() cho POI cũ ngay lập tức -> phát POI mới từ đầu.
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
    /// Giữ nguyên Elapsed khi pause để cộng dồn qua nhiều lần resume.
    /// </summary>
    private readonly Stopwatch _listenStopwatch = new();

    /// <summary>Ngưỡng thời gian nghe tích lũy để tính 1 lượt nghe (mặc định 10s).</summary>
    private const double HistoryThresholdSeconds = 10.0;

    /// <summary>Thời gian chờ tối đa khi tạm dừng trước khi kết thúc phiên (120s).</summary>
    private const int PauseTimeoutSeconds = 120;

    /// <summary>
    /// Flag tránh ghi lịch sử trùng cho cùng 1 POI trong 1 phiên nghe.
    /// Reset về false chỉ khi bắt đầu phiên hoàn toàn mới (Play mới / sau Stop).
    /// KHÔNG reset trong Stop() để tránh race condition với async ghi lịch sử.
    /// </summary>
    private bool _historyRecorded = false;

    /// <summary>Timer cập nhật ListenSeconds lên UI mỗi giây khi đang phát.</summary>
    private readonly System.Timers.Timer _uiTimer;

    /// <summary>Hỗ trợ hủy bộ đếm timeout khi người dùng resume hoặc chuyển POI.</summary>
    private CancellationTokenSource? _pauseTimeoutCts;

    // ── Public state ──────────────────────────────────────────────────────────

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
            // Kiểm tra ngưỡng 10s và ghi lịch sử nếu đủ điều kiện
            var poi = CurrentPlayingPoi;
            if (poi != null) CheckAndRecordHistory(poi);

            // Cập nhật UI
            MainThread.BeginInvokeOnMainThread(() =>
                ListenSecondsChanged?.Invoke(ListenSeconds));
        };
    }

    // ── PlayAsync ─────────────────────────────────────────────────────────────

    public async Task PlayAsync(POI poi)
    {
        if (poi == null) return;

        // 1. Đang phát chính POI này rồi -> không làm gì
        if (CurrentPlayingPoi?.Id == poi.Id && IsPlaying) return;

        string currentLang = SettingService.Instance.Language;

        // ── RESUME: Cùng POI, cùng ngôn ngữ, đang pause ──────────────────────
        // CurrentPlayingPoi vẫn còn (chưa bị Stop do timeout) -> chỉ cần resume
        if (CurrentPlayingPoi?.Id == poi.Id && !IsPlaying && currentLang == _currentLoadedLang)
        {
            // Hủy timeout đang đếm ngược (nếu có) và giải phóng tài nguyên
            CancelAndDisposePauseTimeout();

            // Tiếp tục đếm từ Elapsed hiện tại (không Reset)
            _listenStopwatch.Start();
            _uiTimer.Start();

            Debug.WriteLine($"[AudioService] Resuming '{poi.Name}'. Accumulated: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");
            await StartSpeakingAsync(poi);
            return;
        }

        // ── PLAY MỚI / ĐỔI NGÔN NGỮ KHI ĐANG PAUSE / CHUYỂN POI ────────────
        // Các trường hợp rơi vào đây:
        //   a) POI khác                        -> phát POI mới từ đầu
        //   b) Cùng POI, đang pause, đổi lang  -> phát lại từ đầu bằng ngôn ngữ mới
        //   c) Cùng POI, cùng lang, sau timeout -> CurrentPlayingPoi đã null, coi như mới
        //
        // Stop() xử lý: hủy timeout, chốt lịch sử POI cũ (nếu đủ 10s), reset hoàn toàn.
        // Stop() đã Reset _listenStopwatch bên trong nên không cần Reset lại ở đây.
        Stop();

        CurrentPlayingPoi = poi;

        // _historyRecorded reset ở đây (không trong Stop()) để tránh race condition
        // với async HistoryStore.AddAsync đang chạy dở từ phiên cũ.
        _historyRecorded = false;

        // --- Đồng bộ dữ liệu mới nhất từ API (nếu online) ---
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
            // Cùng ngôn ngữ nhưng là POI mới -> buộc phát từ đầu
            _ttsService.ForceLoadText(finalText);
        }

        _listenStopwatch.Start();
        _uiTimer.Start();
        await StartSpeakingAsync(poi);
    }

    // ── Pause ─────────────────────────────────────────────────────────────────

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

        Debug.WriteLine($"[AudioService] Paused '{CurrentPlayingPoi.Name}'. Accumulated: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");

        PlaybackStateChanged?.Invoke();

        // Bắt đầu đếm ngược 120s; nếu hết thời gian mà không Resume -> Stop()
        StartPauseTimeout();
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dừng hoàn toàn phiên nghe hiện tại và reset về trạng thái ban đầu.
    ///
    /// Chính sách ghi lịch sử tại Stop():
    ///   - Chỉ ghi nếu chưa ghi (_historyRecorded == false) VÀ đã nghe >= 10s.
    ///   - Nếu < 10s: không tính lượt nghe (người dùng chưa thực sự nghe đủ).
    ///   - Nếu đã ghi tại mốc 10s rồi: không ghi lại (tránh trùng).
    ///
    /// Gọi bởi: PlayAsync khi chuyển POI, StartPauseTimeout khi hết 120s, hoặc từ ngoài.
    /// </summary>
    public void Stop()
    {
        // 1. Hủy timeout đang chờ (nếu có)
        CancelAndDisposePauseTimeout();

        // 2. Dừng đếm thời gian ngay để lấy giá trị chính xác
        _listenStopwatch.Stop();
        _uiTimer.Stop();

        // 3. Chốt lịch sử nếu: chưa ghi VÀ đã nghe đủ ngưỡng 10s
        //    (Trường hợp: đang phát bị chuyển POI hoặc timeout sau mốc 10s
        //     nhưng timer chưa kịp fire CheckAndRecordHistory lần cuối)
        if (CurrentPlayingPoi != null && !_historyRecorded
            && _listenStopwatch.Elapsed.TotalSeconds >= HistoryThresholdSeconds)
        {
            _historyRecorded = true;
            var poiToRecord = CurrentPlayingPoi;
            var duration = ListenSeconds;
            Debug.WriteLine($"[AudioService] Stop -> Finalizing history for '{poiToRecord.Name}' ({duration}s)");
            _ = HistoryStore.AddAsync(poiToRecord, duration);
        }
        else if (CurrentPlayingPoi != null && !_historyRecorded)
        {
            Debug.WriteLine($"[AudioService] Stop -> '{CurrentPlayingPoi.Name}' listened only {_listenStopwatch.Elapsed.TotalSeconds:F1}s (< {HistoryThresholdSeconds}s). Not recorded.");
        }

        // 4. Reset bộ đếm sau khi đã đọc giá trị
        _listenStopwatch.Reset();

        // 5. Hủy và giải phóng TTS token
        _ttsToken?.Cancel();
        _ttsToken?.Dispose();
        _ttsToken = null;

        _ttsService.Stop();

        // 6. Reset trạng thái UI của POI cũ
        if (CurrentPlayingPoi != null)
        {
            CurrentPlayingPoi.IsPlaying = false;
        }

        // 7. Reset toàn bộ state
        //    _historyRecorded KHÔNG reset ở đây để tránh race condition với async ghi.
        //    Sẽ được reset ở PlayAsync khi bắt đầu phiên mới.
        IsPlaying = false;
        CurrentPlayingPoi = null;

        PlaybackStateChanged?.Invoke();
    }

    // ── StartSpeakingAsync ────────────────────────────────────────────────────

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
        catch (OperationCanceledException)
        {
            // Bị hủy do Pause hoặc Stop -> không làm gì thêm, Pause/Stop đã xử lý state
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioService] TTS Error: {ex.Message}");
        }
        finally
        {
            // Nếu kết thúc tự nhiên (không phải do Pause/Stop cancel) -> dọn state
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

    // ── TTS Callbacks ─────────────────────────────────────────────────────────

    private void OnTtsProgress(int current, int total)
    {
        if (CurrentPlayingPoi == null || total == 0) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentPlayingPoi.AudioProgress = (double)current / total;

            // Hiển thị thời gian nghe tích lũy: mm:ss
            int seconds = ListenSeconds;
            int mins = seconds / 60;
            int secs = seconds % 60;
            CurrentPlayingPoi.AudioDuration = $"{mins:D2}:{secs:D2}";
        });
    }

    private async void OnTtsFinished()
    {
        if (CurrentPlayingPoi == null) return;

        var poi = CurrentPlayingPoi;
        _listenStopwatch.Stop();
        _uiTimer.Stop();

        Debug.WriteLine($"[AudioService] Finished '{poi.Name}'. Total: {_listenStopwatch.Elapsed.TotalSeconds:F1}s");

        // Ghi lịch sử nếu chưa ghi.
        // Không kiểm tra ngưỡng 10s ở đây vì audio đã phát hết -> luôn tính lượt nghe
        // (Xử lý cả trường hợp script ngắn < 10s mà người dùng nghe trọn vẹn)
        if (!_historyRecorded)
        {
            _historyRecorded = true;
            Debug.WriteLine($"[AudioService] Finished -> Recording history for '{poi.Name}' ({ListenSeconds}s)");
            await HistoryStore.AddAsync(poi, ListenSeconds);
        }

        IsPlaying = false;
        poi.IsPlaying = false;
        PlaybackStateChanged?.Invoke();
    }

    // ── Ghi lịch sử tại mốc 10s ──────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra thời gian nghe tích lũy. Nếu đủ ngưỡng 10s và chưa ghi thì ghi lịch sử.
    /// Gọi từ _uiTimer (mỗi giây) trong khi đang phát.
    /// </summary>
    private void CheckAndRecordHistory(POI poi)
    {
        if (_historyRecorded) return;
        if (_listenStopwatch.Elapsed.TotalSeconds < HistoryThresholdSeconds) return;

        _historyRecorded = true;
        Debug.WriteLine($"[AudioService] Reached {HistoryThresholdSeconds}s -> Recording history for '{poi.Name}' ({ListenSeconds}s)");

        // Fire-and-forget, không block luồng TTS
        _ = HistoryStore.AddAsync(poi, ListenSeconds);
    }

    // ── Pause Timeout ─────────────────────────────────────────────────────────

    /// <summary>
    /// Bắt đầu đếm ngược 120s sau khi Pause.
    /// Nếu hết thời gian mà chưa Resume -> gọi Stop() để kết thúc phiên và reset hoàn toàn.
    /// Sau Stop(), nếu người dùng bấm Play lại sẽ phát từ đầu.
    /// </summary>
    private async void StartPauseTimeout()
    {
        // Hủy timeout cũ (phòng trường hợp gọi nhiều lần liên tiếp)
        CancelAndDisposePauseTimeout();

        _pauseTimeoutCts = new CancellationTokenSource();
        var token = _pauseTimeoutCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(PauseTimeoutSeconds), token);

            // Đến đây = timeout thực sự (không bị Resume/Play cancel)
            if (CurrentPlayingPoi != null)
            {
                Debug.WriteLine($"[AudioService] Pause timeout ({PauseTimeoutSeconds}s) for '{CurrentPlayingPoi.Name}'. Ending session.");
                // Stop() sẽ chốt lịch sử (nếu đủ điều kiện) và reset toàn bộ state.
                // Lần Play tiếp theo sẽ phát từ đầu vì CurrentPlayingPoi = null sau Stop().
                Stop();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[AudioService] Pause timeout cancelled (resumed or switched POI).");
        }
    }

    /// <summary>
    /// Hủy và giải phóng CancellationTokenSource của pause timeout.
    /// Gọi trước khi tạo timeout mới hoặc khi resume/stop.
    /// </summary>
    private void CancelAndDisposePauseTimeout()
    {
        _pauseTimeoutCts?.Cancel();
        _pauseTimeoutCts?.Dispose();
        _pauseTimeoutCts = null;
    }
}