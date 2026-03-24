namespace TelegramBot.Models;

public class CartItem
{
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal Price { get; set; }
    public string Instructions { get; set; } = string.Empty;
}
