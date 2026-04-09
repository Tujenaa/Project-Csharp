using Android.Runtime;
using Android.Speech.Tts;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;
using AndroidTts = Android.Speech.Tts.TextToSpeech;

namespace TourGuideApp.Services;

public partial class TextToSpeechService
{
    private AndroidTts _tts;
    private TaskCompletionSource<bool> _initTcs;
    private TaskCompletionSource<bool> _speechTcs;
    private int _startCharOffset = 0; // Offset ban đầu khi bắt đầu Speak

    private async Task InitTtsAsync()
    {
        if (_tts != null) return;

        _initTcs = new TaskCompletionSource<bool>();
        _tts = new AndroidTts(Platform.AppContext, new TtsOnInitListener(_initTcs));
        await _initTcs.Task;

        _tts.SetOnUtteranceProgressListener(new TtsProgressListener(this));
    }

    private partial async Task SpeakNativeAndroidAsync(string lang, CancellationToken cancelToken)
    {
        try 
        {
            await InitTtsAsync();

            var javaLocale = new Java.Util.Locale(lang);
            _tts.SetLanguage(javaLocale);

            _speechTcs = new TaskCompletionSource<bool>();
            
            // Lưu lại offset hiện tại để tính toán trong OnRangeStart
            _startCharOffset = _charIndex;
            string textToSpeak = _currentText.Substring(_charIndex);
            
            if (string.IsNullOrWhiteSpace(textToSpeak))
            {
                _finished = true;
                return;
            }

            var bundle = new Android.OS.Bundle();
            bundle.PutString(AndroidTts.Engine.KeyParamUtteranceId, "tourguide_tts");

            _tts.Speak(textToSpeak, QueueMode.Flush, bundle, "tourguide_tts");

            using (cancelToken.Register(() => _tts.Stop()))
            {
                await _speechTcs.Task;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS Android] Error: {ex.Message}");
        }
    }

    private partial void StopNativeAndroid()
    {
        _tts?.Stop();
        _speechTcs?.TrySetResult(false);
    }

    private class TtsOnInitListener : Java.Lang.Object, AndroidTts.IOnInitListener
    {
        private TaskCompletionSource<bool> _tcs;
        public TtsOnInitListener(TaskCompletionSource<bool> tcs) => _tcs = tcs;
        public void OnInit([GeneratedEnum] OperationResult status) => _tcs.TrySetResult(status == OperationResult.Success);
    }

    private class TtsProgressListener : UtteranceProgressListener
    {
        private readonly TextToSpeechService _parent;
        public TtsProgressListener(TextToSpeechService parent) => _parent = parent;

        public override void OnStart(string utteranceId) { }
        
        public override void OnDone(string utteranceId)
        {
            if (!_parent._isPaused)
            {
                _parent._finished = true;
                _parent._charIndex = 0;
            }
            _parent._speechTcs?.TrySetResult(true);
        }

        public override void OnError(string utteranceId) => _parent._speechTcs?.TrySetResult(false);

        // API 26+ hỗ trợ lấy vị trí đang đọc
        public override void OnRangeStart(string utteranceId, int start, int end, int frame)
        {
            // Cập nhật vị trí ký tự tuyệt đối trong _currentText
            _parent._charIndex = _parent._startCharOffset + start;
            
            // Gửi progress (charIndex / totalLength)
            _parent.OnProgress?.Invoke(_parent._charIndex, _parent._currentText.Length);
        }
    }
}
