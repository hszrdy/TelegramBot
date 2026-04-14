using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class N8nService
{
    private readonly HttpClient _http;
    private readonly string _checkoutWebhookUrl;

    public N8nService(HttpClient http, string checkoutWebhookUrl)
    {
        _http = http;
        _checkoutWebhookUrl = checkoutWebhookUrl;
    }

    public async Task<CheckoutResult> CheckoutAsync(long chatId, Session session, string orderId)
    {
        var total = 0m;
        foreach (var item in session.Cart)
            total += item.Price * item.Qty;

        var payload = new
        {
            order_id = orderId,
            customer_name = session.CustomerName,
            customer_phone = session.CustomerPhone,
            total = total,
            chat_id = chatId
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync(_checkoutWebhookUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[N8n Response] Status: {response.StatusCode}");
            Console.WriteLine($"[N8n Response] Body: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                return new CheckoutResult
                {
                    Success = false,
                    Error = $"N8n payment workflow failed with status {response.StatusCode}. Body: {responseBody}"
                };
            }

            CheckoutResult? result = null;

            if (responseBody.TrimStart().StartsWith("["))
            {
                var list = JsonSerializer.Deserialize<List<CheckoutResult>>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                result = list?.FirstOrDefault();
            }
            else
            {
                result = JsonSerializer.Deserialize<CheckoutResult>(responseBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (result == null)
            {
                return new CheckoutResult
                {
                    Success = false,
                    Error = $"Empty or unparseable response from N8n: {responseBody}"
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            return new CheckoutResult
            {
                Success = false,
                Error = $"Exception calling N8n: {ex.Message}"
            };
        }
    }
}

public class CheckoutResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("invoice_url")]
    public string? InvoiceUrl { get; set; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}