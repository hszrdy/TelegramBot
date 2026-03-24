using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Models;

namespace TelegramBot.Handlers;

public static class MenuKeyboard
{
    // Hardcoded menu — replace with Firestore/Sheets fetch if needed
    private static readonly List<(string Name, decimal Price)> MenuItems = new()
    {
        ("Coffee",    100),
        ("Cheesecake",200),
        ("Latte",     150),
        ("Chicken",   900),
        ("Kebab",      67),
        ("Royal",      45),
    };

    public static InlineKeyboardMarkup Build(List<CartItem>? cart = null)
    {
        var rows = new List<List<InlineKeyboardButton>>();

        // Build menu item buttons (2 per row)
        for (int i = 0; i < MenuItems.Count; i += 2)
        {
            var row = new List<InlineKeyboardButton>();

            var item1 = MenuItems[i];
            var qty1 = cart?.FirstOrDefault(c => c.Name == item1.Name)?.Qty ?? 0;
            var label1 = qty1 > 0 ? $"{item1.Name} ₱{item1.Price} ({qty1})" : $"{item1.Name} — ₱{item1.Price}";
            row.Add(InlineKeyboardButton.WithCallbackData(label1, $"add|{item1.Name}|{item1.Price}"));

            if (i + 1 < MenuItems.Count)
            {
                var item2 = MenuItems[i + 1];
                var qty2 = cart?.FirstOrDefault(c => c.Name == item2.Name)?.Qty ?? 0;
                var label2 = qty2 > 0 ? $"{item2.Name} ₱{item2.Price} ({qty2})" : $"{item2.Name} — ₱{item2.Price}";
                row.Add(InlineKeyboardButton.WithCallbackData(label2, $"add|{item2.Name}|{item2.Price}"));
            }

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
        if (cart.Count == 0) return "🛒 Your cart is empty.";

        var lines = cart.Select(i =>
        {
            var note = string.IsNullOrEmpty(i.Instructions) ? "" : $" <i>({i.Instructions})</i>";
            return $"• {i.Name} x{i.Qty} — ₱{i.Price * i.Qty}{note}";
        });

        var total = cart.Sum(i => i.Price * i.Qty);
        return $"🛒 <b>Your Cart:</b>\n\n{string.Join("\n", lines)}\n\n<b>Total: ₱{total}</b>";
    }

    public static string FormatSummary(List<CartItem> cart)
    {
        if (cart.Count == 0) return "Cart is empty";
        var total = cart.Sum(i => i.Price * i.Qty);
        var count = cart.Sum(i => i.Qty);
        return $"🛒 {count} item(s) — ₱{total}";
    }
}
