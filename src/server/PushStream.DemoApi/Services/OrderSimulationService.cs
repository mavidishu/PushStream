using System.Threading.Channels;
using PushStream.Core.Abstractions;
using PushStream.DemoApi.Models;

namespace PushStream.DemoApi.Services;

/// <summary>
/// Background service that simulates realistic order progression.
/// Demonstrates PushStream's real-time event publishing for order tracking.
/// </summary>
public class OrderSimulationService : BackgroundService
{
    private readonly Channel<Order> _orderChannel;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrderSimulationService> _logger;
    private readonly Random _random = new();

    // Simulated restaurants
    private static readonly Dictionary<string, RestaurantInfo> Restaurants = new()
    {
        ["burger-palace"] = new("Burger Palace", "American", 4.7, "/images/burger.svg"),
        ["pizza-heaven"] = new("Pizza Heaven", "Italian", 4.8, "/images/pizza.svg"),
        ["sushi-master"] = new("Sushi Master", "Japanese", 4.9, "/images/sushi.svg"),
        ["taco-fiesta"] = new("Taco Fiesta", "Mexican", 4.6, "/images/taco.svg")
    };

    // Simulated drivers
    private static readonly DriverInfo[] Drivers =
    [
        new("Alex M.", 4.9, "Honda Civic", "ABC-1234", "/images/driver1.svg"),
        new("Sarah K.", 4.8, "Toyota Camry", "XYZ-5678", "/images/driver2.svg"),
        new("Mike R.", 4.7, "Ford Focus", "DEF-9012", "/images/driver3.svg"),
        new("Emma L.", 4.9, "Hyundai Elantra", "GHI-3456", "/images/driver4.svg")
    ];

    public OrderSimulationService(
        IEventPublisher eventPublisher,
        ILogger<OrderSimulationService> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;

        _orderChannel = Channel.CreateUnbounded<Order>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Get restaurant info by ID.
    /// </summary>
    public static RestaurantInfo? GetRestaurant(string restaurantId)
    {
        return Restaurants.GetValueOrDefault(restaurantId);
    }

    /// <summary>
    /// Get all available restaurants.
    /// </summary>
    public static IEnumerable<(string Id, RestaurantInfo Info)> GetAllRestaurants()
    {
        return Restaurants.Select(r => (r.Key, r.Value));
    }

    /// <summary>
    /// Enqueue a new order for processing.
    /// </summary>
    public async Task<Order> EnqueueOrderAsync(
        string restaurantId,
        List<OrderItemRequest> items,
        string deliveryAddress,
        string? clientId)
    {
        var restaurant = Restaurants.GetValueOrDefault(restaurantId)
            ?? Restaurants["burger-palace"]; // Default fallback

        var estimatedMinutes = _random.Next(12, 20);
        var order = new Order(
            OrderId: GenerateOrderId(),
            RestaurantId: restaurantId,
            Restaurant: restaurant,
            Items: items,
            DeliveryAddress: deliveryAddress,
            ClientId: clientId,
            CreatedAt: DateTime.UtcNow,
            EstimatedDelivery: DateTime.UtcNow.AddMinutes(estimatedMinutes)
        );

        await _orderChannel.Writer.WriteAsync(order);
        _logger.LogInformation("Order {OrderId} enqueued from {Restaurant}", order.OrderId, restaurant.Name);

        return order;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderSimulationService started");

        await foreach (var order in _orderChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessOrderAsync(order, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}", order.OrderId);
            }
        }

        _logger.LogInformation("OrderSimulationService stopped");
    }

    private async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var driver = Drivers[_random.Next(Drivers.Length)];
        _logger.LogInformation("Processing order {OrderId} from {Restaurant}", order.OrderId, order.Restaurant.Name);

        // Stage 1: Confirmed (immediate)
        await PublishStatusAsync(order, OrderStage.Confirmed,
            "Order confirmed! We're sending it to the restaurant.",
            eta: order.EstimatedDelivery,
            progress: 0);

        await Task.Delay(TimeSpan.FromSeconds(_random.Next(2, 4)), cancellationToken);

        // Stage 2: Preparing (with progress updates)
        await PublishStatusAsync(order, OrderStage.Preparing,
            $"{order.Restaurant.Name} is preparing your order",
            eta: order.EstimatedDelivery,
            progress: 15);

        // Simulate preparation with progress updates
        for (int progress = 25; progress <= 45; progress += 10)
        {
            await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 3)), cancellationToken);
            var message = progress switch
            {
                25 => "Chef is working on your order...",
                35 => "Almost ready at the kitchen...",
                45 => "Final touches being added...",
                _ => "Preparing your delicious meal..."
            };
            await PublishStatusAsync(order, OrderStage.Preparing, message,
                eta: order.EstimatedDelivery, progress: progress);
        }

        await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 2)), cancellationToken);

        // Stage 3: Ready for Pickup
        await PublishStatusAsync(order, OrderStage.ReadyForPickup,
            $"{driver.Name} is picking up your order",
            eta: order.EstimatedDelivery,
            progress: 50,
            driver: driver);

        await Task.Delay(TimeSpan.FromSeconds(_random.Next(2, 4)), cancellationToken);

        // Stage 4: Out for Delivery (with progress updates)
        await PublishStatusAsync(order, OrderStage.OutForDelivery,
            $"{driver.Name} is on the way!",
            eta: order.EstimatedDelivery,
            progress: 60,
            driver: driver);

        // Simulate delivery progress
        for (int progress = 70; progress <= 90; progress += 10)
        {
            await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 3)), cancellationToken);
            var minutesAway = (100 - progress) / 10;
            var message = progress switch
            {
                70 => $"{driver.Name} is about {minutesAway + 2} minutes away",
                80 => $"Almost there! {minutesAway + 1} minutes away",
                90 => "Arriving now!",
                _ => $"{driver.Name} is on the way..."
            };
            await PublishStatusAsync(order, OrderStage.OutForDelivery, message,
                eta: DateTime.UtcNow.AddMinutes(minutesAway),
                progress: progress,
                driver: driver);
        }

        await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 2)), cancellationToken);

        // Stage 5: Delivered
        await PublishStatusAsync(order, OrderStage.Delivered,
            "Your order has been delivered. Enjoy your meal! 🎉",
            eta: null,
            progress: 100,
            driver: driver);

        _logger.LogInformation("Order {OrderId} delivered successfully", order.OrderId);
    }

    private async Task PublishStatusAsync(
        Order order,
        OrderStage stage,
        string message,
        DateTime? eta,
        int progress,
        DriverInfo? driver = null)
    {
        var statusEvent = new OrderStatusEvent(
            OrderId: order.OrderId,
            Stage: stage,
            StageName: GetStageName(stage),
            Message: message,
            Eta: eta,
            Progress: progress,
            Driver: driver,
            Restaurant: order.Restaurant,
            Items: stage == OrderStage.Confirmed ? order.Items : null
        );

        if (!string.IsNullOrEmpty(order.ClientId))
        {
            await _eventPublisher.PublishToAsync(order.ClientId, "order.updated", statusEvent);
        }
        else
        {
            await _eventPublisher.PublishAsync("order.updated", statusEvent);
        }

        _logger.LogDebug("Order {OrderId}: {Stage} - {Message}", order.OrderId, stage, message);
    }

    private static string GetStageName(OrderStage stage) => stage switch
    {
        OrderStage.Confirmed => "Order Confirmed",
        OrderStage.Preparing => "Preparing",
        OrderStage.ReadyForPickup => "Ready for Pickup",
        OrderStage.OutForDelivery => "Out for Delivery",
        OrderStage.Delivered => "Delivered",
        _ => stage.ToString()
    };

    private string GenerateOrderId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var id = new char[4];
        for (int i = 0; i < 4; i++)
        {
            id[i] = chars[_random.Next(chars.Length)];
        }
        return $"ORD-{new string(id)}";
    }
}