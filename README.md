# Telegram Order Bot

C# Telegram bot using Telegram.Bot + Firebase Firestore + n8n webhooks.

## Project structure

```
src/
├── Handlers/
│   ├── CommandHandler.cs       — /menu, /chat, /cart, /cancel, /help
│   ├── CallbackQueryHandler.cs — inline keyboard button taps
│   ├── MessageHandler.cs       — text messages + state routing
│   └── MenuKeyboard.cs         — keyboard builders + cart formatter
├── Models/
│   ├── Session.cs              — session model + state constants
│   └── CartItem.cs             — cart item model
├── Services/
│   ├── AIAgentService.cs       — AI conversational agent integration via n8n
│   ├── FirestoreService.cs     — all Firebase reads/writes
│   ├── MenuService.cs          — dynamic menu fetching via n8n (Google Sheets)
│   ├── N8nService.cs           — n8n checkout webhook calls
│   ├── OrderService.cs         — business logic for cart combinations
│   └── StateService.cs         — user flow and checkout state
├── Platform/
│   └── TelegramPlatformHandler.cs — Telegram Bot specific implementations
└── Program.cs                  — app entry point + webhook setup
```

## Local setup

### 1. Prerequisites

- .NET 8 SDK
- Firebase project with Firestore enabled
- Telegram bot token from BotFather

### 2. Clone and install

```bash
git clone your-repo
cd TelegramBot
dotnet restore
```

### 3. Configure environment

```bash
cp .env.example .env
```

Fill in `.env` with your values.

### 4. Firebase credentials

- Go to Firebase Console → Project Settings → Service Accounts
- Generate new private key → download JSON
- Save as `firebase-credentials.json` in project root
- Make sure `FIREBASE_PROJECT_ID` is correctly set in `.env` matching the project. Also, set `GOOGLE_APPLICATION_CREDENTIALS=./firebase-credentials.json` depending on your environment.

### 5. Config variables

Configure the webhooks you will need (make sure `WEBHOOK_URL` uses https):

- `WEBHOOK_URL=https://your-ngrok-or-railway-url`
- `N8N_CHECKOUT_WEBHOOK_URL=https://your-n8n/webhook/checkout`
- `N8N_MENUS_WEBHOOK_URL=https://your-n8n/webhook/menus`
- `N8N_AI_INTEGRATION_URL=https://your-n8n/webhook/ai`

### 6. Run locally

```bash
dotnet run --project src
```

For local webhook testing, use [ngrok](https://ngrok.com):

```bash
ngrok http 5000
```

Set `WEBHOOK_URL=https://your-ngrok-url.ngrok.io` in `.env`

## Railway deployment

### 1. Push to GitHub

```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin your-github-repo
git push
```

### 2. Deploy on Railway

- Go to railway.app → New Project → Deploy from GitHub
- Select your repo
- Add environment variables (same as .env but without quotes)
- Railway auto-detects the Dockerfile and deploys

### 3. Set WEBHOOK_URL

After Railway gives you a deployment URL:

- Add `WEBHOOK_URL=https://your-app.railway.app` in Railway environment variables
- Redeploy

## Adding menu items

The bot uses dynamic menus supplied by an external webhook, managed by `MenuService.cs`.

1. In your `N8N_MENUS_WEBHOOK_URL`, create an n8n workflow that fetches items from Google Sheets.
2. The endpoint should return a JSON array containing `items`: `[{ "name": "Coffee", "price": 100, "availability": "AVAILABLE" }]`.
3. If the webhook fails, the bot seamlessly falls back to a hardcoded menu in `MenuService.cs`.

## n8n integration

The bot calls n8n for multiple purposes:

1. **Menus (`N8N_MENUS_WEBHOOK_URL`)**: Provides the current product inventory and pricing (fetches from Google Sheets).
2. **AI Chat (`N8N_AI_INTEGRATION_URL`)**: Provides an AI assistant integration which is activated via the `/chat` command. Retrieves natural language context of the user cart and responds to questions.
3. **Checkout (`N8N_CHECKOUT_WEBHOOK_URL`)**: Complete the order. The webhook receives:
   - `{ chat_id, customer_name, customer_phone, cart, total }`
   - Should return `{ success, invoice_url, order_id }`
4. **Post Payment**: Once payment is completed, the payment gateway calls `WEBHOOK_URL/payment-confirmed` (custom webhook configured in `Program.cs`) to set the bot state to `COMPLETED`.
