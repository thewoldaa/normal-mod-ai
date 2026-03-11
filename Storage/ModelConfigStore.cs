using System.Text.Json;

namespace AlyaOfflineChat.Storage;

public sealed class ModelConfigStore
{
    private readonly string _filePath;

    public ModelConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public string GetModelPath()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return string.Empty;
            }
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<ModelConfig>(json);
            return data?.ModelPath ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void SetModelPath(string path)
    {
        var data = new ModelConfig { ModelPath = path ?? string.Empty };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    private sealed class ModelConfig
    {
        public string ModelPath { get; set; } = string.Empty;
    }
}
