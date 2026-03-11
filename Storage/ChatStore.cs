using System.Text.Json;
using AlyaOfflineChat.Models;

namespace AlyaOfflineChat.Storage;

public sealed class ChatStore
{
    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly List<ChatMessage> _messages;

    public ChatStore(string filePath)
    {
        _filePath = filePath;
        _messages = Load();
    }

    public IReadOnlyList<ChatMessage> GetRecent(int count)
    {
        lock (_sync)
        {
            if (_messages.Count <= count)
            {
                return _messages.ToArray();
            }
            return _messages.Skip(Math.Max(0, _messages.Count - count)).ToArray();
        }
    }

    public void Append(ChatMessage message)
    {
        lock (_sync)
        {
            _messages.Add(message);
            Save();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _messages.Clear();
            Save();
        }
    }

    private List<ChatMessage> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<ChatMessage>();
            }
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<List<ChatMessage>>(json);
            return data ?? new List<ChatMessage>();
        }
        catch
        {
            return new List<ChatMessage>();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_messages, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_filePath, json);
    }
}
