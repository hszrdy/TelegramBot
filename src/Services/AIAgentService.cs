using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class AIAgentService
{
    private readonly HttpClient _http;
    private readonly string _aiAgentWebhookUrl;

    public AIAgentService(HttpClient http, string agentWebhookUrl)
    {
        _http = http;
        _aiAgentWebhookUrl = agentWebhookUrl;
    }

    public async Task<AIAgentResponseFormat> SendMessageToAgentAsync(Session session, long chat_id)
    {

    }
}

public class AIAgentResponseFormat
{

}