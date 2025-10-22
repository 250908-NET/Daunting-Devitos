/*
    Name: IHandRepository.cs
    Description: Interface for Hand repository
    Children: HandRepository.cs
*/
using Project.Api.Enums;
using Project.Api.Models;
using Project.Api.Repositories;

namespace Project.Api.Repositories
{
    public interface IHandRepository
    {
        // Define Fields
        // Get a hand by its ID
        Task<Hand?> GetHandByIdAsync(Guid handId);

        // Get all hands in a room

        Task<List<Hand>> GetHandsByRoomIdAsync(Guid roomId);

        // Get all hands by a user in a room
        Task<List<Hand>> GetHandsByUserIdAsync(Guid roomId, Guid userId);

        // Create a new hand
        Task<Hand> CreateHandAsync(Hand hand);

        // Update an existing hand
        Task<Hand> UpdateHandAsync(Guid handId, Hand hand);

        // Partially update an existing hand
        Task<Hand> PatchHandAsync(Guid handId, int? Order = null, int? Bet = null);

        // Delete a hand
        Task<Hand> DeleteHandAsync(Guid handId);

        // Save changes to the database
        Task SaveChangesAsync();
    }
}
