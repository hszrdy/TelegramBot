using System.Text;
using System.Text.Json;
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

    // Call n8n checkout sub-workflow
    public async Task<CheckoutResult> CheckoutAsync(long chatId, Session session)
    {
        var payload = new
        {
            chat_id = chatId,
            customer_name = session.CustomerName,
            customer_phone = session.CustomerPhone,
            cart = session.Cart.Select(i => new
            {
                name = i.Name,
                qty = i.Qty,
                price = i.Price,
                instructions = i.Instructions
            }),
            total = session.Cart.Sum(i => i.Price * i.Qty)
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(_checkoutWebhookUrl, content);

        if (!response.IsSuccessStatusCode)
            return new CheckoutResult { Success = false, Error = "n8n webhook failed" };

        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CheckoutResult>(responseBody);
        return result ?? new CheckoutResult { Success = false, Error = "Empty response" };
    }
}

public class CheckoutResult
{
    public bool Success { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? OrderId { get; set; }
    public string? Error { get; set; }
}
