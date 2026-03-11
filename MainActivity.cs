using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech.Tts;
using Android.Provider;
using Android.Webkit;
using Android.Widget;
using Java.Interop;
using Uri = Android.Net.Uri;
using AlyaOfflineChat.Services;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Android.Views;

namespace AlyaOfflineChat;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true, HardwareAccelerated = false)]
public class MainActivity : Activity, TextToSpeech.IOnInitListener
{
    private const int RequestExportCode = 9001;
    private const int RequestImportCode = 9002;
    private const int RequestFileChooserCode = 9003;
    private const int RequestModelPickCode = 9004;

    private WebView? _webView;
    private ChatEngine? _chatEngine;
    private TextToSpeech? _tts;
    private bool _ttsReady;
    private string? _pendingExportBase64;
    private IValueCallback? _fileUploadCallback;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            SetContentView(Resource.Layout.activity_main);

            var root = FindViewById<FrameLayout>(Resource.Id.root);
            if (root == null)
            {
                throw new InvalidOperationException("Root container tidak ditemukan.");
            }

            _webView = TryCreateWebView(root);
            if (_webView == null)
            {
                return;
            }

            var dataDir = FilesDir?.AbsolutePath ?? CacheDir?.AbsolutePath ?? "/data/data/com.companyname.AlyaOfflineChat";
            var systemPrompt = PersonalityLoader.LoadFromAssets(this);
            _chatEngine = new ChatEngine(dataDir, systemPrompt);
            EnsureModelSelection();

            _webView.AddJavascriptInterface(new AlyaJsBridge(this, _chatEngine), "AlyaBridge");
            _webView.LoadUrl("file:///android_asset/index.html");

            _tts = new TextToSpeech(this, this);
        }
        catch (Exception ex)
        {
            ReportFatalError(ex);
        }
    }

    private WebView? TryCreateWebView(FrameLayout root)
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var pkg = WebView.GetCurrentWebViewPackage(this);
                if (pkg == null)
                {
                    ShowFatalText("Android System WebView tidak ditemukan. Aktifkan atau perbarui WebView/Chrome.");
                    return null;
                }
            }

            var webView = new WebView(this);
            webView.LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent);
            root.AddView(webView);

            var settings = webView.Settings;
            settings.JavaScriptEnabled = true;
            settings.DomStorageEnabled = true;
            settings.AllowFileAccess = true;
            settings.AllowContentAccess = true;
            settings.MediaPlaybackRequiresUserGesture = false;

            webView.SetWebViewClient(new LocalOnlyWebViewClient());
            webView.SetWebChromeClient(new AlyaWebChromeClient(this));

            return webView;
        }
        catch (Exception ex)
        {
            ReportFatalError(ex);
            return null;
        }
    }

    private void EnsureModelSelection()
    {
        if (_chatEngine == null)
        {
            return;
        }

        var existing = _chatEngine.GetModelPath();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        Toast.MakeText(this, "Model tidak termasuk APK. Buka Pengaturan lalu pilih file Qwen_Qwen3.5-2B-Q4_K_M.gguf.", ToastLength.Long)?.Show();
    }

    public void OnInit([GeneratedEnum] OperationResult status)
    {
        _ttsReady = status == OperationResult.Success;
        if (_ttsReady && _tts != null)
        {
            _tts.SetLanguage(Java.Util.Locale.ForLanguageTag("id-ID"));
        }
    }

    public void Speak(string text)
    {
        if (!_ttsReady || _tts == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        _tts.Speak(text, QueueMode.Flush, null, "alya-tts");
    }

    internal void RequestExport(string base64Payload)
    {
        _pendingExportBase64 = base64Payload;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/json");
        intent.PutExtra(Intent.ExtraTitle, $"alya-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        StartActivityForResult(intent, RequestExportCode);
    }

    internal void RequestImport()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/json");
        StartActivityForResult(intent, RequestImportCode);
    }

    internal void RequestModelPickFromUi()
    {
        RunOnUiThread(RequestModelPick);
    }

    private void RequestModelPick()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "application/octet-stream", "application/x-gguf", "application/x-llama-gguf" });
        StartActivityForResult(intent, RequestModelPickCode);
    }

    internal bool HandleFileChooser(IValueCallback filePathCallback, WebChromeClient.FileChooserParams fileChooserParams)
    {
        _fileUploadCallback?.OnReceiveValue(null);
        _fileUploadCallback = filePathCallback;

        Intent intent;
        try
        {
            intent = fileChooserParams.CreateIntent();
        }
        catch (ActivityNotFoundException)
        {
            _fileUploadCallback = null;
            return false;
        }

        StartActivityForResult(intent, RequestFileChooserCode);
        return true;
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RequestFileChooserCode)
        {
            HandleFileChooserResult(resultCode, data);
            return;
        }

        if (requestCode == RequestExportCode)
        {
            HandleExportResult(resultCode, data);
            return;
        }

        if (requestCode == RequestImportCode)
        {
            HandleImportResult(resultCode, data);
            return;
        }

        if (requestCode == RequestModelPickCode)
        {
            HandleModelPickResult(resultCode, data);
            return;
        }
    }

    private void HandleFileChooserResult(Result resultCode, Intent? data)
    {
        if (_fileUploadCallback == null)
        {
            return;
        }

        Uri[]? results = null;
        if (resultCode == Result.Ok && data != null)
        {
            if (data.Data != null)
            {
                results = new[] { data.Data };
            }
            else if (data.ClipData != null)
            {
                var clip = data.ClipData;
                var uris = new List<Uri>();
                for (var i = 0; i < clip.ItemCount; i++)
                {
                    var item = clip.GetItemAt(i);
                    if (item.Uri != null)
                    {
                        uris.Add(item.Uri);
                    }
                }
                results = uris.ToArray();
            }
        }

        _fileUploadCallback.OnReceiveValue(results);
        _fileUploadCallback = null;
    }

    private void HandleExportResult(Result resultCode, Intent? data)
    {
        if (resultCode != Result.Ok || data?.Data == null || string.IsNullOrWhiteSpace(_pendingExportBase64))
        {
            _pendingExportBase64 = null;
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(_pendingExportBase64);
            using var stream = ContentResolver.OpenOutputStream(data.Data);
            if (stream != null)
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _pendingExportBase64 = null;
        }
    }

    private void HandleImportResult(Result resultCode, Intent? data)
    {
        if (resultCode != Result.Ok || data?.Data == null || _webView == null)
        {
            return;
        }

        try
        {
            using var stream = ContentResolver.OpenInputStream(data.Data);
            if (stream == null) return;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

            RunOnUiThread(() =>
            {
                _webView.EvaluateJavascript($"window.__applyImportBase64('{base64}')", null);
            });
        }
        catch
        {
            // ignore
        }
    }

    private void HandleModelPickResult(Result resultCode, Intent? data)
    {
        if (resultCode != Result.Ok || data?.Data == null || _chatEngine == null)
        {
            return;
        }

        var pickedUri = data.Data;
        Toast.MakeText(this, "Menyalin model ke penyimpanan aplikasi...", ToastLength.Short)?.Show();

        Task.Run(() =>
        {
            var path = CopyModelFromUri(pickedUri);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _chatEngine.SetModelPath(path);
                UpdateModelPathOnWeb(path);
            }
        });
    }

    private string? CopyModelFromUri(Uri uri)
    {
        try
        {
            var fileName = GetFileNameFromUri(uri);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "Qwen_Qwen3.5-2B-Q4_K_M.gguf";
            }

            var baseDir = FilesDir?.AbsolutePath ?? CacheDir?.AbsolutePath ?? "/data/data/com.companyname.AlyaOfflineChat";
            var destDir = Path.Combine(baseDir, "models");
            var destPath = Path.Combine(destDir, fileName);

            Directory.CreateDirectory(destDir);

            using var input = ContentResolver.OpenInputStream(uri);
            if (input == null)
            {
                return null;
            }
            using var output = File.Create(destPath);
            input.CopyTo(output);

            return destPath;
        }
        catch
        {
            return null;
        }
    }

    private string GetFileNameFromUri(Uri uri)
    {
        if (uri.Scheme?.Equals("content", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var cursor = ContentResolver.Query(uri, null, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                var index = cursor.GetColumnIndex(OpenableColumns.DisplayName);
                if (index >= 0)
                {
                    return cursor.GetString(index) ?? string.Empty;
                }
            }
        }

        return Path.GetFileName(uri.Path) ?? string.Empty;
    }

    private void UpdateModelPathOnWeb(string path)
    {
        if (_webView == null)
        {
            return;
        }

        var jsValue = JsonSerializer.Serialize(path);
        RunOnUiThread(() =>
        {
            _webView.EvaluateJavascript($"(function(){{localStorage.setItem('alya.modelPath', {jsValue}); var el=document.getElementById('model-path'); if (el) el.value={jsValue};}})()", null);
        });
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _tts?.Stop();
        _tts?.Shutdown();
    }

    private void ReportFatalError(Exception ex)
    {
        try
        {
            var baseDir = FilesDir?.AbsolutePath ?? CacheDir?.AbsolutePath ?? "/data/data/com.companyname.AlyaOfflineChat";
            var logPath = Path.Combine(baseDir, "alya_crash.log");
            File.AppendAllText(logPath, $"{DateTime.UtcNow:O} {ex}\n");
        }
        catch
        {
            // ignore
        }

        var message = "Alya gagal dibuka. Coba pastikan Android System WebView aktif dan perbarui bila perlu.\n\nDetail:\n" + ex.Message;
        ShowFatalText(message);
    }

    private void ShowFatalText(string message)
    {
        var textView = new TextView(this)
        {
            Text = message
        };
        textView.SetPadding(24, 24, 24, 24);
        SetContentView(textView);
    }
}

public sealed class AlyaJsBridge : Java.Lang.Object
{
    private readonly MainActivity _activity;
    private readonly ChatEngine _engine;

    public AlyaJsBridge(MainActivity activity, ChatEngine engine)
    {
        _activity = activity;
        _engine = engine;
    }

    [JavascriptInterface]
    [Export("sendMessage")]
    public string SendMessage(string text)
    {
        return _engine.Reply(text);
    }

    [JavascriptInterface]
    [Export("exportData")]
    public void ExportData(string base64Payload)
    {
        _activity.RequestExport(base64Payload);
    }

    [JavascriptInterface]
    [Export("requestImport")]
    public void RequestImport()
    {
        _activity.RequestImport();
    }

    [JavascriptInterface]
    [Export("getModelPath")]
    public string GetModelPath()
    {
        return _engine.GetModelPath();
    }

    [JavascriptInterface]
    [Export("setModelPath")]
    public void SetModelPath(string path)
    {
        _engine.SetModelPath(path);
    }

    [JavascriptInterface]
    [Export("requestModelPick")]
    public void RequestModelPick()
    {
        _activity.RequestModelPickFromUi();
    }

    [JavascriptInterface]
    [Export("clearNativeData")]
    public void ClearNativeData()
    {
        _engine.ClearHistory();
        _engine.ClearModelConfig();
    }

    [JavascriptInterface]
    [Export("clearChatHistory")]
    public void ClearChatHistory()
    {
        _engine.ClearHistory();
    }

    [JavascriptInterface]
    [Export("speak")]
    public void Speak(string text)
    {
        _activity.Speak(text);
    }
}

public sealed class AlyaWebChromeClient : WebChromeClient
{
    private readonly MainActivity _activity;

    public AlyaWebChromeClient(MainActivity activity)
    {
        _activity = activity;
    }

    public override bool OnShowFileChooser(WebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
    {
        if (filePathCallback == null || fileChooserParams == null)
        {
            return false;
        }
        return _activity.HandleFileChooser(filePathCallback, fileChooserParams);
    }
}

public sealed class LocalOnlyWebViewClient : WebViewClient
{
    public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
    {
        var url = request?.Url?.ToString() ?? string.Empty;
        if (url.StartsWith("file:///android_asset/", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }
}
