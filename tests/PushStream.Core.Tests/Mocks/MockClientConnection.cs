using PushStream.Core.Abstractions;

namespace PushStream.Core.Tests.Mocks;

/// <summary>
/// A mock implementation of <see cref="IClientConnection"/> for testing.
/// </summary>
public sealed class MockClientConnection : IClientConnection
{
    private readonly List<string> _writtenData = new();
    private bool _isConnected = true;

    public MockClientConnection(string connectionId, string clientId)
    {
        ConnectionId = connectionId;
        ClientId = clientId;
    }

    public string ConnectionId { get; }

    public string ClientId { get; }

    public bool IsConnected => _isConnected;

    /// <summary>
    /// Gets all data that has been written to this connection.
    /// </summary>
    public IReadOnlyList<string> WrittenData => _writtenData;

    /// <summary>
    /// Gets the last data written, or null if nothing was written.
    /// </summary>
    public string? LastWrittenData => _writtenData.Count > 0 ? _writtenData[^1] : null;

    public Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Connection is closed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _writtenData.Add(data);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates closing the connection.
    /// </summary>
    public void Close()
    {
        _isConnected = false;
    }

    /// <summary>
    /// Clears all recorded written data.
    /// </summary>
    public void ClearWrittenData()
    {
        _writtenData.Clear();
    }
}