# N8n Webhook Response Format Reference

## Quick Reference Card

**Webhook Response Format:**
```json
{
  "items": [
    {
      "name": "Item Name",
      "price": 150
    }
  ]
}
```

## Expected Schema

| Field | Type | Required | Example | Notes |
|-------|------|----------|---------|-------|
| items | Array | ✅ Yes | `[...]` | Array of menu items |
| items[].name | String | ✅ Yes | `"Espresso"` | Display name, shown on button |
| items[].price | Number | ✅ Yes | `120` | Decimal price in PHP |

## Valid Examples

### Minimal (1 item)
```json
{
  "items": [
    {"name": "Coffee", "price": 150}
  ]
}
```

### Small Menu (3 items)
```json
{
  "items": [
    {"name": "Espresso", "price": 120},
    {"name": "Cappuccino", "price": 150},
    {"name": "Iced Coffee", "price": 140}
  ]
}
```

### Full Menu (from Google Sheets)
```json
{
  "items": [
    {"name": "Espresso", "price": 120},
    {"name": "Double Espresso", "price": 160},
    {"name": "Americano", "price": 100},
    {"name": "Cappuccino", "price": 150},
    {"name": "Latte", "price": 160},
    {"name": "Macchiato", "price": 140},
    {"name": "Iced Coffee", "price": 140},
    {"name": "Iced Latte", "price": 150},
    {"name": "Cold Brew", "price": 180},
    {"name": "Flat White", "price": 170},
    {"name": "Mocha", "price": 170},
    {"name": "Affogato", "price": 150}
  ]
}
```

## Invalid Examples (❌ Won't Work)

### Missing "items" array
```json
{
  "menu": [{"name": "Coffee", "price": 150}]
}
```
❌ Wrong - looking for "items" key

### Price as string
```json
{
  "items": [{"name": "Coffee", "price": "150"}]
}
```
❌ Wrong - price must be number, not string

### Missing price
```json
{
  "items": [{"name": "Coffee"}]
}
```
❌ Wrong - price is required

### Missing name
```json
{
  "items": [{"price": 150}]
}
```
❌ Wrong - name is required

### Items as object instead of array
```json
{
  "items": {
    "coffee": {"price": 150}
  }
}
```
❌ Wrong - items must be array

### Empty items array (edge case)
```json
{
  "items": []
}
```
⚠️ Valid format but will show fallback menu

## N8n Workflow Structure

### Minimal N8n Flow:
1. **HTTP Trigger** (listens for webhook requests)
2. **Google Sheets Node** (queries your menu items)
   - Query formula: Returns rows from sheet
   - Columns: Item Name, Price
3. **Function Node** (transforms to expected format)
   ```javascript
   return {
     items: $('Google Sheets').toArray().map(row => ({
       name: row['Item Name'],
       price: parseFloat(row['Price'])
     }))
   }
   ```
4. **HTTP Response** (returns formatted JSON)

## Testing the Webhook

### Using curl:
```bash
# Test the webhook
curl https://your-n8n-webhook-url/

# Expected output
{
  "items": [
    {"name": "Coffee", "price": 150}
  ]
}
```

### Using Postman:
1. Create a new GET request
2. URL: `https://your-n8n-webhook-url/`
3. Click Send
4. Response status should be 200
5. Response body should match format above

### Using Browser:
```
https://your-n8n-webhook-url/
```
(If webhook is set to GET, you can visit in browser)

## Google Sheets Integration

### Sample Sheet Structure:
| Item Name | Price |
|-----------|-------|
| Espresso | 120 |
| Cappuccino | 150 |
| Iced Coffee | 140 |

### In N8n Google Sheets Node:
- **Spreadsheet**: Your menu spreadsheet
- **Sheet**: Menu items tab
- **Range**: A2:B100 (or appropriate range)
- **Headers**: A1 = "Item Name", B1 = "Price"

## Debugging: How to Tell if Format is Wrong

**Console Log (if N8n fails):**
```
[Error] Error deserializing N8n response: ...
Using fallback menu
```

**This means:**
- MenuService tried to parse response as JSON
- Failed because format doesn't match expected structure
- Fell back to hardcoded menu

**To debug:**
1. Copy the exact URL from N8n
2. Paste in browser or curl
3. Copy the response
4. Paste in JSON validator: [jsonlint.com](https://www.jsonlint.com/)
5. Verify it has `"items"` array with `"name"` and `"price"` fields

## When Fallback Menu Appears

MenuService uses fallback menu when:
1. ❌ N8n webhook URL not set in environment variable
2. ❌ N8n webhook returns HTTP error (4xx, 5xx)
3. ❌ Network timeout (30+ seconds)
4. ❌ Invalid JSON response
5. ❌ Missing "items" array in response
6. ❌ Missing "name" or "price" in items
7. ❌ Exception during JSON deserialization

**Always check:**
1. Is `N8N_MENUS_WEBHOOK_URL` set in `.env`?
2. Does the webhook URL work when tested in curl/browser?
3. Is the JSON response valid?
4. Does response have all required fields?

## Performance Tips

- N8n should respond within 500ms for good UX
- Each menu request hits N8n (no caching in bot)
- If N8n is slow, users might see delay on /menu command
- Google Sheets API query should be optimized (filter rows, not fetch entire sheet)

## Price Formats Supported

| Format | Example | Valid? | Notes |
|--------|---------|--------|-------|
| Integer | `150` | ✅ | Treated as 150.00 |
| Decimal | `150.50` | ✅ | Exact price |
| Two decimals | `150.00` | ✅ | Standard format |
| Scientific | `1.5e2` | ✅ | But don't recommend |
| String | `"150"` | ❌ | Must be number |

## Character Limits

- **Item Name**: ~20 characters max (Telegram button width limit)
- **Price**: Unlimited but recommend < 6 characters

Examples:
- ✅ "Cappuccino" (10 chars)
- ✅ "Double Espresso" (15 chars)
- ❌ "Super Delicious Premium Espresso Blend" (too long, wraps awkwardly)

## Common Errors & Solutions

### Error: "Unable to parse JSON"
**Cause:** Response isn't valid JSON  
**Solution:** Test URL in curl/browser, validate with jsonlint.com

### Error: "No items found"
**Cause:** Response missing "items" array  
**Solution:** Check response has `{"items": [...]}`

### Error: "Network timeout"
**Cause:** N8n workflow is slow or unreachable  
**Solution:** Test N8n workflow independently, check network connectivity

### Menu shows fallback items
**Cause:** See "When Fallback Menu Appears" section  
**Solution:** Test webhook URL, verify JSON format, check environment variable

---

## Quick Checklist Before Deploying

- [ ] N8n webhook URL copied correctly
- [ ] URL added to `.env` as `N8N_MENUS_WEBHOOK_URL`
- [ ] Webhook tested in curl/browser to verify it works
- [ ] Response contains `"items"` array
- [ ] Each item has `"name"` (string) and `"price"` (number)
- [ ] Prices are positive numbers
- [ ] Item names are reasonable length (~20 chars max)
- [ ] N8n workflow is deployed/activated
- [ ] Bot application restarted after env var change

**If everything checks out**: Send `/menu` command to bot - you should see your N8n menu items instead of fallback!

