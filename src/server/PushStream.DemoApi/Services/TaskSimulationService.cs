using System.Threading.Channels;
using PushStream.Core.Abstractions;
using PushStream.DemoApi.Models;

namespace PushStream.DemoApi.Services;

/// <summary>
/// Background service that processes simulated long-running tasks.
/// Demonstrates PushStream's event publishing capabilities.
/// </summary>
public class TaskSimulationService : BackgroundService
{
    private readonly Channel<TaskItem> _taskChannel;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<TaskSimulationService> _logger;

    // Progress simulation settings
    private const int ProgressStepMs = 500;      // Time between progress updates
    private const int ProgressStepPercent = 10;  // Percentage increase per step

    public TaskSimulationService(
        IEventPublisher eventPublisher,
        ILogger<TaskSimulationService> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
        
        // Unbounded channel for task queue
        _taskChannel = Channel.CreateUnbounded<TaskItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Enqueue a new task for processing.
    /// </summary>
    public async Task<TaskItem> EnqueueTaskAsync(string? name, string? clientId)
    {
        var task = new TaskItem(
            TaskId: Guid.NewGuid().ToString("N")[..8], // Short ID for readability
            Name: name ?? $"Task-{DateTime.UtcNow:HHmmss}",
            ClientId: clientId,
            CreatedAt: DateTime.UtcNow
        );

        await _taskChannel.Writer.WriteAsync(task);
        _logger.LogInformation("Task {TaskId} enqueued: {Name}", task.TaskId, task.Name);

        return task;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskSimulationService started");

        await foreach (var task in _taskChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessTaskAsync(task, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing task {TaskId}", task.TaskId);
            }
        }

        _logger.LogInformation("TaskSimulationService stopped");
    }

    private async Task ProcessTaskAsync(TaskItem task, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Processing task {TaskId}: {Name}", task.TaskId, task.Name);

        // Publish task.started event
        await PublishEventAsync(
            "task.started",
            new TaskStartedEvent(task.TaskId, task.Name, task.CreatedAt),
            task.ClientId
        );

        // Simulate progress: 10%, 20%, 30%, ... 100%
        for (int percentage = ProgressStepPercent; percentage <= 100; percentage += ProgressStepPercent)
        {
            await Task.Delay(ProgressStepMs, cancellationToken);

            var message = GetProgressMessage(percentage);
            
            // Publish task.progress event
            await PublishEventAsync(
                "task.progress",
                new TaskProgressEvent(task.TaskId, percentage, message),
                task.ClientId
            );

            _logger.LogDebug("Task {TaskId}: {Percentage}%", task.TaskId, percentage);
        }

        // Calculate duration
        var duration = (DateTime.UtcNow - startTime).TotalSeconds;

        // Publish task.completed event
        await PublishEventAsync(
            "task.completed",
            new TaskCompletedEvent(task.TaskId, "Success", Math.Round(duration, 2)),
            task.ClientId
        );

        _logger.LogInformation("Task {TaskId} completed in {Duration:F2}s", task.TaskId, duration);
    }

    private async Task PublishEventAsync<T>(string eventName, T payload, string? clientId)
    {
        if (!string.IsNullOrEmpty(clientId))
        {
            // Targeted: send only to specific client
            await _eventPublisher.PublishToAsync(clientId, eventName, payload);
        }
        else
        {
            // Broadcast: send to all connected clients
            await _eventPublisher.PublishAsync(eventName, payload);
        }
    }

    private static string GetProgressMessage(int percentage) => percentage switch
    {
        10 => "Initializing connection...",
        20 => "Fetching resources...",
        30 => "Processing batch 1/3...",
        40 => "Processing batch 2/3...",
        50 => "Processing batch 3/3...",
        60 => "Running optimizations...",
        70 => "Validating output...",
        80 => "Writing results...",
        90 => "Cleaning up...",
        100 => "Completed successfully",
        _ => $"Working... ({percentage}%)"
    };
}