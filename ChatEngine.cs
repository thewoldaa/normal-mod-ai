using AlyaOfflineChat.Models;
using AlyaOfflineChat.Services;
using AlyaOfflineChat.Storage;

namespace AlyaOfflineChat;

public sealed class ChatEngine
{
    private readonly ChatStore _chatStore;
    private readonly ModelConfigStore _modelConfig;
    private readonly ILlmService _llm;
    private readonly string _systemPrompt;

    public ChatEngine(string dataDir, string systemPrompt)
    {
        Directory.CreateDirectory(dataDir);
        _chatStore = new ChatStore(Path.Combine(dataDir, "chat_history.json"));
        _modelConfig = new ModelConfigStore(Path.Combine(dataDir, "model_config.json"));
        _llm = new MockLlmService(_modelConfig);
        _systemPrompt = systemPrompt;
    }

    public string Reply(string userMessage)
    {
        var userMsg = new ChatMessage
        {
            Sender = "user",
            Content = userMessage
        };
        _chatStore.Append(userMsg);

        var reply = _llm.Generate(_systemPrompt, _chatStore.GetRecent(20), userMessage);
        var aiMsg = new ChatMessage
        {
            Sender = "ai",
            Content = reply
        };
        _chatStore.Append(aiMsg);

        return reply;
    }

    public void ClearHistory()
    {
        _chatStore.Clear();
    }

    public string GetModelPath()
    {
        return _modelConfig.GetModelPath();
    }

    public void SetModelPath(string path)
    {
        _modelConfig.SetModelPath(path);
    }

    public void ClearModelConfig()
    {
        _modelConfig.Clear();
    }
}
