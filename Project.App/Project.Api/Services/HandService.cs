using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;

namespace Project.Api.Services;

/*
    Name: HandService.cs
    Description: Implementation of Hand service
    Endpoint Functionality:
    Main point: /api/rooms/{roomId}/hands
    CreateHandAsync(Hand hand) - / # create a new hand
    GetHandsByRoomIdAsync(Guid roomId) - / # get all hands in a room
    GetHandAsyncByIdAsync(Guid handId) - /{handId} # get a hand by id
    GetHandsByUserIdAsync(Guid roomId, Guid userId) - /user/{userId} # get all hands for a user in a room
    UpdateHandAsync(Guid handId, Hand hand) - /{handId} # update a hand by id
    PatchHandAsync(Guid handId, int? Order = null, string? CardsJson = null, int? Bet = null) - /{handId} # patch a hand by id
    DeleteHandAsync(Guid handId) - /{handId} # delete a hand by id
    Parent: IHandService.cs
*/
public class HandService : IHandService
{
    // Call the repository
    private readonly IHandRepository _Repo;
    private readonly ILogger<HandService> _logger;

    // Constructor
    public HandService(IHandRepository repo, ILogger<HandService> logger)
    {
        // Dependency Injection
        _Repo = repo;
        _logger = logger;
    }

    // Implement the methods
    // Create a new hand
    public async Task<Hand> CreateHandAsync(Hand hand)
    {
        // send request to repository
        _logger.LogInformation("Creating a new hand");
        return await _Repo.CreateHandAsync(hand);
    }

    // Get all hands in a room
    public async Task<List<Hand>> GetHandsByRoomIdAsync(Guid roomId)
    {
        try
        {
            // send request to repository
            _logger.LogInformation("Getting all hands for room {roomId}", roomId);
            return await _Repo.GetHandsByRoomIdAsync(roomId);
        }
        catch (Exception e)
        {
            // throw an exception and Log it
            _logger.LogError(
                e,
                "Error getting hands for room {roomId}: {e.Message}",
                roomId,
                e.Message
            );
            throw new Exception(e.Message);
        }
    }

    // Get a hand by its ID
    public async Task<Hand?> GetHandByIdAsync(Guid handId)
    {
        try
        {
            // send request to repository
            _logger.LogInformation("Getting hand {handId}", handId);
            return await _Repo.GetHandByIdAsync(handId);
        }
        catch (Exception e)
        {
            // throw an exception and Log it
            _logger.LogError(e, "Error getting hand {handId}: {e.Message}", handId, e.Message);
            throw new Exception(e.Message);
        }
    }

    // Get all hands by a user in a room
    public async Task<List<Hand>> GetHandsByUserIdAsync(Guid roomId, Guid userId)
    {
        try
        {
            // send request to repository
            _logger.LogInformation(
                "Getting all hands for user {userId} in room {roomId}",
                userId,
                roomId
            );
            return await _Repo.GetHandsByUserIdAsync(roomId, userId);
        }
        catch (Exception e)
        {
            // throw an exception and Log it
            _logger.LogError(
                e,
                "Error getting all hands for user {userId} in room {roomId}: {e.Message}",
                userId,
                roomId,
                e.Message
            );
            throw new Exception(e.Message);
        }
    }

    // Update an existing hand
    public async Task<Hand> UpdateHandAsync(Guid handId, Hand hand)
    {
        try
        {
            // send request to repository
            _logger.LogInformation("Updating hand {handId}", handId);
            return await _Repo.UpdateHandAsync(handId, hand);
        }
        catch (Exception e)
        {
            // throw an exception and Log it
            _logger.LogError(e, "Error updating hand {handId}: {e.Message}", handId, e.Message);
            throw new Exception(e.Message);
        }
    }

    // Partially update an existing hand
    public async Task<Hand> PatchHandAsync(Guid handId, int? Order = null, int? Bet = null)
    {
        try
        {
            // send request to repository
            _logger.LogInformation("Patching hand {handId}", handId);
            return await _Repo.PatchHandAsync(handId, Order, Bet);
        }
        catch (Exception e)
        {
            // throw an exception and Log it
            _logger.LogError(e, "Error patching hand {handId}: {e.Message}", handId, e.Message);
            throw new Exception(e.Message);
        }
    }

    // Delete a hand
    public async Task<Hand> DeleteHandAsync(Guid handId)
    {
        try
        {
            // send request to repository
            _logger.LogInformation("Deleting hand {handId}", handId);
            return await _Repo.DeleteHandAsync(handId);
        }
        catch (Exception e)
        {
            // throw an exception and Log it
            _logger.LogError(e, "Error deleting hand {handId}: {e.Message}", handId, e.Message);
            throw new Exception(e.Message);
        }
    }
};
