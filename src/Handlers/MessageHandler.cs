using Telegram.Bot.Types;
using TelegramBot.Interfaces;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Handlers;

/// <summary>
/// Telegram-specific message handler.
/// Dispatches to platform-agnostic services for business logic.
/// </summary>
public class MessageHandler
{
    private readonly IMessagingPlatformHandler _platform;
    private readonly CommandHandler _commandHandler;
    private readonly StateService _stateService;

    public MessageHandler(
        IMessagingPlatformHandler platform,
        StateService stateService,
        CommandHandler commandHandler)
    {
        _platform = platform;
        _stateService = stateService;
        _commandHandler = commandHandler;
    }

    public async Task HandleAsync(Message message, Session session)
    {
        var text = message.Text ?? string.Empty;

        // Non-text messages
        if (string.IsNullOrEmpty(text))
        {
            await _platform.SendMessageAsync(
                message.Chat.Id,
                "Sorry, I can only process text messages right now."
            );
            return;
        }

        // Always handle commands regardless of state
        if (text.StartsWith('/'))
        {
            await _commandHandler.HandleAsync(message, session);
            return;
        }

        // Route based on session state
        if (session.State.StartsWith("AWAITING_QUANTITY_"))
        {
            var stateParts = session.State.Substring("AWAITING_QUANTITY_".Length).Split('|');
            var itemName = stateParts[0];
            var price = decimal.Parse(stateParts[1]);
            await _stateService.HandleAwaitingQuantityAsync(message.Chat.Id, itemName, price, text, session, _platform);
            return;
        }

        switch (session.State)
        {
            case SessionState.AwaitingName:
                await _stateService.HandleAwaitingNameAsync(message.Chat.Id, text, session, _platform);
                break;

            case SessionState.AwaitingPhone:
                await _stateService.HandleAwaitingPhoneAsync(message.Chat.Id, text, session, _platform);
                break;

            default:
                // User typed something without a command in browsing state
                await _platform.SendMessageAsync(
                    message.Chat.Id,
                    "Use /menu to browse our menu or /help for available commands."
                );
                break;
        }
    }
}
