using PushStream.AspNetCore.DependencyInjection;
using PushStream.AspNetCore.Routing;
using PushStream.DemoApi.Models;
using PushStream.DemoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== PushStream Setup (< 10 lines!) =====

// Register PushStream services with client ID resolution from query parameter
builder.Services.AddPushStream(options =>
{
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    options.ClientIdResolver = ctx => ctx.Request.Query["clientId"].FirstOrDefault();
});

// Register background services
builder.Services.AddSingleton<TaskSimulationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TaskSimulationService>());

builder.Services.AddSingleton<OrderSimulationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OrderSimulationService>());

var app = builder.Build();

// Serve static files from wwwroot
app.UseStaticFiles();

// ===== API Endpoints =====

// SSE event stream endpoint
app.MapEventStream("/events");

// ===== Task Endpoints (existing demo) =====

app.MapPost("/api/tasks/start", async (
    StartTaskRequest? request,
    TaskSimulationService taskService,
    HttpContext context) =>
{
    var clientId = context.Request.Query["clientId"].FirstOrDefault();
    var task = await taskService.EnqueueTaskAsync(request?.Name, clientId);
    return Results.Ok(new StartTaskResponse(task.TaskId, task.Name));
});

// ===== Order Endpoints (Live Order Tracker) =====

// Get available restaurants
app.MapGet("/api/restaurants", () =>
{
    var restaurants = OrderSimulationService.GetAllRestaurants()
        .Select(r => new { id = r.Id, name = r.Info.Name, cuisine = r.Info.Cuisine, rating = r.Info.Rating })
        .ToList();
    return Results.Ok(restaurants);
});

// Place a new order
app.MapPost("/api/orders/place", async (
    PlaceOrderRequest? request,
    OrderSimulationService orderService,
    HttpContext context) =>
{
    if (request == null || request.Items.Count == 0)
    {
        return Results.BadRequest(new { error = "Order must contain at least one item" });
    }

    var clientId = context.Request.Query["clientId"].FirstOrDefault();
    var order = await orderService.EnqueueOrderAsync(
        request.RestaurantId,
        request.Items,
        request.DeliveryAddress,
        clientId
    );

    var total = request.Items.Sum(i => i.Price * i.Quantity);
    var estimatedMinutes = (int)(order.EstimatedDelivery - DateTime.UtcNow).TotalMinutes;

    return Results.Ok(new PlaceOrderResponse(
        order.OrderId,
        order.Restaurant.Name,
        request.Items,
        total,
        order.EstimatedDelivery,
        estimatedMinutes
    ));
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Redirect root to index.html
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
