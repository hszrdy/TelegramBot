using Telegram.Bot.Types;
using TelegramBot.Interfaces;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Handlers;

/// <summary>
/// Telegram-specific command handler (/menu, /help, /cart, etc.).
/// Dispatches to platform handler for UI rendering.
/// </summary>
public class CommandHandler
{
    private readonly IMessagingPlatformHandler _platform;
    private readonly FirestoreService _firestore;

    public CommandHandler(IMessagingPlatformHandler platform, FirestoreService firestore)
    {
        _platform = platform;
        _firestore = firestore;
    }

    public async Task HandleAsync(Message message, Session session)
    {
        var command = message.Text?.Split(' ')[0].ToLower();

        switch (command)
        {
            case "/start":
            case "/menu":
                await HandleMenuAsync(message, session);
                break;

            case "/chat":
                await HandleChatAsync(message, session);
                break;

            case "/cart":
                await HandleViewCartAsync(message, session);
                break;

            case "/cancel":
                await HandleCancelAsync(message, session);
                break;

            case "/help":
                await HandleHelpAsync(message);
                break;

            case "/cancelchat":
                await HandleCancelChatAsync(message, session);
                break;

            default:
                await _platform.SendMessageAsync(
                    message.Chat.Id,
                    "Unknown command. Use /menu to order or /help for available commands."
                );
                break;
        }
    }

    private async Task HandleMenuAsync(Message message, Session session)
    {
        // Reset to browsing state when user opens menu
        await _firestore.SetStateAsync(session.ChatId, SessionState.Browsing);

        // SendMenuAsync will fetch menu from MenuService and build keyboard
        await _platform.SendMenuAsync(message.Chat.Id, session.Cart);
    }

    private async Task HandleViewCartAsync(Message message, Session session)
    {
        if (session.Cart.Count == 0)
        {
            await _platform.SendMessageAsync(message.Chat.Id, "Your cart is empty. Use /menu to start ordering.");
            return;
        }

        var cartText = CartFormatter.Format(session.Cart);
        await _platform.SendMessageAsync(message.Chat.Id, cartText);
    }

    private async Task HandleCancelAsync(Message message, Session session)
    {
        await _firestore.ResetSessionAsync(session.ChatId);
        await _platform.SendMessageAsync(
            message.Chat.Id,
            "❌ Order cancelled. Your cart has been cleared.\n\nUse /menu to start a new order."
        );
    }

    private async Task HandleChatAsync(Message message, Session session)
    {
        await _firestore.SetStateAsync(session.ChatId, SessionState.ChatMode);
        await _platform.SendMessageAsync(
            message.Chat.Id,
            "🤖 Chat mode activated. Send any message and I will respond as your AI assistant.\n\nUse /menu to return to the menu browsing experience."
        );
    }

    private async Task HandleCancelChatAsync(Message message, Session session)
    {
        await _firestore.SetStateAsync(session.ChatId, SessionState.Browsing);
        await _platform.SendMessageAsync(
            message.Chat.Id,
            "🤖 Chat mode deactivated. Use /start, /menu or /help to get started."
        );
    }

    private async Task HandleHelpAsync(Message message)
    {
        await _platform.SendMessageAsync(
            message.Chat.Id,
            "📋 <b>Available commands:</b>\n\n" +
            "/menu — Browse and order items\n" +
            "/chat — Enter AI chat mode\n" +
            "/cancelchat — Exit AI chat mode\n" +
            "/cart — View your current cart\n" +
            "/cancel — Cancel your current order\n" +
            "/help — Show this message"
        );
    }
}
