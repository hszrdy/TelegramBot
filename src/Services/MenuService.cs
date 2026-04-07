using System.Text.Json;
using System.Text.Json.Serialization;

namespace TelegramBot.Services;

/// <summary>
/// Fetches menu items dynamically from N8n workflow.
/// N8n queries Google Sheets and returns menu list.
/// Includes fallback to hardcoded menu if N8n fails.
/// </summary>
public class MenuService
{
    private readonly HttpClient _http;
    private readonly string _menusWebhookUrl;
    private List<(string Name, decimal Price)>? _cachedMenu;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public MenuService(HttpClient http, string menusWebhookUrl)
    {
        _http = http;
        _menusWebhookUrl = menusWebhookUrl;
    }

    /// <summary>
    /// Fetch menu items from N8n (which queries Google Sheets).
    /// Returns hardcoded fallback if N8n fails.
    /// Cached for 5 minutes to improve responsiveness.
    /// </summary>
    public async Task<List<(string Name, decimal Price)>> GetMenuItemsAsync()
    {
        if (_cachedMenu != null && DateTime.UtcNow - _cacheTime < _cacheDuration)
        {
            return _cachedMenu;
        }

        try
        {
            var response = await _http.GetAsync(_menusWebhookUrl);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Menu webhook returned: {response.StatusCode}");
                return GetFallbackMenu();
            }

            var json = await response.Content.ReadAsStringAsync();

            // Use case-insensitive JSON deserialization
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var result = JsonSerializer.Deserialize<MenuResponse>(json, options);

            if (result?.Items == null || result.Items.Count == 0)
            {
                Console.WriteLine("Menu response was empty");
                return GetFallbackMenu();
            }

            // Convert MenuItem objects to tuples
            var menuItems = new List<(string Name, decimal Price)>();
            foreach (var item in result.Items)
            {
                if (item.Availability.ToUpper() == MenuAvailabilityConstants.AVAILABLE.ToString())
                    menuItems.Add((item.Name, item.Price));
            }

            Console.WriteLine($"Fetched {menuItems.Count} menu items from N8n");
            _cachedMenu = menuItems;
            _cacheTime = DateTime.UtcNow;
            return menuItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching menu from N8n: {ex.Message}");
            var fallback = GetFallbackMenu();
            _cachedMenu = fallback;
            _cacheTime = DateTime.UtcNow;
            return fallback;
        }
    }

    /// <summary>
    /// Hardcoded fallback menu if N8n is unavailable.
    /// </summary>
    private List<(string Name, decimal Price)> GetFallbackMenu()
    {
        Console.WriteLine("Using fallback menu");
        var fallback = new List<(string Name, decimal Price)>
        {
            ("Coffee", 100),
            ("Cheesecake", 200),
            ("Latte", 150),
            ("Chicken", 900),
            ("Kebab", 6767),
            ("Royal", 45)
        };
        _cachedMenu = fallback;
        _cacheTime = DateTime.UtcNow;
        return fallback;
    }
}

/// <summary>
/// Represents a menu item from N8n/Google Sheets.
/// </summary>
public class MenuItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }
    [JsonPropertyName("availability")]
    public string Availability { get; set; }
}

/// <summary>
/// Expected JSON response format from N8n.
/// N8n workflow should return items array with name and price.
/// </summary>
public class MenuResponse
{
    [JsonPropertyName("items")]
    public List<MenuItem> Items { get; set; } = new();
}

public enum MenuAvailabilityConstants
{
    AVAILABLE,
    UNAVAILABLE,
    UNKNOWN
}