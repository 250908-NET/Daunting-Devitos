using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Project.Api.DTOs;
using Project.Api.Enums;
using Project.Api.Services.Interface;

namespace Project.Api.Services;

public class DeckApiService : IDeckApiService
{
    private readonly HttpClient _httpClient;

    public DeckApiService(HttpClient client)
    {
        _httpClient = client;
    }

    /*
    Create a new shuffled deck and return the deck ID.
    The deck will consist of specified number of standard decks shuffled together and two Jokers if enabled.

    default values:
        numOfDecks = 6
        enableJokers = false
    */
    public async Task<string> CreateDeck(int numOfDecks = 6, bool enableJokers = false)
    {
        string url =
            $"https://deckofcardsapi.com/api/deck/new/shuffle/?deck_count={numOfDecks}&enable_Jokers={enableJokers}";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to create deck");

        string data = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(data);

        string deckId =
            doc.RootElement.GetProperty("deck_id").GetString() ?? throw new Exception(
                "Deck ID not found"
            );

        return deckId;
    }

    /*
    Create an empty hand (pile) identified by handName within the specified deck.
    Returns true if successful.
    */
    public async Task<bool> CreateEmptyHand(string deckId, string handName)
    {
        string url = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/add/?cards=";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to create empty hand");
        return true;
    }

    /*
    Create an empty hand (pile) identified by handName within the specified deck.
    Returns true if successful.
    */
    public async Task<bool> CreateEmptyHand(string deckId, long handId)
    {
        return await CreateEmptyHand(deckId, handId.ToString());
    }

    /*
    Player draws specified number of cards, count, from specified deck.
    Draws one card by default.
    Returns true if successful.
    */
    public async Task<List<CardDTO>> DrawCards(string deckId, long handId, int count = 1)
    {
        return await DrawCards(deckId, handId.ToString(), count);
    }

    /*
    Player draws specified number of cards, count, from specified deck.
    Draws one card by default.
    Returns true if successful.
    */
    public async Task<List<CardDTO>> DrawCards(string deckId, string handName, int count = 1)
    {
        //Draw one card, get cardCode
        string drawUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/draw/?count={count}";
        var drawResponse = await _httpClient.GetAsync(drawUrl);
        drawResponse.EnsureSuccessStatusCode();

        var drawJson = await drawResponse.Content.ReadAsStringAsync();

        using var drawDoc = JsonDocument.Parse(drawJson);
        string cardCode =
            drawDoc.RootElement.GetProperty("cards")[0].GetProperty("code").GetString()
            ?? throw new Exception("Card code not found in draw response");

        //Add Card to the player’s hand
        await addToHand(deckId, handName.ToString(), cardCode);

        string listPileUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/list/";
        var listResponse = await _httpClient.GetAsync(listPileUrl);
        listResponse.EnsureSuccessStatusCode();
        var pilesJson = await listResponse.Content.ReadAsStringAsync();

        //list contents of hand
        return await listHand(deckId, handName.ToString());
    }

    /*
    Return all cards from all piles back to the main deck.
    Returns true if successful.
    */
    public async Task<bool> ReturnAllCardsToDeck(string deckId)
    {
        string url = $"https://deckofcardsapi.com/api/deck/{deckId}/return/";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to return cards to deck");
        }
        return true;
    }

    /*
    Calls Api to add card to specified hand. If hand does not exist, will create a hand with give handName.
    Returns true, if operation succeeded.
    */
    private async Task<bool> addToHand(string deckId, string handName, string cardCode)
    {
        string addToPileUrl =
            $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/add/?cards={cardCode}";
        var addResponse = await _httpClient.GetAsync(addToPileUrl);
        if (!addResponse.IsSuccessStatusCode)
        {
            throw new Exception("Failed to return cards to deck");
        }
        return true;
    }

    /*
    Calls Api to list cards in specified pile from specified deck.
    Returns List<CardDTO>,
    */
    private async Task<List<CardDTO>> listHand(string deckId, string handName)
    {
        string listPileUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/list/";
        var listResponse = await _httpClient.GetAsync(listPileUrl);
        listResponse.EnsureSuccessStatusCode();
        var pilesJson = await listResponse.Content.ReadAsStringAsync();

        //Add-to-pile response and return only the "cards" property as JSON string
        using var listDoc = JsonDocument.Parse(pilesJson);
        var cardsProperty = listDoc
            .RootElement.GetProperty("piles")
            .GetProperty(handName)
            .GetProperty("cards");

        string cardsJson = cardsProperty.GetRawText();

        return JsonSerializer.Deserialize<List<CardDTO>>(cardsJson) ?? new List<CardDTO>();
    }
}
