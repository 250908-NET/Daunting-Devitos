
using Project.Api.DTOs;
using Project.Api.Services.Interface;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using Project.Api.Enums;

namespace Project.Api.Services;

public class DeckApiService : IDeckApiService
{
    private readonly HttpClient _httpClient;

    public DeckApiService(HttpClient client)
    {
        _httpClient = client;
    }

    public async Task<string> CreateDeck()
    {
        string url = "https://deckofcardsapi.com/api/deck/new/shuffle/?deck_count=6";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to create deck");


        string data = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(data);

        string deckId = doc.RootElement.GetProperty("deck_id").GetString() ?? throw new Exception("Deck ID not found");

        return deckId;
    }

    public async Task<bool> CreateEmptyHand(string deckId, string handName)
    {
        string url = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/add/?cards=";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to create empty hand");
        return true;
    }

    public async Task<string> PlayerDraw(string deckId, string handName)
    {
        //Draw one card, get cardCode
        string drawUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/draw/?count=1";
        var drawResponse = await _httpClient.GetAsync(drawUrl);
        drawResponse.EnsureSuccessStatusCode();

        var drawJson = await drawResponse.Content.ReadAsStringAsync();

        using var drawDoc = JsonDocument.Parse(drawJson);
        string cardCode = drawDoc.RootElement.GetProperty("cards")[0].GetProperty("code").GetString() ?? throw new Exception("Card code not found in draw response");

        //Add Card to the player’s hand
        string addToPileUrl = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/add/?cards={cardCode}";
        var addResponse = await _httpClient.GetAsync(addToPileUrl);
        addResponse.EnsureSuccessStatusCode();

        var addJson = await addResponse.Content.ReadAsStringAsync();

        //Add-to-pile response and return only the "cards" property as JSON string
        using var addDoc = JsonDocument.Parse(addJson);
        var cardsProperty = addDoc.RootElement.GetProperty("piles").GetProperty(handName).GetProperty("cards");

        string cardsJson = cardsProperty.GetRawText(); 

        return cardsJson;
    }

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