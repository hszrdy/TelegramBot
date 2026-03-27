using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramBot.Models;

namespace TelegramBot.Services;

/// <summary>
/// Handles N8n payment workflow integration.
/// Sends payment requests to N8n which:
/// 1. Creates Xendit invoice
/// 2. Saves order to Google Sheets
/// Xendit callback workflow handles payment notifications and order completion.
/// </summary>
public class N8nService
{
    private readonly HttpClient _http;
    private readonly string _checkoutWebhookUrl;

    public N8nService(HttpClient http, string checkoutWebhookUrl)
    {
        _http = http;
        _checkoutWebhookUrl = checkoutWebhookUrl;
    }

    /// <summary>
    /// Send payment request to N8n.
    /// N8n will create Xendit invoice and store order in Google Sheets.
    /// Returns invoice URL for user to complete payment.
    /// </summary>
    public async Task<CheckoutResult> CheckoutAsync(long chatId, Session session)
    {
        // Generate order ID
        var orderId = Guid.NewGuid().ToString().Substring(0, 12).ToUpper();

        // Calculate total
        var total = 0m;
        foreach (var item in session.Cart)
        {
            total += item.Price * item.Qty;
        }

        // Payload to send to N8n Payment workflow
        // N8n creates Xendit invoice; order details already in Firebase
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

            if (!response.IsSuccessStatusCode)
            {
                return new CheckoutResult
                {
                    Success = false,
                    Error = $"N8n payment workflow failed with status {response.StatusCode}"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CheckoutResult>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                return new CheckoutResult
                {
                    Success = false,
                    Error = "Empty response from N8n"
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

/// <summary>
/// Response from N8n Payment workflow.
/// Xendit webhook callback will use order_id to retrieve full order details.
/// </summary>
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
