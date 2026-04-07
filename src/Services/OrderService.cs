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

    public async Task HandleIncrementItemAsync(
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

    public async Task HandleDecrementItemAsync(
        long chatId,
        int messageId,
        string itemName,
        Session session,
        string callbackId,
        IMessagingPlatformHandler platform)
    {
        var existing = session.Cart.FirstOrDefault(i => i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        if (existing == null || existing.Qty <= 1)
        {
            // Remove the item if qty <=1
            var item = new CartItem { Name = itemName, Qty = 1, Price = 0 };
            var updatedCart = await _firestore.UpdateCartAsync(chatId, "remove", new List<CartItem> { item });
            await platform.AnswerButtonClickAsync(callbackId, $"❌ {itemName} removed from cart");
            await platform.EditMenuAsync(chatId, messageId, updatedCart);
        }
        else
        {
            // Decrement qty
            existing.Qty -= 1;
            var updatedCart = await _firestore.UpdateCartAsync(chatId, "update", new List<CartItem> { existing });
            await platform.AnswerButtonClickAsync(callbackId, $"✅ {itemName} x{existing.Qty} in cart");
            await platform.EditMenuAsync(chatId, messageId, updatedCart);
        }
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

        var result = await _n8n.CheckoutAsync(chatId, session);

        if (!result.Success)
        {
            await platform.SendMessageAsync(
                chatId,
                $"❌ Checkout failed: {result.Error}\n\nPlease try again or contact support."
            );
            return;
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
