/*
    Name: IHandService.cs
    Description: Interface for Hand service
    Children: HandService.cs
*/
using Project.Api.Models;

namespace Project.Api.Services;
public interface IHandService
{
    // Define Fields
    Task<List<Hand>> GetHandsByRoomIdAsync(Guid roomId);

    Task<Hand?> GetHandAsyncByIdAsync(Guid handId);

    Task<List<Hand>> GetHandsByUserIdAsync(Guid roomId, Guid userId);

    Task<Hand> CreateHandAsync(Hand hand);

    Task<Hand> UpdateHandAsync(Guid handId, Hand hand);

    Task<Hand> PatchHandAsync(Guid handId, int? Order = null, string? CardsJson = null, int? Bet = null);

    Task<Hand> DeleteHandAsync(Guid handId);
}
