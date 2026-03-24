namespace TelegramBot.Models;

public class Session
{
    public long ChatId { get; set; }
    public string State { get; set; } = SessionState.Browsing;
    public List<CartItem> Cart { get; set; } = new();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string XenditInvoiceId { get; set; } = string.Empty;
}

public static class SessionState
{
    public const string Browsing = "BROWSING";
    public const string ChatMode = "CHAT_MODE";
    public const string AwaitingName = "AWAITING_NAME";
    public const string AwaitingPhone = "AWAITING_PHONE";
    public const string AwaitingPayment = "AWAITING_PAYMENT";
    public const string Completed = "COMPLETED";
}
