using TelegramBot.Interfaces;
using TelegramBot.Models;

namespace TelegramBot.Services;

/// <summary>
/// Platform-agnostic business logic for order/cart operations.
/// Works with any platform handler (Telegram, Viber, etc.)
/// </summary>
public class OrderService
{
    private readonly FirestoreService _firestore;
    private readonly N8nService _n8n;

    public OrderService(FirestoreService firestore, N8nService n8n)
    {
        _firestore = firestore;
        _n8n = n8n;
    }

    public async Task HandleAddItemAsync(
        long chatId,
        int messageId,
        string itemName,
        decimal price,
        Session session,
        string callbackId,
        IMessagingPlatformHandler platform)
    {
        var item = new CartItem
        {
            Name = itemName,
            Price = price,
            Qty = 1
        };

        var updatedCart = await _firestore.UpdateCartAsync(chatId, "add", new List<CartItem> { item });
        var added = updatedCart.First(i => i.Name == itemName);

        await platform.AnswerButtonClickAsync(callbackId, $"✅ {itemName} x{added.Qty} in cart");
        await platform.EditMenuAsync(chatId, messageId, updatedCart);
    }

    public async Task HandleRemoveItemAsync(
        long chatId,
        int messageId,
        string itemName,
        Session session,
        IMessagingPlatformHandler platform)
    {
        var item = new CartItem { Name = itemName, Qty = 1, Price = 0 };
        var updatedCart = await _firestore.UpdateCartAsync(chatId, "remove", new List<CartItem> { item });

        await platform.ShowCartAsync(chatId, messageId, updatedCart);
    }

    public async Task HandleViewCartAsync(
        long chatId,
        int messageId,
        Session session,
        IMessagingPlatformHandler platform)
    {
        if (session.Cart.Count == 0)
        {
            await platform.SendMessageAsync(chatId, "Your cart is empty!");
            return;
        }

        await platform.ShowCartAsync(chatId, messageId, session.Cart);
    }

    public async Task HandleClearCartAsync(
        long chatId,
        int messageId,
        Session session,
        IMessagingPlatformHandler platform)
    {
        await _firestore.UpdateCartAsync(chatId, "clear", new List<CartItem>());
        await platform.ShowMenuAfterClearAsync(chatId, messageId);
    }

    public async Task HandleBackToMenuAsync(
        long chatId,
        int messageId,
        Session session,
        IMessagingPlatformHandler platform)
    {
        await platform.EditMenuAsync(chatId, messageId, session.Cart);
    }

    public async Task HandleCheckoutAsync(
        long chatId,
        Session session,
        IMessagingPlatformHandler platform)
    {
        if (session.Cart.Count == 0)
        {
            await platform.SendMessageAsync(chatId, "Your cart is empty! Add some items first.");
            return;
        }

        var total = session.Cart.Sum(i => i.Price * i.Qty);
        await platform.SendCheckoutConfirmAsync(chatId, session.Cart, total);
    }

    public async Task HandleConfirmCheckoutAsync(
        long chatId,
        Session session,
        IMessagingPlatformHandler platform)
    {
        if (string.IsNullOrEmpty(session.CustomerName))
        {
            await _firestore.SetStateAsync(chatId, SessionState.AwaitingName);
            await platform.SendMessageAsync(chatId, "Please enter your full name:");
            return;
        }

        if (string.IsNullOrEmpty(session.CustomerPhone))
        {
            await _firestore.SetStateAsync(chatId, SessionState.AwaitingPhone);
            await platform.SendMessageAsync(chatId, "Please enter your phone number:");
            return;
        }

        // All info collected — process checkout
        await ProcessCheckoutAsync(chatId, session, platform);
    }

    public async Task ProcessCheckoutAsync(
    long chatId,
    Session session,
    IMessagingPlatformHandler platform)
{
    await platform.SendMessageAsync(chatId, "⏳ Processing your order...");

    // Generate order ID here so we can save to Firestore before calling n8n
    var orderId = Guid.NewGuid().ToString().Substring(0, 12).ToUpper();
    Console.WriteLine($"[Order] Generated orderId: {orderId} for chatId: {chatId}");

    // Save order_id to session — lets us look up chat_id from Xendit callback
    await _firestore.SetOrderIdAsync(chatId, orderId);
    Console.WriteLine($"[Order] Saved orderId to Firestore");

    var result = await _n8n.CheckoutAsync(chatId, session, orderId);

    if (!result.Success)
    {
        await platform.SendMessageAsync(
            chatId,
            $"❌ Checkout failed: {result.Error}\n\nPlease try again or contact support."
        );
        return;
    }

    if (!string.IsNullOrEmpty(result.InvoiceId))
    {
        await _firestore.SetXenditInvoiceIdAsync(chatId, result.InvoiceId);
        Console.WriteLine($"[Order] Saved xendit_invoice_id: {result.InvoiceId}");
    }

    await _firestore.SetStateAsync(chatId, SessionState.AwaitingPayment);

    var orderText = $"Order ID: <code>{result.OrderId}</code>";
    await platform.SendOrderSuccessAsync(
        chatId,
        orderText,
        result.InvoiceUrl!
    );
}
}
