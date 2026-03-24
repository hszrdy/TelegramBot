using TelegramBot.Interfaces;
using TelegramBot.Models;

namespace TelegramBot.Services;

/// <summary>
/// Platform-agnostic business logic for state-based message handling.
/// Processes user inputs during checkout (name, phone).
/// </summary>
public class StateService
{
    private readonly FirestoreService _firestore;
    private readonly OrderService _orderService;

    public StateService(FirestoreService firestore, OrderService orderService)
    {
        _firestore = firestore;
        _orderService = orderService;
    }

    public async Task HandleAwaitingNameAsync(
        long chatId,
        string nameInput,
        Session session,
        IMessagingPlatformHandler platform)
    {
        var name = nameInput.Trim();

        if (name.Length < 2)
        {
            await platform.SendMessageAsync(chatId, "Please enter a valid name:");
            return;
        }

        await _firestore.SetCustomerInfoAsync(chatId, name, session.CustomerPhone);
        await _firestore.SetStateAsync(chatId, SessionState.AwaitingPhone);
        await platform.SendMessageAsync(chatId, $"Got it, {name}! Now please enter your phone number:");
    }

    public async Task HandleAwaitingPhoneAsync(
        long chatId,
        string phoneInput,
        Session session,
        IMessagingPlatformHandler platform)
    {
        var phone = phoneInput.Trim();

        if (phone.Length < 7)
        {
            await platform.SendMessageAsync(chatId, "Please enter a valid phone number:");
            return;
        }

        await _firestore.SetCustomerInfoAsync(chatId, session.CustomerName, phone);

        // All info collected — refresh session and process checkout
        var updatedSession = await _firestore.GetOrCreateSessionAsync(chatId);
        await _orderService.ProcessCheckoutAsync(chatId, updatedSession, platform);
    }
}
