using TelegramBot.Models;

namespace TelegramBot.Interfaces;

/// <summary>
/// Platform-agnostic interface for messaging handlers.
/// Abstracts away Telegram-specific (or Viber-specific) implementations.
/// Allows easy porting between platforms.
/// </summary>
public interface IMessagingPlatformHandler
{
    /// <summary>
    /// Send a menu message with inline buttons to add items.
    /// </summary>
    Task SendMenuAsync(long chatId, List<CartItem> cart);

    /// <summary>
    /// Edit an existing menu message (e.g., to update cart count display).
    /// </summary>
    Task EditMenuAsync(long chatId, int messageId, List<CartItem> cart);

    /// <summary>
    /// Show a cart summary with options to modify/checkout.
    /// </summary>
    Task ShowCartAsync(long chatId, int messageId, List<CartItem> cart);

    /// <summary>
    /// Show checkout confirmation dialog.
    /// </summary>
    Task SendCheckoutConfirmAsync(long chatId, List<CartItem> cart, decimal total);

    /// <summary>
    /// Handle a button click acknowledgment (toast/notification).
    /// </summary>
    Task AnswerButtonClickAsync(string callbackId, string toastText);

    /// <summary>
    /// Send a text message (for state-based responses like "Enter your name").
    /// </summary>
    Task SendMessageAsync(long chatId, string text);

    /// <summary>
    /// Send order success with payment link.
    /// </summary>
    Task SendOrderSuccessAsync(long chatId, string orderText, string invoiceUrl);

    /// <summary>
    /// Clear the cart and show menu.
    /// </summary>
    Task ShowMenuAfterClearAsync(long chatId, int messageId);
}
