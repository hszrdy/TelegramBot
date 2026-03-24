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
│   ├── FirestoreService.cs     — all Firebase reads/writes
│   └── N8nService.cs          — n8n webhook calls
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
- Make sure `GOOGLE_APPLICATION_CREDENTIALS=./firebase-credentials.json` in `.env`

### 5. Run locally
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

Currently menu items are hardcoded in `MenuKeyboard.cs`. To make them dynamic:
1. Store menu in Firestore `menu` collection
2. Add `GetMenuAsync()` to `FirestoreService`
3. Call it in `MenuKeyboard.Build()` or cache it on startup

## n8n integration

The bot calls n8n only for checkout. Your n8n checkout webhook should:
1. Receive `{ chat_id, customer_name, customer_phone, cart, total }`
2. Create Xendit invoice
3. Return `{ success, invoice_url, order_id }`
