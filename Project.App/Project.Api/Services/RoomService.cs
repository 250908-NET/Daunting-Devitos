using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;

namespace Project.Api.Services;

public class RoomService(IRoomRepository roomRepository, IBlackjackService blackjackService)
{
    private readonly IRoomRepository _roomRepository = roomRepository;

    private readonly IBlackjackService _blackjackService = blackjackService;

    // TODO: change functions to use DTO as input

    // --- get/set room data ---

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        return await _roomRepository.GetAllAsync();
    }

    public async Task<Room?> GetByIdAsync(Guid id)
    {
        return await _roomRepository.GetByIdAsync(id);
    }

    public async Task<Room?> GetByHostIdAsync(Guid hostId)
    {
        return await _roomRepository.GetByHostIdAsync(hostId);
    }

    public async Task<IEnumerable<Room>> GetActiveRoomsAsync()
    {
        return await _roomRepository.GetActiveRoomsAsync();
    }

    public async Task<IEnumerable<Room>> GetPublicRoomsAsync()
    {
        return await _roomRepository.GetPublicRoomsAsync();
    }

    public async Task<Room> CreateAsync(Room room)
    {
        return await _roomRepository.CreateAsync(room);
    }

    public async Task<Room?> UpdateAsync(Room room)
    {
        return await _roomRepository.UpdateAsync(room);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _roomRepository.DeleteAsync(id);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _roomRepository.ExistsAsync(id);
    }

    public async Task GetGameStateAsync(Guid roomId)
    {
        throw new NotImplementedException();
    }

    // --- game functionality ---

    // start game
    // includes specifying game mode and setting optional game config

    // do player action
}
