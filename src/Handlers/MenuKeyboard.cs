using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Handlers;

public static class MenuKeyboard
{
    public static async Task<InlineKeyboardMarkup> BuildAsync(
        MenuService menuService,
        List<CartItem>? cart = null)
    {
        var menuItems = await menuService.GetMenuItemsAsync();
        var rows = new List<List<InlineKeyboardButton>>();

        foreach (var item in menuItems)
        {
            var qty = 0;
            if (cart != null)
            {
                var found = cart.FirstOrDefault(c => c.Name == item.Name);
                if (found != null)
                {
                    qty = found.Qty;
                }
            }

            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{item.Name} ({qty})", $"select_item|{item.Name}|{item.Price}")
            };

            rows.Add(row);
        }

        // Action buttons
        rows.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🛒 View Cart", "view_cart"),
            InlineKeyboardButton.WithCallbackData("✅ Checkout", "checkout")
        });

        return new InlineKeyboardMarkup(rows);
    }
}

public static class CartKeyboard
{
    public static InlineKeyboardMarkup Build()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗑️ Clear Cart", "clear_cart"),
                InlineKeyboardButton.WithCallbackData("✅ Checkout", "checkout")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Back to Menu", "back_to_menu")
            }
        });
    }
}

public static class CheckoutKeyboard
{
    public static InlineKeyboardMarkup Build()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Yes, proceed", "confirm_checkout"),
                InlineKeyboardButton.WithCallbackData("❌ Cancel", "clear_cart")
            }
        });
    }
}

public static class CartFormatter
{
    public static string Format(List<CartItem> cart)
    {
        if (cart.Count == 0)
        {
            return "🛒 Your cart is empty.";
        }

        var cartLines = new List<string>();

        foreach (var item in cart)
        {
            var itemTotal = item.Price * item.Qty;
            var note = "";

            if (!string.IsNullOrEmpty(item.Instructions))
            {
                note = $" <i>({item.Instructions})</i>";
            }

            var line = $"• {item.Name} x{item.Qty} — ₱{itemTotal}{note}";
            cartLines.Add(line);
        }

        var total = 0m;
        foreach (var item in cart)
        {
            total += item.Price * item.Qty;
        }

        var formattedCart = string.Join("\n", cartLines);
        return $"🛒 <b>Your Cart:</b>\n\n{formattedCart}\n\n<b>Total: ₱{total}</b>";
    }

    public static string FormatSummary(List<CartItem> cart)
    {
        if (cart.Count == 0)
        {
            return "Cart is empty";
        }

        var itemCount = 0;
        var total = 0m;

        foreach (var item in cart)
        {
            itemCount += item.Qty;
            total += item.Price * item.Qty;
        }

        return $"🛒 {itemCount} item(s) — ₱{total}";
    }
}
