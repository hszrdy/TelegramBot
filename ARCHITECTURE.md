# TelegramBot - Complete Architecture Guide

## Table of Contents
1. [System Overview](#system-overview)
2. [Architecture Layers](#architecture-layers)
3. [Component Responsibilities](#component-responsibilities)
4. [Data Models](#data-models)
5. [Configuration & Environment](#configuration--environment)
6. [Data Flow](#data-flow)
7. [Extension Patterns](#extension-patterns)
8. [Common Tasks](#common-tasks)
9. [Troubleshooting](#troubleshooting)
10. [Best Practices](#best-practices)

---

## System Overview

### What This Project Does
This is a **Telegram ordering chatbot** that allows users to:
- Browse a menu of items
- Add items to a shopping cart
- Proceed through checkout
- Make payments via N8n integration

### Technology Stack
| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Messaging Platform** | Telegram Bot API | Receives user messages & callbacks |
| **Web Server** | ASP.NET Core 8 | Webhook receiver for Telegram |
| **Database** | Google Firestore | Persists user sessions & cart data |
| **Payment/Checkout** | N8n | External workflow for order processing |
| **JSON Serialization** | System.Text.Json | Handles Telegram's snake_case format |

### Key Architectural Decision: Platform Abstraction
The codebase uses an **IMessagingPlatformHandler** interface to abstract away Telegram-specific code. This allows:
- Easy porting to Viber, WhatsApp, or other platforms
- Clear separation of concerns
- Platform-agnostic business logic

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│ ENTRY POINT                                                 │
│ Program.cs (Webhook receiver, DI setup)                    │
└─────────────────────────────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ HANDLERS (Platform-Specific)                                │
│ - CallbackQueryHandler (button clicks)                     │
│ - MessageHandler (text messages)                           │
│ - CommandHandler (slash commands)                          │
└─────────────────────────────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ PLATFORM ABSTRACTION                                        │
│ IMessagingPlatformHandler                                  │
│ └─ TelegramPlatformHandler (implementation)                │
│ └─ ViberPlatformHandler (template ready)                   │
└─────────────────────────────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ BUSINESS LOGIC (Platform-Agnostic)                         │
│ - OrderService (cart operations)                           │
│ - StateService (user state transitions)                    │
└─────────────────────────────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ DATA ACCESS & EXTERNAL SERVICES                            │
│ - FirestoreService (user sessions, carts)                 │
│ - N8nService (checkout webhook)                           │
└─────────────────────────────────────────────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ EXTERNAL SYSTEMS                                            │
│ - Google Firestore (database)                             │
│ - Telegram API (messaging)                                │
│ - N8n (workflow/payments)                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

### **ENTRY POINT: Program.cs**

**Responsibility**: 
- Load environment variables
- Build the ASP.NET Core application
- Register services (Dependency Injection)
- Configure JSON serialization
- Set up webhook endpoint
- Install Telegram webhook

**Key Code Sections**:
```csharp
// 1. Environment Config Loading
var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
var firestoreProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");

// 2. Service Registration
builder.Services.AddSingleton<IMessagingPlatformHandler>(
    new TelegramPlatformHandler(botClient)
);
builder.Services.AddSingleton<OrderService>();

// 3. JSON Configuration (Telegram uses snake_case)
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNamingPolicy = 
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

// 4. Webhook Endpoint
app.MapPost("/webhook", async (Update update, ...) => { ... });

// 5. Install Webhook
await botClient.SetWebhook($"{webhookUrl}/webhook");
```

**Extension Points**:
- Add new environment variables for new features
- Register new services in dependency injection
- Add middleware for logging, rate limiting, authentication

---

### **HANDLERS: Message Processing Layer**

All handlers receive Telegram updates and dispatch to business logic services.

#### 1. **CallbackQueryHandler** - Button Clicks
**Purpose**: Handle inline button presses from keyboards

**Flow**:
```
User clicks button
    ↓
Telegram sends CallbackQuery (with callback_data)
    ↓
ASP.NET deserializes to Update.CallbackQuery
    ↓
CallbackQueryHandler.HandleAsync() called
    ↓
Parses callback data (format: "action|param1|param2")
    ↓
Dispatches to OrderService methods
    ↓
OrderService calls IMessagingPlatformHandler to send response
```

**Supported Callback Actions**:
```csharp
"add|ItemName|Price"        // Add item to cart
"remove|ItemName"           // Remove item from cart
"view_cart"                 // Show cart contents
"clear_cart"               // Empty the cart
"back_to_menu"             // Return to menu
"checkout"                 // Start checkout process
"confirm_checkout"         // Confirm and process order
```

**Example: Adding an Item**
```
Button pressed: "add|Coffee|100"
    ↓
parts = ["add", "Coffee", "100"]
    ↓
OrderService.HandleAddItemAsync(
    chatId, messageId, "Coffee", 100m, session, 
    callbackId, _platform
)
    ↓
Creates CartItem { Name = "Coffee", Price = 100, Qty = 1 }
    ↓
Calls FirestoreService.UpdateCartAsync()
    ↓
Persists to Firestore (atomic transaction)
    ↓
Calls platform.AnswerButtonClickAsync() → Toast: "✅ Coffee x1 in cart"
    ↓
Calls platform.EditMenuAsync() → Updates menu with new qty
```

#### 2. **MessageHandler** - Text Messages
**Purpose**: Handle user text input based on session state

**Flow**:
```
User sends text message
    ↓
Telegram sends Message
    ↓
MessageHandler.HandleAsync() called
    ↓
Routes based on session.State:
  - Starts with "/" → CommandHandler
  - State = AwaitingName → StateService.HandleAwaitingNameAsync()
  - State = AwaitingPhone → StateService.HandleAwaitingPhoneAsync()
  - Default → Send help message
```

**Session States**:
| State | Triggered By | User Input | Next State |
|-------|---|---|---|
| `BROWSING` | /menu | Buttons or /help | BROWSING |
| `AWAITING_NAME` | checkout confirmation | Customer name text | AWAITING_PHONE |
| `AWAITING_PHONE` | name entered | Phone number text | AWAITING_PAYMENT |
| `AWAITING_PAYMENT` | checkout info complete | (automatic) | COMPLETED |

#### 3. **CommandHandler** - Slash Commands
**Purpose**: Handle special commands that work in any state

**Supported Commands**:
```
/start       → Show welcome menu
/menu        → Browse items (reset state to BROWSING)
/cart        → View current cart
/cancel      → Clear cart and reset
/help        → Show available commands
```

**Example: /menu Flow**
```csharp
/menu command received
    ↓
CommandHandler.HandleAsync() called
    ↓
Sets session.State = SessionState.Browsing
    ↓
Calls platform.SendMenuAsync(chatId, session.Cart)
    ↓
TelegramPlatformHandler creates inline keyboard with items
    ↓
User sees menu with buttons
```

---

### **PLATFORM ABSTRACTION LAYER**

#### **IMessagingPlatformHandler** Interface
Defines all platform operations in a messaging-system-agnostic way.

```csharp
public interface IMessagingPlatformHandler {
    // Menus & UIs
    Task SendMenuAsync(long chatId, List<CartItem> cart);
    Task EditMenuAsync(long chatId, int messageId, List<CartItem> cart);
    Task ShowCartAsync(long chatId, int messageId, List<CartItem> cart);
    Task SendCheckoutConfirmAsync(long chatId, List<CartItem> cart, decimal total);
    
    // Interactions
    Task AnswerButtonClickAsync(string callbackId, string toastText);
    Task SendMessageAsync(long chatId, string text);
    Task SendOrderSuccessAsync(long chatId, string orderText, string invoiceUrl);
    Task ShowMenuAfterClearAsync(long chatId, int messageId);
}
```

**Why This Matters**:
- All Telegram-specific code lives in `TelegramPlatformHandler`
- Business logic doesn't know about Telegram
- Easy to implement `ViberPlatformHandler` later
- Each method maps to a platform-specific operation

#### **TelegramPlatformHandler** Implementation
Contains all `Telegram.Bot` library calls.

```csharp
public class TelegramPlatformHandler : IMessagingPlatformHandler {
    private readonly ITelegramBotClient _bot;
    
    public async Task SendMenuAsync(long chatId, List<CartItem> cart) {
        // Telegram-specific: create inline keyboard
        var keyboard = MenuKeyboard.Build(cart);
        
        // Call Telegram API
        await _bot.SendMessage(
            chatId,
            "🍽️ <b>Menu</b>\n\n{CartFormatter.FormatSummary(cart)}...",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard
        );
    }
}
```

---

### **BUSINESS LOGIC LAYER**

#### 1. **OrderService** - Cart Operations
**Purpose**: Handle all shopping cart interactions without platform knowledge

**Key Methods**:
```csharp
// Add item to cart
HandleAddItemAsync(chatId, messageId, itemName, price, session, callbackId, platform)
    → Creates CartItem
    → Calls FirestoreService.UpdateCartAsync("add")
    → Updates menu display via platform

// Remove item from cart
HandleRemoveItemAsync(chatId, messageId, itemName, session, platform)
    → Calls FirestoreService.UpdateCartAsync("remove")
    → Shows updated cart

// Clear cart
HandleClearCartAsync(chatId, messageId, session, platform)
    → Calls FirestoreService.UpdateCartAsync("clear")
    → Shows empty message

// Show cart contents
HandleViewCartAsync(chatId, messageId, session, platform)
    → Calls platform.ShowCartAsync()

// Initiate checkout
HandleCheckoutAsync(chatId, session, platform)
    → Validates cart not empty
    → Calls platform.SendCheckoutConfirmAsync()

// Process order (once name & phone collected)
ProcessCheckoutAsync(chatId, session, platform)
    → Calls N8nService.CheckoutAsync()
    → Gets invoice URL + order ID
    → Calls platform.SendOrderSuccessAsync()
```

**Key Pattern**: All methods accept `IMessagingPlatformHandler platform` parameter, making them platform-independent.

#### 2. **StateService** - User State Transitions
**Purpose**: Handle multi-step checkout flow (name → phone → payment)

**Key Methods**:
```csharp
// Handle user entering their name during checkout
HandleAwaitingNameAsync(chatId, nameInput, session, platform)
    → Validates name length
    → Saves to Firestore via SetCustomerInfoAsync()
    → Updates state to AWAITING_PHONE
    → Prompts for phone number

// Handle user entering phone number
HandleAwaitingPhoneAsync(chatId, phoneInput, session, platform)
    → Validates phone number format
    → Saves to Firestore
    → Calls OrderService.ProcessCheckoutAsync()
```

**Important**: State transitions are persisted to Firestore, so if the user closes the app and reopens, the bot remembers where they were.

---

### **DATA ACCESS LAYER**

#### 1. **FirestoreService** - Persistence
**Purpose**: All database operations

**Firestore Data Structure**:
```
Firestore Database: sessions/{chatId}/

Document: {
    "state": "BROWSING" | "AWAITING_NAME" | "AWAITING_PHONE" | "AWAITING_PAYMENT",
    "cart": [
        {
            "name": "Coffee",
            "qty": 2,
            "price": 100.0,
            "instructions": "Extra hot"
        }
    ],
    "customer_name": "John Doe",
    "customer_phone": "+1234567890",
    "order_id": "ORD123",
    "xendit_invoice_id": "INV456"
}
```

**Key Methods**:
```csharp
// Get or create user session
GetOrCreateSessionAsync(long chatId)
    → Fetches from Firestore by chatId
    → Creates new if doesn't exist
    → Returns Session object

// Update cart (atomic transaction)
UpdateCartAsync(long chatId, string action, List<CartItem> items)
    → Fetches current cart from Firestore
    → Applies action ("add", "remove", "clear", "update")
    → Persists atomically (no race conditions)
    → Returns updated cart

// Update session state
SetStateAsync(long chatId, string newState)
    → Updates "state" field in Firestore
    → Used for state machine progression

// Update customer info
SetCustomerInfoAsync(long chatId, string name, string phone)
    → Saves customer details to Firestore

// Reset session
ResetSessionAsync(long chatId)
    → Clears cart and state
    → Used when user cancels order
```

**Atomic Transactions**:
```csharp
await _db.RunTransactionAsync(async transaction => {
    var snapshot = await transaction.GetSnapshotAsync(docRef);
    var cart = DeserializeCart(snapshot);
    
    // Modify
    cart = AddItems(cart, incomingItems);
    
    // Update atomically - prevents race condition if user
    // adds item in 2 windows simultaneously
    transaction.Update(docRef, "cart", SerializeCart(cart));
});
```

**Type Conversion Note**:
- C# uses `decimal` for prices
- Firestore doesn't support `decimal` natively
- SerializeCart converts `decimal → double` for Firestore
- DeserializeCart converts `double → decimal` when reading

#### 2. **N8nService** - External Workflow
**Purpose**: Call N8n webhook to process checkout

**How It Works**:
```csharp
public async Task<CheckoutResult> CheckoutAsync(long chatId, Session session) {
    var payload = new {
        chat_id = chatId,
        customer_name = session.CustomerName,
        customer_phone = session.CustomerPhone,
        cart = session.Cart.Select(i => new {
            name = i.Name,
            qty = i.Qty,
            price = i.Price,
            instructions = i.Instructions
        }),
        total = session.Cart.Sum(i => i.Price * i.Qty)
    };
    
    // Send to N8n webhook
    var response = await _http.PostAsync(_checkoutWebhookUrl, content);
    
    // N8n processes order and returns:
    // - Success: true/false
    // - InvoiceUrl: Payment link
    // - OrderId: Unique order ID
    // - Error: Error message if failed
}
```

**What N8n Does** (Your External Workflow):
1. Receives order details (cart, customer info)
2. Validates order
3. Creates invoice in payment gateway (Xendit, Stripe, etc.)
4. Returns invoice URL + order ID
5. Handles payment confirmation webhooks

---

## Data Models

### **Session**
```csharp
public class Session {
    public long ChatId { get; set; }              // Telegram user ID
    public string State { get; set; }             // Current state (see SessionState)
    public List<CartItem> Cart { get; set; }     // Items in cart
    public string CustomerName { get; set; }     // Collected during checkout
    public string CustomerPhone { get; set; }    // Collected during checkout
    public string OrderId { get; set; }          // From N8n after checkout
    public string XenditInvoiceId { get; set; }  // Payment gateway invoice ID
}
```

### **SessionState Constants**
```csharp
public static class SessionState {
    public const string Browsing = "BROWSING";           // Default, browsing menu
    public const string AwaitingName = "AWAITING_NAME";   // Checkout step 1
    public const string AwaitingPhone = "AWAITING_PHONE"; // Checkout step 2
    public const string AwaitingPayment = "AWAITING_PAYMENT"; // Awaiting payment
    public const string Completed = "COMPLETED";          // Order complete
}
```

### **CartItem**
```csharp
public class CartItem {
    public string Name { get; set; }              // "Coffee", "Cake", etc.
    public int Qty { get; set; }                 // Quantity (1, 2, 3...)
    public decimal Price { get; set; }           // Unit price (100m = ₱100)
    public string Instructions { get; set; }     // Special requests ("no sugar")
}
```

### **CheckoutResult**
```csharp
public class CheckoutResult {
    public bool Success { get; set; }
    public string? InvoiceUrl { get; set; }      // Payment link from N8n
    public string? OrderId { get; set; }         // Order reference
    public string? Error { get; set; }           // Error message if failed
}
```

---

## Configuration & Environment

### **Required Environment Variables**

Create a `.env` file in the project root:

```env
# Telegram Bot Token (from BotFather)
TELEGRAM_BOT_TOKEN=7123456789:ABCDefGHIjklmnopqrstuVWXYZabcdefgh

# Firebase/Firestore Project ID
FIREBASE_PROJECT_ID=your-firebase-project-id

# N8n Webhook URL for checkout processing
N8N_CHECKOUT_WEBHOOK_URL=https://your-n8n-instance.com/webhook/checkout

# Public webhook URL that Telegram uses to reach your bot
# Must be HTTPS and publicly accessible
WEBHOOK_URL=https://your-domain.com
```

### **Application Configuration** (appsettings.json)

Currently, the app doesn't use appsettings.json - configuration is entirely environment-driven. To add config:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Firebase": {
    "ProjectId": "from-env-variable"
  },
  "Telegram": {
    "BotToken": "from-env-variable"
  }
}
```

### **Service Registration** (Program.cs)

```csharp
// Platform-agnostic services
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<StateService>();
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<N8nService>();

// Platform-specific handler
builder.Services.AddSingleton<IMessagingPlatformHandler>(
    new TelegramPlatformHandler(botClient)
);

// Message/callback handlers
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton<MessageHandler>();
builder.Services.AddSingleton<CallbackQueryHandler>();
```

---

## Data Flow

### **Complete User Journey**

```
┌─────────────────────────────────────────────────────────────┐
│ 1. USER SENDS /start COMMAND                               │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
    Telegram sends: Update { Message { Text = "/start" } }
                           │
                           ▼
            ASP.NET webhook receives POST /webhook
                           │
                           ▼
         Program.cs deserializes Update (snake_case → PascalCase)
                           │
                           ▼
    Gets session from Firestore: GetOrCreateSessionAsync(chatId)
                           │
                           ▼
              Message.Text starts with "/" → CommandHandler
                           │
                           ▼
              CommandHandler.HandleAsync() matches "/start"
                           │
                           ▼
            Sets State = "BROWSING" via FirestoreService
                           │
                           ▼
            Calls platform.SendMenuAsync(chatId, cart=[])
                           │
                           ▼
    TelegramPlatformHandler creates inline keyboard with items
                           │
                           ▼
         Telegram Bot sends message with buttons to user's chat
                           │
        ┌──────────────────────────────────────────────────────┐
        │ 2. USER CLICKS "Coffee — ₱100" BUTTON               │
        └──────────────────────────────────────────────────────┘
                           │
                           ▼
    Telegram sends: Update { CallbackQuery { Data = "add|Coffee|100" } }
                           │
                           ▼
                 Webhook receives update
                           │
                           ▼
        CallbackQueryHandler.HandleAsync() called
                           │
                           ▼
        Parses: action="add", itemName="Coffee", price=100m
                           │
                           ▼
    OrderService.HandleAddItemAsync() called with platform
                           │
                           ▼
        Create CartItem { Name="Coffee", Price=100, Qty=1 }
                           │
                           ▼
    FirestoreService.UpdateCartAsync("add", [cartItem])
                           │
                           ▼
        [Atomic Transaction in Firestore]
        - GET current cart from Firestore
        - MODIFY: Add Coffee (or increment qty if exists)
        - UPDATE: Persist to Firestore
                           │
                           ▼
    Returns updated cart with Coffee qty=1
                           │
                           ▼
        platform.AnswerButtonClickAsync() → Toast: "✅ Coffee x1 in cart"
                           │
                           ▼
        platform.EditMenuAsync() → Update menu message with qty display
                           │
                           ▼
     User sees menu refresh with "Coffee — ₱100 (1)"
        
        ┌──────────────────────────────────────────────────────┐
        │ 3. USER CLICKS "✅ CHECKOUT" BUTTON                  │
        └──────────────────────────────────────────────────────┘
                           │
                           ▼
    CallbackQuery { Data = "checkout" }
                           │
                           ▼
    OrderService.HandleCheckoutAsync()
                           │
                           ▼
    Validates cart not empty (Coffee x1)
                           │
                           ▼
    Calculates total = 1 * 100 = 100
                           │
                           ▼
    platform.SendCheckoutConfirmAsync() sends confirmation dialog
                           │
        ┌──────────────────────────────────────────────────────┐
        │ 4. USER CLICKS "✅ Yes, proceed"                     │
        └──────────────────────────────────────────────────────┘
                           │
                           ▼
    CallbackQuery { Data = "confirm_checkout" }
                           │
                           ▼
    OrderService.HandleConfirmCheckoutAsync()
                           │
                           ▼
    Check if CustomerName set → NOT SET
                           │
                           ▼
    FirestoreService.SetStateAsync(chatId, "AWAITING_NAME")
                           │
                           ▼
    platform.SendMessageAsync() → "Please enter your full name:"
                           │
        ┌──────────────────────────────────────────────────────┐
        │ 5. USER SENDS TEXT "John Doe"                        │
        └──────────────────────────────────────────────────────┘
                           │
                           ▼
    Message { Text = "John Doe" }
                           │
                           ▼
    MessageHandler.HandleAsync() gets session
                           │
                           ▼
    session.State = "AWAITING_NAME"
                           │
                           ▼
    StateService.HandleAwaitingNameAsync()
                           │
                           ▼
    Validates: name.Length >= 2 ✓
                           │
                           ▼
    FirestoreService.SetCustomerInfoAsync(chatId, "John Doe", "")
                           │
                           ▼
    FirestoreService.SetStateAsync(chatId, "AWAITING_PHONE")
                           │
                           ▼
    platform.SendMessageAsync() → "Got it, John! Now please enter your phone number:"
                           │
        ┌──────────────────────────────────────────────────────┐
        │ 6. USER SENDS TEXT "+1234567890"                     │
        └──────────────────────────────────────────────────────┘
                           │
                           ▼
    Message { Text = "+1234567890" }
                           │
                           ▼
    MessageHandler.HandleAsync()
                           │
                           ▼
    session.State = "AWAITING_PHONE"
                           │
                           ▼
    StateService.HandleAwaitingPhoneAsync()
                           │
                           ▼
    Validates: phone.Length >= 7 ✓
                           │
                           ▼
    FirestoreService.SetCustomerInfoAsync(chatId, "John Doe", "+1234567890")
                           │
                           ▼
    Fetches updated session from Firestore
                           │
                           ▼
    OrderService.ProcessCheckoutAsync()
                           │
                           ▼
    N8nService.CheckoutAsync() POSTS to N8n webhook:
    {
        "chat_id": 7722703255,
        "customer_name": "John Doe",
        "customer_phone": "+1234567890",
        "cart": [{ "name": "Coffee", "qty": 1, "price": 100, "instructions": "" }],
        "total": 100
    }
                           │
                           ▼
    N8n creates invoice (external system)
                           │
                           ▼
    N8n responds:
    {
        "Success": true,
        "InvoiceUrl": "https://payment-gateway.com/pay/INV456",
        "OrderId": "ORD123",
        "Error": null
    }
                           │
                           ▼
    FirestoreService.SetStateAsync(chatId, "AWAITING_PAYMENT")
                           │
                           ▼
    platform.SendOrderSuccessAsync() sends:
    "✅ Order placed!
    Order ID: ORD123
    Complete your payment here: [Pay Now button]"
                           │
                           ▼
                    USER SEES SUCCESS MESSAGE
                    AND CLICKS PAYMENT LINK
```

---

## Extension Patterns

### **Adding a New Button Action**

**Goal**: Add a "favorite" button that saves items to wishlist

**Steps**:

1. **Update MenuKeyboard** to generate button:
```csharp
// src/Handlers/MenuKeyboard.cs
row.Add(InlineKeyboardButton.WithCallbackData(
    "⭐ Add to Favorites", 
    $"favorite|{item.Name}"
));
```

2. **Extend IMessagingPlatformHandler**:
```csharp
// src/Interfaces/IMessagingPlatformHandler.cs
public interface IMessagingPlatformHandler {
    // ... existing methods ...
    Task SendFavoriteConfirmAsync(long chatId, string itemName);
}
```

3. **Implement in TelegramPlatformHandler**:
```csharp
// src/Services/Platform/TelegramPlatformHandler.cs
public async Task SendFavoriteConfirmAsync(long chatId, string itemName) {
    await _bot.SendMessage(
        chatId,
        $"⭐ {itemName} added to favorites!"
    );
}
```

4. **Add handler method in OrderService**:
```csharp
// src/Services/OrderService.cs
public async Task HandleFavoriteAsync(
    long chatId, string itemName, Session session, 
    IMessagingPlatformHandler platform) {
    
    // Save to Firestore favorites
    await _firestore.AddToFavoritesAsync(chatId, itemName);
    
    // Show confirmation
    await platform.SendFavoriteConfirmAsync(chatId, itemName);
}
```

5. **Add case in CallbackQueryHandler**:
```csharp
// src/Handlers/CallbackQueryHandler.cs
case "favorite":
    if (parts.Length < 2) break;
    await _orderService.HandleFavoriteAsync(
        chatId, parts[1], session, _platform
    );
    break;
```

6. **Extend FirestoreService**:
```csharp
// src/Services/FirestoreService.cs
public async Task AddToFavoritesAsync(long chatId, string itemName) {
    var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());
    await docRef.UpdateAsync("favorites", 
        FieldValue.ArrayUnion(itemName));
}
```

---

### **Adding a New Command**

**Goal**: Add `/profile` command to show user info

**Steps**:

1. Add handler in CommandHandler:
```csharp
// src/Handlers/CommandHandler.cs
case "/profile":
    await HandleProfileAsync(message, session);
    break;

private async Task HandleProfileAsync(Message message, Session session) {
    var profileText = $"👤 Your Profile:\n\n" +
        $"Name: {session.CustomerName ?? "Not set"}\n" +
        $"Phone: {session.CustomerPhone ?? "Not set"}\n" +
        $"Orders: {session.OrderId ?? "None"}";
    
    await _platform.SendMessageAsync(message.Chat.Id, profileText);
}
```

**That's it!** No need to extend FirestoreService if you're just displaying existing data.

---

### **Adding a New Checkout Step**

**Goal**: Add delivery address input during checkout

**Steps**:

1. **Extend SessionState**:
```csharp
public static class SessionState {
    // ... existing states ...
    public const string AwaitingAddress = "AWAITING_ADDRESS";
}
```

2. **Add field to Session**:
```csharp
public class Session {
    // ... existing fields ...
    public string DeliveryAddress { get; set; } = string.Empty;
}
```

3. **Update HandleConfirmCheckoutAsync**:
```csharp
public async Task HandleConfirmCheckoutAsync(long chatId, Session session, 
    IMessagingPlatformHandler platform) {
    
    if (string.IsNullOrEmpty(session.CustomerName)) {
        await _firestore.SetStateAsync(chatId, SessionState.AwaitingName);
        await platform.SendMessageAsync(chatId, "Please enter your full name:");
        return;
    }
    
    if (string.IsNullOrEmpty(session.CustomerPhone)) {
        await _firestore.SetStateAsync(chatId, SessionState.AwaitingPhone);
        await platform.SendMessageAsync(chatId, "Please enter your phone number:");
        return;
    }
    
    if (string.IsNullOrEmpty(session.DeliveryAddress)) {
        await _firestore.SetStateAsync(chatId, SessionState.AwaitingAddress);
        await platform.SendMessageAsync(chatId, "Please enter delivery address:");
        return;
    }
    
    // All info collected
    await ProcessCheckoutAsync(chatId, session, platform);
}
```

4. **Handle in StateService**:
```csharp
// Add to StateService.cs
public async Task HandleAwaitingAddressAsync(
    long chatId, string addressInput, Session session, 
    IMessagingPlatformHandler platform) {
    
    if (addressInput.Length < 5) {
        await platform.SendMessageAsync(chatId, "Please enter a valid address:");
        return;
    }
    
    await _firestore.SetDeliveryAddressAsync(chatId, addressInput);
    
    // Refresh session and continue checkout
    var updatedSession = await _firestore.GetOrCreateSessionAsync(chatId);
    await _orderService.ProcessCheckoutAsync(chatId, updatedSession, platform);
}
```

5. **Add to MessageHandler routing**:
```csharp
case SessionState.AwaitingAddress:
    await _stateService.HandleAwaitingAddressAsync(message.Chat.Id, text, session, _platform);
    break;
```

6. **Update N8n payload**:
In `N8nService.CheckoutAsync()`, add delivery address:
```csharp
var payload = new {
    // ... existing fields ...
    delivery_address = session.DeliveryAddress
};
```

---

### **Adding Integration with External Service**

**Goal**: Integrate with inventory/stock management service

**Steps**:

1. **Create new Service**:
```csharp
// src/Services/InventoryService.cs
public class InventoryService {
    private readonly HttpClient _http;
    private readonly string _inventoryApiUrl;
    
    public InventoryService(HttpClient http, string inventoryApiUrl) {
        _http = http;
        _inventoryApiUrl = inventoryApiUrl;
    }
    
    public async Task<int> GetStockAsync(string itemName) {
        var response = await _http.GetAsync($"{_inventoryApiUrl}/stock/{itemName}");
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<StockResult>(json);
        return result?.Qty ?? 0;
    }
    
    public async Task<bool> ReserveStockAsync(string itemName, int qty) {
        var response = await _http.PostAsync(
            $"{_inventoryApiUrl}/reserve",
            new StringContent(JsonSerializer.Serialize(new { itemName, qty }))
        );
        return response.IsSuccessStatusCode;
    }
}

public class StockResult {
    public string ItemName { get; set; }
    public int Qty { get; set; }
}
```

2. **Register in Program.cs**:
```csharp
var inventoryApiUrl = Environment.GetEnvironmentVariable("INVENTORY_API_URL");
var inventoryService = new InventoryService(httpClient, inventoryApiUrl);
builder.Services.AddSingleton(inventoryService);
```

3. **Use in OrderService**:
```csharp
public async Task HandleAddItemAsync(
    long chatId, int messageId, string itemName, decimal price,
    Session session, string callbackId, 
    InventoryService inventory,
    IMessagingPlatformHandler platform) {
    
    // Check stock
    int stock = await inventory.GetStockAsync(itemName);
    if (stock <= 0) {
        await platform.AnswerButtonClickAsync(callbackId, 
            $"❌ {itemName} is out of stock");
        return;
    }
    
    // Add to cart
    var item = new CartItem { Name = itemName, Price = price, Qty = 1 };
    var updatedCart = await _firestore.UpdateCartAsync(chatId, "add", 
        new List<CartItem> { item });
    
    // ... rest of logic
}
```

---

## Common Tasks

### **Task 1: Change Menu Items**

**Current**: Hardcoded in MenuKeyboard.cs
**To Change**: Edit `MenuKeyboard.cs`:

```csharp
private static readonly List<(string Name, decimal Price)> MenuItems = new() {
    ("Coffee", 100),      // Change or add items
    ("Cheesecake", 200),
    ("Latte", 150),
    ("Chicken", 900),
    ("Kebab", 67),
    ("Royal", 45)
};
```

**Better Solution**: Fetch from Firestore
```csharp
// Store menu in Firestore collection: menus/{businessId}/items
// Then in MenuKeyboard:
public static async Task<InlineKeyboardMarkup> BuildAsync(
    FirestoreService firestore, List<CartItem>? cart = null) {
    
    var items = await firestore.GetMenuItemsAsync();
    // ... build keyboard from items
}
```

---

### **Task 2: Change Prices or Apply Discount**

**Location**: `CartFormatter.cs` has `FormatSummary()` method

```csharp
public static string FormatSummary(List<CartItem> cart) {
    if (cart.Count == 0) return "🛒 Your cart is empty.";
    
    var total = cart.Sum(i => i.Price * i.Qty);
    var discount = total > 500m ? total * 0.1m : 0m;  // 10% discount over ₱500
    var subtotal = total - discount;
    
    return $"Subtotal: ₱{total}\n" +
           $"Discount: -₱{discount}\n" +
           $"Total: ₱{subtotal}";
}
```

---

### **Task 3: Add Custom Item Instructions**

Currently supported - already in CartItem model.

When user adds item, let them specify instructions:

```csharp
// In TelegramPlatformHandler
public async Task SendItemDetailsAsync(long chatId, string itemName, decimal price) {
    var keyboard = new InlineKeyboardMarkup(new[] {
        new[] {
            InlineKeyboardButton.WithCallbackData("Add - No Special", $"add|{itemName}|{price}"),
            InlineKeyboardButton.WithCallbackData("Add - Extra Hot", $"add|{itemName}|{price}|extra-hot")
        }
    });
    
    await _bot.SendMessage(chatId, $"{itemName} - ₱{price}", replyMarkup: keyboard);
}
```

---

### **Task 4: Implement Order History**

```csharp
// src/Services/FirestoreService.cs
public async Task<List<Order>> GetOrderHistoryAsync(long chatId) {
    var orders = await _db
        .Collection("orders")
        .WhereEqualTo("chat_id", chatId)
        .OrderByDescending("created_at")
        .Limit(10)
        .GetSnapshotAsync();
    
    return orders.Documents
        .Select(doc => new Order {
            OrderId = doc.Id,
            Total = Convert.ToDecimal(doc.GetValue<double>("total")),
            CreatedAt = doc.GetValue<Timestamp>("created_at").ToDateTime()
        })
        .ToList();
}

// Add new command handler
case "/orders":
    var orders = await _firestore.GetOrderHistoryAsync(message.Chat.Id);
    var text = "📋 Your Orders:\n\n" + 
        string.Join("\n", orders.Select(o => 
            $"• {o.OrderId} - ₱{o.Total} ({o.CreatedAt:MM/dd})"));
    await _platform.SendMessageAsync(message.Chat.Id, text);
    break;
```

---

## Troubleshooting

### **Problem: Callbacks arriving as "Unknown"**
**Cause**: JSON deserialization issue  
**Solution**: Ensure Program.cs has JSON configuration:
```csharp
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNamingPolicy = 
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});
```

### **Problem: Cart not updating across different menu instances**
**Cause**: Session not refreshing from Firestore  
**Solution**: Always call `GetOrCreateSessionAsync(chatId)` before using session in handlers

### **Problem: Checkout webhook fails with "Unable to create converter for Decimal"**
**Cause**: Firestore can't serialize `decimal` type  
**Solution**: `FirestoreService` already handles this - converts decimal ↔ double

### **Problem: State transitions not persisting**
**Cause**: Forgot to call `SetStateAsync()`  
**Solution**: Always update Firestore state when changing session state

### **Problem: Buttons not responding**
**Cause**: CallbackQueryHandler missing case for action  
**Solution**: Add case in switch statement and implement handler method

---

## Best Practices

### **1. Always Use Atomic Transactions for Cart Updates**
```csharp
// ✓ GOOD - Atomic, prevents race conditions
await _db.RunTransactionAsync(async transaction => {
    var cart = await GetCart(transaction);
    cart = AddItems(cart, items);
    transaction.Update(docRef, "cart", SerializeCart(cart));
});

// ✗ BAD - Race condition if updated simultaneously
var cart = await GetCart();
cart.Add(newItem);
await SaveCart(cart);
```

### **2. Keep Handlers Thin, Move Logic to Services**
```csharp
// ✓ GOOD
public class CallbackQueryHandler {
    public async Task HandleAsync(CallbackQuery c, Session s) {
        // Just dispatch
        await _orderService.HandleAddItemAsync(...);
    }
}

// ✗ BAD - Business logic in handler
public class CallbackQueryHandler {
    public async Task HandleAsync(CallbackQuery c, Session s) {
        var item = new CartItem { ... };
        var updated = await _firestore.UpdateCartAsync(...);
        // ... lots of logic ...
    }
}
```

### **3. Use IMessagingPlatformHandler Not ITelegramBotClient**
```csharp
// ✓ GOOD - Platform agnostic
public async Task HandleAsync(IMessagingPlatformHandler platform) {
    await platform.SendMessageAsync(chatId, text);
}

// ✗ BAD - Tightly coupled to Telegram
public async Task HandleAsync(ITelegramBotClient bot) {
    await bot.SendMessage(chatId, text);
}
```

### **4. Validate Input Early**
```csharp
// ✓ GOOD
public async Task HandleNameAsync(string input, IMessagingPlatformHandler platform) {
    if (input.Length < 2) {
        await platform.SendMessageAsync(chatId, "Name too short");
        return;
    }
    // ... proceed
}

// ✗ BAD
public async Task HandleNameAsync(string input) {
    await SaveNameAsync(input);  // Might fail!
}
```

### **5. Always Refresh Session from Firestore Before Using**
```csharp
// ✓ GOOD
var session = await _firestore.GetOrCreateSessionAsync(chatId);
// Use fresh session with latest data

// ✗ BAD
var session = session;  // Using old session object
```

### **6. Log Important Operations**
```csharp
// ✓ GOOD
try {
    var result = await _n8n.CheckoutAsync(chatId, session);
    if (result.Success) {
        Console.WriteLine($"Order {result.OrderId} processed");
    } else {
        Console.WriteLine($"Checkout failed: {result.Error}");
    }
} catch (Exception ex) {
    Console.WriteLine($"Error: {ex.Message}");
}
```

### **7. Use Consistent Naming**
- Files: PascalCase (`OrderService.cs`)
- Classes: PascalCase (`CallbackQueryHandler`)
- Methods: PascalCase (`HandleAddItemAsync`)
- LocalVariables: camelCase (`cartItem`, `updatedCart`)
- Constants: UpperCase (`BROWSING`, `AwaitingName`)

---

## Summary

### Quick Reference

| When You Need To... | File | Method |
|---|---|---|
| Add a button | `MenuKeyboard.cs` | Update MenuItems or Build() |
| Handle button click | `CallbackQueryHandler.cs` | Add case in HandleAsync() |
| Add a command | `CommandHandler.cs` | Add case in HandleAsync() |
| Store user data | `FirestoreService.cs` | New method |
| Call external service | Create `*Service.cs` | Register in Program.cs |
| Change message format | `CartFormatter.cs` | Update format methods |
| Add platform feature | Extend `IMessagingPlatformHandler` | Implement in TelegramPlatformHandler |
| Add checkout step | `StateService.cs` | New HandleAwaitingXAsync() |
| Schedule task | Middleware in `Program.cs` | Add service |
| Add validation | Any Service | Check before state change |

### Key Files at a Glance

```
src/
├── Program.cs              ← Entry point, DI setup
├── Handlers/
│   ├── CallbackQueryHandler.cs   ← Button clicks
│   ├── MessageHandler.cs         ← Text messages
│   └── CommandHandler.cs         ← /commands
├── Services/
│   ├── FirestoreService.cs       ← Database
│   ├── OrderService.cs           ← Cart logic
│   ├── StateService.cs           ← Checkout flow
│   ├── N8nService.cs             ← External webhook
│   └── Platform/
│       ├── TelegramPlatformHandler.cs   ← Telegram impl
│       └── ViberPlatformHandler.cs.template (for Viber)
├── Interfaces/
│   └── IMessagingPlatformHandler.cs   ← Platform contract
└── Models/
    ├── Session.cs          ← User state
    ├── CartItem.cs         ← Cart item
    └── MenuKeyboard.cs     ← Menu UI
```

This architecture makes your bot **easy to modify**, **easy to extend**, and **ready to port to other platforms**. Happy coding! 🚀
