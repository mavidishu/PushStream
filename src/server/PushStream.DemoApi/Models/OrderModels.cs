namespace PushStream.DemoApi.Models;

// ===== Request/Response DTOs =====

/// <summary>
/// Request to place a new order.
/// </summary>
public record PlaceOrderRequest(
    string RestaurantId,
    List<OrderItemRequest> Items,
    string DeliveryAddress
);

/// <summary>
/// An item in the order request.
/// </summary>
public record OrderItemRequest(
    string Name,
    int Quantity,
    decimal Price
);

/// <summary>
/// Response after placing an order.
/// </summary>
public record PlaceOrderResponse(
    string OrderId,
    string RestaurantName,
    List<OrderItemRequest> Items,
    decimal Total,
    DateTime EstimatedDelivery,
    int EstimatedMinutes
);

// ===== Order Stage Enum =====

/// <summary>
/// The stages an order goes through.
/// </summary>
public enum OrderStage
{
    Confirmed,
    Preparing,
    ReadyForPickup,
    OutForDelivery,
    Delivered
}

// ===== Event DTOs =====

/// <summary>
/// Event payload for order status updates.
/// </summary>
public record OrderStatusEvent(
    string OrderId,
    OrderStage Stage,
    string StageName,
    string Message,
    DateTime? Eta,
    int? Progress,
    DriverInfo? Driver,
    RestaurantInfo? Restaurant,
    List<OrderItemRequest>? Items
);

/// <summary>
/// Driver information for delivery stage.
/// </summary>
public record DriverInfo(
    string Name,
    double Rating,
    string Vehicle,
    string Plate,
    string PhotoUrl
);

/// <summary>
/// Restaurant information.
/// </summary>
public record RestaurantInfo(
    string Name,
    string Cuisine,
    double Rating,
    string ImageUrl
);

// ===== Internal Order Model =====

/// <summary>
/// Internal order representation for processing.
/// </summary>
public record Order(
    string OrderId,
    string RestaurantId,
    RestaurantInfo Restaurant,
    List<OrderItemRequest> Items,
    string DeliveryAddress,
    string? ClientId,
    DateTime CreatedAt,
    DateTime EstimatedDelivery
);
