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

    public Task<AIAgentResponseFormat> SendMessageToAgentAsync(Session session, long chatId)
    {
        return SendMessageToAgentAsync(session, chatId, string.Empty);
    }

    public async Task<AIAgentResponseFormat> SendMessageToAgentAsync(
        Session session,
        long chatId,
        string userMessage)
    {
        var total = session.Cart.Sum(i => i.Price * i.Qty);

        var payload = new
        {
            chat_id = chatId,
            message = userMessage,
            state = session.State,
            customer_name = session.CustomerName,
            customer_phone = session.CustomerPhone,
            order_id = session.OrderId,
            cart = session.Cart.Select(i => new
            {
                name = i.Name,
                qty = i.Qty,
                price = i.Price,
                line_total = i.Price * i.Qty,
                instructions = i.Instructions
            }),
            total,
            source = "telegram-bot"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync(_aiAgentWebhookUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new AIAgentResponseFormat
                {
                    Success = false,
                    Error = $"AI workflow failed with status {(int)response.StatusCode}: {responseBody}"
                };
            }

            var result = JsonSerializer.Deserialize<AIAgentResponseFormat>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (result == null)
            {
                return new AIAgentResponseFormat
                {
                    Success = false,
                    Error = "AI workflow returned an empty response"
                };
            }

            if (string.IsNullOrWhiteSpace(result.ReplyText) && string.IsNullOrWhiteSpace(result.Error))
            {
                result.Error = "AI workflow response is missing reply_text";
            }

            result.Success = string.IsNullOrWhiteSpace(result.Error);
            return result;
        }
        catch (Exception ex)
        {
            return new AIAgentResponseFormat
            {
                Success = false,
                Error = $"Exception calling AI workflow: {ex.Message}"
            };
        }
    }
}

public class AIAgentResponseFormat
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("reply_text")]
    public string? ReplyText { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("actions")]
    public JsonElement? Actions { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}