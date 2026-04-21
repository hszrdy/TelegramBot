using DotNetEnv;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Handlers;
using TelegramBot.Interfaces;
using TelegramBot.Services;
using TelegramBot.Services.Platform;
using TelegramBot.Models;
// Load .env file for local development
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ── Config ────────────────────────────────────────────────────────────────
var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
    ?? throw new Exception("TELEGRAM_BOT_TOKEN is not set");

var firestoreProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
    ?? throw new Exception("FIREBASE_PROJECT_ID is not set");

var n8nCheckoutUrl = Environment.GetEnvironmentVariable("N8N_CHECKOUT_WEBHOOK_URL")
    ?? throw new Exception("N8N_CHECKOUT_WEBHOOK_URL is not set");

var webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL")
    ?? throw new Exception("WEBHOOK_URL is not set");

var menusWebhookUrl = Environment.GetEnvironmentVariable("N8N_MENUS_WEBHOOK_URL")
    ?? throw new Exception("N8N_MENUS_WEBHOOK_URL");

var agentWebhookUrl = Environment.GetEnvironmentVariable("N8N_AI_INTEGRATION_URL")
    ?? throw new Exception("N8N_AI_INTEGRATION_URL");

// ── Services ──────────────────────────────────────────────────────────────
var botClient = new TelegramBotClient(botToken);
var firestoreService = new FirestoreService(firestoreProjectId);
var httpClient = new HttpClient();
var n8nService = new N8nService(httpClient, n8nCheckoutUrl);
var menuService = new MenuService(httpClient, menusWebhookUrl);
var agentService = new AIAgentService(httpClient, agentWebhookUrl);

builder.Services.AddSingleton<ITelegramBotClient>(botClient);
builder.Services.AddSingleton(firestoreService);
builder.Services.AddSingleton(n8nService);
builder.Services.AddSingleton(menuService);
builder.Services.AddSingleton(agentService);

// ── Platform Handler (Telegram-specific) ──────────────────────────────────
builder.Services.AddSingleton<IMessagingPlatformHandler>(
    new TelegramPlatformHandler(botClient, menuService)
);

// ── Business Logic Services (Platform-agnostic) ───────────────────────────
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<StateService>();

// ── Handlers ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton<MessageHandler>();
builder.Services.AddSingleton<CallbackQueryHandler>();

// ── JSON serialization for Telegram snake_case properties ────────────────
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

// ── Request logging middleware ────────────────────────────────────────────
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/webhook" && context.Request.Method == "POST")
    {
        context.Request.EnableBuffering();
        var body = await new System.IO.StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;

        Console.WriteLine($"Raw webhook body: {body}");
    }

    await next();
});

// ── Webhook endpoint ──────────────────────────────────────────────────────
app.MapPost("/webhook", async (
    Update update,
    ITelegramBotClient bot,
    FirestoreService firestore,
    MessageHandler messageHandler,
    CallbackQueryHandler callbackHandler) =>
{
    // Only respond to private chats
    if (update.Message != null && update.Message.Chat.Type != ChatType.Private)
        return Results.Ok();

    if (update.CallbackQuery != null && update.CallbackQuery.Message?.Chat.Type != ChatType.Private)
        return Results.Ok();

    Console.WriteLine($"Update received: {update.Type}");
    Console.WriteLine($"Raw update: Message={update.Message != null}, CallbackQuery={update.CallbackQuery != null}");

    try
    {
        // Check by object existence instead of UpdateType enum
        if (update.Message != null)
        {
            Console.WriteLine($"Message text: {update.Message.Text}");
            var chatId = update.Message.Chat.Id;
            var session = await firestore.GetOrCreateSessionAsync(chatId);
            await messageHandler.HandleAsync(update.Message, session);
        }
        else if (update.CallbackQuery != null)
        {
            Console.WriteLine($"Callback data: {update.CallbackQuery.Data}");
            var chatId = update.CallbackQuery.Message!.Chat.Id;
            var session = await firestore.GetOrCreateSessionAsync(chatId);
            await callbackHandler.HandleAsync(update.CallbackQuery, session);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Stack: {ex.StackTrace}");
    }

    return Results.Ok();
});

app.MapPost("/payment-confirmed", async (
    HttpContext context,
    FirestoreService firestore) =>
{
    var request = await context.Request.ReadFromJsonAsync<PaymentConfirmedRequest>();

    if (request == null)
        return Results.BadRequest("Missing chat_id");

    await firestore.SetStateAsync(request.ChatIdLong, SessionState.Completed);
    Console.WriteLine($"[Payment] State set to Complete for chatId: {request.ChatId}");

    return Results.Ok(new { success = true });
});

// ── Health check ──────────────────────────────────────────────────────────
app.MapGet("/", () => "Bot is running!");

// ── Set webhook on startup ────────────────────────────────────────────────
await botClient.SetWebhook(
    $"{webhookUrl}/webhook",
    allowedUpdates: new[] { UpdateType.Message, UpdateType.CallbackQuery }
);
Console.WriteLine($"Webhook set to {webhookUrl}/webhook");

app.Run();
public record PaymentConfirmedRequest(string ChatId)
{
    public long ChatIdLong => long.Parse(ChatId);
}