using Google.Cloud.Firestore;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;
    private const string SessionsCollection = "sessions";

    public FirestoreService(string projectId)
    {
        _db = FirestoreDb.Create(projectId);
    }

    // Get session by chat_id, create one if it doesn't exist
    public async Task<Session> GetOrCreateSessionAsync(long chatId)
    {
        var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());
        var snapshot = await docRef.GetSnapshotAsync();

        if (snapshot.Exists)
        {
            return new Session
            {
                ChatId = chatId,
                State = snapshot.GetValue<string>("state"),
                Cart = DeserializeCart(snapshot),
                CustomerName = snapshot.ContainsField("customer_name") ? snapshot.GetValue<string>("customer_name") : string.Empty,
                CustomerPhone = snapshot.ContainsField("customer_phone") ? snapshot.GetValue<string>("customer_phone") : string.Empty,
                OrderId = snapshot.ContainsField("order_id") ? snapshot.GetValue<string>("order_id") : string.Empty,
                XenditInvoiceId = snapshot.ContainsField("xendit_invoice_id") ? snapshot.GetValue<string>("xendit_invoice_id") : string.Empty,
            };
        }

        // New user — create a fresh session
        var newSession = new Session { ChatId = chatId };
        await docRef.SetAsync(new Dictionary<string, object>
        {
            { "state", newSession.State },
            { "cart", new List<object>() },
            { "customer_name", string.Empty },
            { "customer_phone", string.Empty },
            { "order_id", string.Empty },
            { "xendit_invoice_id", string.Empty }
        });

        return newSession;
    }

    // Update session state
    public async Task SetStateAsync(long chatId, string state)
    {
        var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());
        await docRef.UpdateAsync("state", state);
    }

    // Update cart atomically using a transaction (prevents race conditions)
    public async Task<List<CartItem>> UpdateCartAsync(long chatId, string action, List<CartItem> incomingItems)
    {
        var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());

        List<CartItem> updatedCart = new();

        await _db.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef);
            var cart = DeserializeCart(snapshot);

            cart = action switch
            {
                "clear" => new List<CartItem>(),
                "add" => AddItems(cart, incomingItems),
                "remove" => RemoveItems(cart, incomingItems),
                "update" => UpdateItems(cart, incomingItems),
                _ => cart
            };

            transaction.Update(docRef, "cart", SerializeCart(cart));
            updatedCart = cart;
        });

        return updatedCart;
    }

    // Update customer info
    public async Task SetCustomerInfoAsync(long chatId, string name, string phone)
    {
        var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());
        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            { "customer_name", name },
            { "customer_phone", phone }
        });
    }

    // Reset session after cancel
    public async Task ResetSessionAsync(long chatId)
    {
        var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());
        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            { "state", SessionState.Browsing },
            { "cart", new List<object>() },
            { "order_id", string.Empty },
            { "xendit_invoice_id", string.Empty }
        });
    }

    // ── Cart helpers ──────────────────────────────────────────────────────

    private static List<CartItem> AddItems(List<CartItem> cart, List<CartItem> incoming)
    {
        foreach (var item in incoming)
        {
            var existing = cart.FirstOrDefault(
                i => i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Qty += item.Qty;
                if (!string.IsNullOrEmpty(item.Instructions))
                    existing.Instructions = item.Instructions;
            }
            else
            {
                cart.Add(item);
            }
        }
        return cart;
    }

    private static List<CartItem> RemoveItems(List<CartItem> cart, List<CartItem> incoming)
    {
        return cart
            .Where(i => !incoming.Any(r =>
                r.Name.Equals(i.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<CartItem> UpdateItems(List<CartItem> cart, List<CartItem> incoming)
    {
        foreach (var item in incoming)
        {
            var existing = cart.FirstOrDefault(
                i => i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Qty = item.Qty;
                if (!string.IsNullOrEmpty(item.Instructions))
                    existing.Instructions = item.Instructions;
            }
        }
        return cart;
    }

    private static List<CartItem> DeserializeCart(DocumentSnapshot snapshot)
    {
        if (!snapshot.ContainsField("cart")) return new List<CartItem>();

        var rawCart = snapshot.GetValue<List<Dictionary<string, object>>>("cart");
        return rawCart?.Select(i => new CartItem
        {
            Name = i.TryGetValue("name", out var n) ? n.ToString()! : string.Empty,
            Qty = i.TryGetValue("qty", out var q) ? Convert.ToInt32(q) : 0,
            Price = i.TryGetValue("price", out var p) ? Convert.ToDecimal(Convert.ToDouble(p)) : 0,
            Instructions = i.TryGetValue("instructions", out var ins) ? ins.ToString()! : string.Empty
        }).ToList() ?? new List<CartItem>();
    }

    private static List<Dictionary<string, object>> SerializeCart(List<CartItem> cart)
    {
        return cart.Select(i => new Dictionary<string, object>
        {
            { "name", i.Name },
            { "qty", i.Qty },
            { "price", (double)i.Price },
            { "instructions", i.Instructions }
        }).ToList();
    }

   public async Task SetOrderIdAsync(long chatId, string orderId)
{
    var docRef = _db.Collection(SessionsCollection).Document(chatId.ToString());
    await docRef.SetAsync(new Dictionary<string, object>
    {
        { "order_id", orderId }
    }, SetOptions.MergeAll);
     Console.WriteLine($"[Firestore] SetOrderIdAsync complete");
}
}
