using Project.Api.DTOs;

namespace Project.Api.Services.Interface;

public interface IDeckApiService
{
    Task<string> CreateDeck();
    Task<bool> CreateEmptyHand(string deckId, long handId);
    Task<List<CardDTO>> PlayerDraw(string deckId, long handId);
    Task<bool> ReturnAllCardsToDeck(string deckId);
}
