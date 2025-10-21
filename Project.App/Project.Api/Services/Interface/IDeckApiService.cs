using Project.Api.DTOs;
namespace Project.Api.Services.Interface;
public interface IDeckApiService
{
    Task<string> CreateDeck();
    Task<bool> CreateEmptyHand(string deckId,string handName);
    Task<string> PlayerDraw(string deckId, string handId);
    Task<bool> ReturnAllCardsToDeck(string deckId);

}