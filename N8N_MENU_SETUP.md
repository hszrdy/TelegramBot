# N8n Menu Webhook Setup Guide

## Overview
The MenuService fetches menu items dynamically from an N8n webhook instead of hardcoding them. This allows you to manage menu items via Google Sheets without redeploying the application.

## MenuService Integration Points

### 1. Service Location
- **File**: [src/Services/MenuService.cs](src/Services/MenuService.cs)
- **Purpose**: Fetches menu items from N8n webhook with fallback to hardcoded menu
- **Environment Variable**: `N8N_MENUS_WEBHOOK_URL` (required)

### 2. Expected N8n Response Format
The N8n webhook should return JSON matching this structure:

```json
{
  "items": [
    {
      "name": "Espresso",
      "price": 120
    },
    {
      "name": "Cappuccino",
      "price": 150
    },
    {
      "name": "Iced Coffee",
      "price": 140
    }
  ]
}
```

**Field Details:**
- `items`: Array of menu items (required)
- `name`: String, display name of the item (required)
- `price`: Number (decimal), price in PHP (required)

### 3. Where MenuService is Used
- **TelegramPlatformHandler**: Injects MenuService in constructor
  ```csharp
  public TelegramPlatformHandler(ITelegramBotClient bot, MenuService menuService)
  ```
- **MenuKeyboard**: Calls `await menuService.GetMenuItemsAsync()` to build dynamic buttons
  ```csharp
  var menuItems = await menuService.GetMenuItemsAsync();
  ```

## Setting Up the N8n Workflow

### Step 1: Create HTTP Trigger
1. In N8n, create a new workflow
2. Add an "HTTP Request" trigger node
3. Set it to listen for POST requests
4. Copy the webhook URL (you'll need this for the environment variable)

### Step 2: Add Google Sheets Integration
1. Add a "Google Sheets" node (or similar data source)
2. Configure it to query your menu items spreadsheet
3. Expected spreadsheet columns:
   - Column A: Item Name (e.g., "Espresso")
   - Column B: Price (e.g., 120)

### Step 3: Transform the Response
1. Add a "Function" or "Set" node to format the response
2. Transform data into the expected JSON structure:

**Example JavaScript (if using Function node):**
```javascript
return {
  items: $('Google Sheets').toArray().map(row => ({
    name: row['Item Name'],
    price: parseFloat(row['Price'])
  }))
}
```

### Step 4: Return HTTP Response
1. Add an "HTTP Response" node
2. Set it to return the transformed data as JSON

### Step 5: Deploy and Get Webhook URL
1. Click "Activate" to deploy the workflow
2. Copy the webhook URL
3. Example format: `https://your-instance.n8n.cloud/webhook/abc123def456`

## Configuring the Application

### Step 1: Set Environment Variable
Add to your `.env` file:
```bash
N8N_MENUS_WEBHOOK_URL=https://your-instance.n8n.cloud/webhook/abc123def456
```

### Step 2: Verify Program.cs Registration
The MenuService is already registered in [Program.cs](src/Program.cs):
```csharp
builder.Services.AddScoped<MenuService>();
```

And passed to TelegramPlatformHandler:
```csharp
var menuService = app.Services.GetRequiredService<MenuService>();
var platformHandler = new TelegramPlatformHandler(botClient, menuService);
```

### Step 3: Test the Integration
1. Build and run the application
2. Send `/menu` to the Telegram bot
3. Check the console for:
   - `"Fetched X menu items from N8n"` (success)
   - `"Using fallback menu"` (N8n failed, using hardcoded items)

## Fallback Behavior

If N8n is unreachable or returns invalid JSON, MenuService automatically uses a hardcoded fallback menu:

**Hardcoded Fallback Items:**
- Coffee - ₱150
- Iced Tea - ₱100
- Pastry - ₱80

This ensures the bot remains functional even if N8n is down.

**When Fallback is Used:**
- Network error when calling N8n
- HTTP status code is not 2xx
- Invalid JSON response
- Missing "items" array in response
- Exception during parsing

## Troubleshooting

### Issue: Menu shows fallback items instead of N8n items
**Causes:**
- Environment variable not set or incorrect
- N8n webhook URL is wrong
- N8n workflow not deployed/activated
- N8n response format doesn't match expected schema

**Solution:**
1. Verify `N8N_MENUS_WEBHOOK_URL` in `.env`
2. Test the webhook manually with curl:
   ```bash
   curl https://your-webhook-url
   ```
3. Check N8n logs for errors
4. Verify response contains `"items"` array

### Issue: "404 Not Found" error
**Cause:** N8n webhook URL is incorrect or workflow is deactivated

**Solution:**
1. In N8n, click "Copy Webhook URL"
2. Paste exactly into `.env` file
3. Click "Activate" workflow button

### Issue: Invalid JSON error in console
**Cause:** N8n response doesn't match expected format

**Solution:**
1. Check N8n response format matches the structure in "Expected N8n Response Format"
2. Verify all items have `name` (string) and `price` (number)
3. Use a JSON validator to check response is valid

## Performance Considerations

- MenuService caches nothing - every menu fetch calls N8n
- Each `/menu` command = 1 HTTP call to N8n
- Typical response time: 200-500ms
- Consider caching if performance becomes an issue

**Future Optimization:**
```csharp
// Could add caching to MenuService
private DateTime _lastMenuFetch = DateTime.MinValue;
private List<(string Name, decimal Price)> _cachedMenu;
private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
```

## Security Notes

- Store `N8N_MENUS_WEBHOOK_URL` in environment variables, never in code
- If N8n is public-facing, consider adding authentication to the webhook
- Validate price and name fields for suspicious values
- Consider rate limiting if N8n calls become expensive

## Testing N8n Workflow

### Using curl:
```bash
curl -X POST https://your-webhook-url \
  -H "Content-Type: application/json"
```

### Expected response:
```json
{
  "items": [
    {"name": "Espresso", "price": 120},
    {"name": "Cappuccino", "price": 150}
  ]
}
```

### Using Postman:
1. Create POST request to webhook URL
2. Verify response status is 200
3. Check response body has "items" array

## Related Files

- [MenuService.cs](src/Services/MenuService.cs) - Fetches from N8n with fallback
- [MenuKeyboard.cs](src/Handlers/MenuKeyboard.cs) - Builds buttons from menu items  
- [TelegramPlatformHandler.cs](src/Services/Platform/TelegramPlatformHandler.cs) - Uses MenuService
- [Program.cs](src/Program.cs) - Registers MenuService in DI container
- [.env.example](.env.example) - Example environment variables

## Next Steps

1. ✅ MenuService code is complete
2. ⏳ Create N8n workflow with Google Sheets trigger
3. ⏳ Deploy N8n workflow and copy webhook URL
4. ⏳ Add webhook URL to `.env` file
5. ⏳ Test `/menu` command with Telegram bot
6. ⏳ Verify menu items load from N8n (or fallback if N8n down)
