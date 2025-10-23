using Project.Api.Models;

namespace Project.Api.Repositories.Interface
{
    /*
        Name: IHandRepository.cs
        Description: Interface for Hand repository
        Children: HandRepository.cs
    */
    public interface IHandRepository
    {
        // Define Fields
        // Get a hand by its ID
        Task<Hand?> GetHandByIdAsync(Guid handId);

        // Get all hands in a room

        Task<List<Hand>> GetHandsByRoomIdAsync(Guid roomId);

        // Get all hands by a user in a room
        Task<List<Hand>> GetHandsByUserIdAsync(Guid roomId, Guid userId);

        /// <summary>
        /// Get a hand by room ID, player order, and hand order.
        /// </summary>
        /// <throws>NotFoundException no hand is found.</throws>
        Task<Hand> GetHandByRoomOrderAsync(Guid roomId, int playerOrder, int handOrder);

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
