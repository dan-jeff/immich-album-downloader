using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace ImmichDownloader.Web.Hubs;

/// <summary>
/// SignalR hub for real-time progress updates of background tasks.
/// Handles client connections and manages progress update groups.
/// </summary>
[Authorize]
public class ProgressHub : Hub
{
    /// <summary>
    /// Adds the current connection to a specified group for receiving targeted updates.
    /// </summary>
    /// <param name="groupName">The name of the group to join.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Removes the current connection from a specified group.
    /// </summary>
    /// <param name="groupName">The name of the group to leave.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Automatically adds the connection to the "ProgressUpdates" group.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ProgressUpdates");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Automatically removes the connection from the "ProgressUpdates" group.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "ProgressUpdates");
        await base.OnDisconnectedAsync(exception);
    }
}