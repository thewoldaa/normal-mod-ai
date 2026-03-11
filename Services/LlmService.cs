using System.Text.RegularExpressions;
using AlyaOfflineChat.Models;
using AlyaOfflineChat.Storage;

namespace AlyaOfflineChat.Services;

public interface ILlmService
{
    string Generate(string systemPrompt, IReadOnlyList<ChatMessage> history, string userMessage);
}

public sealed class MockLlmService : ILlmService
{
    private readonly ModelConfigStore _modelConfig;

    public MockLlmService(ModelConfigStore modelConfig)
    {
        _modelConfig = modelConfig;
    }

    public string Generate(string systemPrompt, IReadOnlyList<ChatMessage> history, string userMessage)
    {
        var trimmed = (userMessage ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "Boleh diulang? Aku belum menangkap pesannya.";
        }

        if (Regex.IsMatch(trimmed, "\b(halo|hai|hello|hi)\b", RegexOptions.IgnoreCase))
        {
            return "Hai! Aku Alya. Senang bisa ngobrol sama kamu.";
        }

        if (Regex.IsMatch(trimmed, "\b(nama|siapa)\b", RegexOptions.IgnoreCase))
        {
            return "Aku Alya, AI companion offline yang siap menemani kamu.";
        }

        var modelPath = _modelConfig.GetModelPath();
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return "Aku siap membantu, tapi model Qwen3.5-2B belum dipasang. Isi path model di Pengaturan agar Alya bisa menjawab lebih lengkap.";
        }

        if (!File.Exists(modelPath))
        {
            return $"Model belum ditemukan di path: {modelPath}. Cek ulang path atau pindahkan file model ke lokasi itu.";
        }

        return "Model terdeteksi, tapi engine inferensi lokal belum terhubung. Aku tetap menyimpan percakapan dan menyiapkan sesi.";
    }
}
