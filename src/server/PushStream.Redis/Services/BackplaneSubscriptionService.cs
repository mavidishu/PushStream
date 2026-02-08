using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PushStream.Core.Abstractions;
using PushStream.Core.Publishing;

namespace PushStream.Redis.Services;

/// <summary>
/// Background service that subscribes to the backplane and delivers incoming messages
/// to local connections via the local EventPublisher (avoids re-publishing to backplane).
/// </summary>
public sealed class BackplaneSubscriptionService : BackgroundService
{
    private readonly IBackplane _backplane;
    private readonly EventPublisher _localPublisher;
    private readonly ILogger<BackplaneSubscriptionService> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="BackplaneSubscriptionService"/>.
    /// </summary>
    /// <param name="backplane">The backplane to subscribe to.</param>
    /// <param name="localPublisher">The local event publisher (concrete type to avoid re-entering the decorator).</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public BackplaneSubscriptionService(
        IBackplane backplane,
        EventPublisher localPublisher,
        ILogger<BackplaneSubscriptionService> logger)
    {
        _backplane = backplane ?? throw new ArgumentNullException(nameof(backplane));
        _localPublisher = localPublisher ?? throw new ArgumentNullException(nameof(localPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Backplane subscription service starting. ServerId: {ServerId}",
            _backplane.ServerId);

        await _backplane.SubscribeAsync(HandleBackplaneMessageAsync, stoppingToken);

        // Keep the service running until shutdown; the subscription is active via the handler
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleBackplaneMessageAsync(BackplaneMessage message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message.Payload);
            var payload = doc.RootElement;

            if (message.TargetClientId != null)
            {
                await _localPublisher.PublishToAsync(
                    message.TargetClientId,
                    message.EventName,
                    payload,
                    message.EventId);
            }
            else
            {
                await _localPublisher.PublishAsync(
                    message.EventName,
                    payload,
                    message.EventId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error delivering backplane message to local connections. EventName: {EventName}",
                message.EventName);
        }
    }
}
