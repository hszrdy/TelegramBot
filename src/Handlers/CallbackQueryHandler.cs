using Telegram.Bot.Types;
using TelegramBot.Interfaces;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Handlers;

/// <summary>
/// Telegram-specific callback handler.
/// Dispatches to platform-agnostic OrderService for business logic.
/// </summary>
public class CallbackQueryHandler
{
    private readonly OrderService _orderService;
    private readonly IMessagingPlatformHandler _platform;
    private readonly FirestoreService _firestore;

    public CallbackQueryHandler(OrderService orderService, IMessagingPlatformHandler platform, FirestoreService firestore)
    {
        _orderService = orderService;
        _platform = platform;
        _firestore = firestore;
    }

    public async Task HandleAsync(CallbackQuery callbackQuery, Session session)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;
        var data = callbackQuery.Data ?? string.Empty;
        var parts = data.Split('|');
        var action = parts[0];

        // Always acknowledge the button tap immediately
        await _platform.AnswerButtonClickAsync(callbackQuery.Id, "");

        try
        {
            switch (action)
            {
                case "add":
                    if (parts.Length < 3) break;
                    var itemName = parts[1];
                    var price = decimal.Parse(parts[2]);
                    await _orderService.HandleAddItemAsync(
                        chatId, messageId, itemName, price, session, callbackQuery.Id, _platform
                    );
                    break;

                case "increment":
                    if (parts.Length < 3) break;
                    var incItemName = parts[1];
                    var incPrice = decimal.Parse(parts[2]);
                    await _orderService.HandleIncrementItemAsync(
                        chatId, messageId, incItemName, incPrice, session, callbackQuery.Id, _platform
                    );
                    break;

                case "decrement":
                    if (parts.Length < 3) break;
                    var decItemName = parts[1];
                    var decPrice = decimal.Parse(parts[2]);
                    await _orderService.HandleDecrementItemAsync(
                        chatId, messageId, decItemName, session, callbackQuery.Id, _platform
                    );
                    break;

                case "select_item":
                    if (parts.Length < 3) break;
                    var selItemName = parts[1];
                    var selPrice = decimal.Parse(parts[2]);
                    var currentQty = session.Cart.FirstOrDefault(c => c.Name.Equals(selItemName, StringComparison.OrdinalIgnoreCase))?.Qty ?? 0;
                    await _firestore.SetStateAsync(chatId, $"AWAITING_QUANTITY_{selItemName}|{selPrice}");
                    await _platform.SendMessageAsync(chatId, $"Enter quantity for {selItemName} (current: {currentQty}):");
                    await _platform.AnswerButtonClickAsync(callbackQuery.Id, "");
                    break;

                case "noop":
                    // Do nothing
                    break;

                case "remove":
                    if (parts.Length < 2) break;
                    await _orderService.HandleRemoveItemAsync(
                        chatId, messageId, parts[1], session, _platform
                    );
                    break;

                case "view_cart":
                    await _orderService.HandleViewCartAsync(chatId, messageId, session, _platform);
                    break;

                case "clear_cart":
                    await _orderService.HandleClearCartAsync(chatId, messageId, session, _platform);
                    break;

                case "back_to_menu":
                    await _orderService.HandleBackToMenuAsync(chatId, messageId, session, _platform);
                    break;

                case "checkout":
                    await _orderService.HandleCheckoutAsync(chatId, session, _platform);
                    break;

                case "confirm_checkout":
                    await _orderService.HandleConfirmCheckoutAsync(chatId, session, _platform);
                    break;

                default:
                    Console.WriteLine($"Unknown callback action: {action}");
                    await _platform.AnswerButtonClickAsync(callbackQuery.Id, "❌ Unknown command");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in callback handler: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
}
