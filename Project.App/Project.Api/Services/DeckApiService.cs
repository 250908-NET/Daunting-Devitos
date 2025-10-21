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
    The deck will consist of 6 standard decks shuffled together.
    */
    public async Task<string> CreateDeck()
    {
        string url = "https://deckofcardsapi.com/api/deck/new/shuffle/?deck_count=6";
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
    Create an empty hand (pile) for a player identified by handId within the specified deck.
    Returns true if successful.
    */
    public async Task<bool> CreateEmptyHand(string deckId, long handId)
    {
        string url = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handId}/add/?cards=";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to create empty hand");
        return true;
    }

    /*
    Player draws one card from the deck and adds it to their hand (pile).
    Returns the list of cards currently in the player's hand.
    */
    public async Task<List<CardDTO>> PlayerDraw(string deckId, long handId)
    {
        //Draw one card, get cardCode
        string drawUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/draw/?count=1";
        var drawResponse = await _httpClient.GetAsync(drawUrl);
        drawResponse.EnsureSuccessStatusCode();

        var drawJson = await drawResponse.Content.ReadAsStringAsync();

        using var drawDoc = JsonDocument.Parse(drawJson);
        string cardCode =
            drawDoc.RootElement.GetProperty("cards")[0].GetProperty("code").GetString()
            ?? throw new Exception("Card code not found in draw response");

        //Add Card to the player’s hand
        string addToPileUrl =
            $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handId}/add/?cards={cardCode}";
        var addResponse = await _httpClient.GetAsync(addToPileUrl);
        addResponse.EnsureSuccessStatusCode();

        string listPileUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handId}/list/";
        var listResponse = await _httpClient.GetAsync(listPileUrl);
        listResponse.EnsureSuccessStatusCode();
        var pilesJson = await listResponse.Content.ReadAsStringAsync();

        //Add-to-pile response and return only the "cards" property as JSON string
        using var listDoc = JsonDocument.Parse(pilesJson);
        var cardsProperty = listDoc
            .RootElement.GetProperty("piles")
            .GetProperty(handId.ToString())
            .GetProperty("cards");

        string cardsJson = cardsProperty.GetRawText();

        return JsonSerializer.Deserialize<List<CardDTO>>(cardsJson) ?? new List<CardDTO>();
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
}
