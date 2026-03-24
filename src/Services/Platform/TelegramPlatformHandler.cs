using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBot.Handlers;
using TelegramBot.Interfaces;
using TelegramBot.Models;

namespace TelegramBot.Services.Platform;

/// <summary>
/// Telegram-specific implementation of IMessagingPlatformHandler.
/// Handles all Telegram.Bot API calls.
/// </summary>
public class TelegramPlatformHandler : IMessagingPlatformHandler
{
    private readonly ITelegramBotClient _bot;

    public TelegramPlatformHandler(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task SendMenuAsync(long chatId, List<CartItem> cart)
    {
        await _bot.SendMessage(
            chatId,
            $"🍽️ <b>Menu</b>\n\n{CartFormatter.FormatSummary(cart)}\n\nTap an item to add more:",
            parseMode: ParseMode.Html,
            replyMarkup: MenuKeyboard.Build(cart)
        );
    }

    public async Task EditMenuAsync(long chatId, int messageId, List<CartItem> cart)
    {
        await _bot.EditMessageText(
            chatId,
            messageId,
            $"🍽️ <b>Menu</b>\n\n{CartFormatter.FormatSummary(cart)}\n\nTap an item to add more:",
            parseMode: ParseMode.Html,
            replyMarkup: MenuKeyboard.Build(cart)
        );
    }

    public async Task ShowCartAsync(long chatId, int messageId, List<CartItem> cart)
    {
        if (cart.Count == 0)
        {
            await SendMessageAsync(chatId, "Your cart is empty!");
            return;
        }

        await _bot.EditMessageText(
            chatId,
            messageId,
            CartFormatter.Format(cart),
            parseMode: ParseMode.Html,
            replyMarkup: CartKeyboard.Build()
        );
    }

    public async Task SendCheckoutConfirmAsync(long chatId, List<CartItem> cart, decimal total)
    {
        var cartText = CartFormatter.Format(cart);
        await _bot.SendMessage(
            chatId,
            $"{cartText}\n\n💳 <b>Total: ₱{total}</b>\n\nProceed to checkout?",
            parseMode: ParseMode.Html,
            replyMarkup: CheckoutKeyboard.Build()
        );
    }

    public async Task AnswerButtonClickAsync(string callbackId, string toastText)
    {
        await _bot.AnswerCallbackQuery(callbackId, toastText);
    }

    public async Task SendMessageAsync(long chatId, string text)
    {
        await _bot.SendMessage(chatId, text);
    }

    public async Task SendOrderSuccessAsync(long chatId, string orderText, string invoiceUrl)
    {
        await _bot.SendMessage(
            chatId,
            $"✅ <b>Order placed!</b>\n\n{orderText}\n\n" +
            $"Complete your payment here:\n<a href=\"{invoiceUrl}\">Pay Now</a>",
            parseMode: ParseMode.Html
        );
    }

    public async Task ShowMenuAfterClearAsync(long chatId, int messageId)
    {
        await _bot.EditMessageText(
            chatId,
            messageId,
            "🛒 Cart cleared. Use /menu to start ordering.",
            replyMarkup: MenuKeyboard.Build()
        );
    }
}
