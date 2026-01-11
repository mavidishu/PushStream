namespace PushStream.DemoApi.Models;

/// <summary>
/// Request to start a new task.
/// </summary>
/// <param name="Name">Optional name for the task.</param>
public record StartTaskRequest(string? Name = null);

/// <summary>
/// Response after starting a task.
/// </summary>
/// <param name="TaskId">Unique identifier for the task.</param>
/// <param name="Name">Name of the task.</param>
public record StartTaskResponse(string TaskId, string Name);

/// <summary>
/// Internal representation of a task in the queue.
/// </summary>
/// <param name="TaskId">Unique identifier for the task.</param>
/// <param name="Name">Name of the task.</param>
/// <param name="ClientId">Optional client ID for targeted messaging.</param>
/// <param name="CreatedAt">When the task was created.</param>
public record TaskItem(string TaskId, string Name, string? ClientId, DateTime CreatedAt);

/// <summary>
/// Event payload when a task is started.
/// </summary>
public record TaskStartedEvent(string TaskId, string Name, DateTime CreatedAt);

/// <summary>
/// Event payload for task progress updates.
/// </summary>
public record TaskProgressEvent(string TaskId, int Percentage, string Message);

/// <summary>
/// Event payload when a task completes.
/// </summary>
public record TaskCompletedEvent(string TaskId, string Result, double DurationSeconds);

