namespace PushStream.Core.Abstractions;

/// <summary>
/// Manages the storage and retrieval of active client connections.
/// </summary>
public interface IConnectionStore
{
    /// <summary>
    /// Registers a new client connection.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <returns>A task representing the async operation.</returns>
    Task AddAsync(IClientConnection connection);

    /// <summary>
    /// Removes a connection by its unique connection ID.
    /// </summary>
    /// <param name="connectionId">The unique connection ID to remove.</param>
    /// <returns>A task representing the async operation.</returns>
    Task RemoveAsync(string connectionId);

    /// <summary>
    /// Gets all currently active connections.
    /// </summary>
    /// <returns>All active connections.</returns>
    Task<IEnumerable<IClientConnection>> GetAllAsync();

    /// <summary>
    /// Gets all connections associated with a specific client identifier.
    /// </summary>
    /// <param name="clientId">The logical client identifier.</param>
    /// <returns>Connections matching the client ID.</returns>
    Task<IEnumerable<IClientConnection>> GetByClientIdAsync(string clientId);

    /// <summary>
    /// Gets the total count of active connections.
    /// </summary>
    /// <returns>The number of active connections.</returns>
    Task<int> GetCountAsync();
}

