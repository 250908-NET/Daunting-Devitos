using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Project.Api.DTOs;
using Project.Api.Services.Interface;

namespace Project.Api.Services
{
    public class SplitService : ISplitService
    {
        private readonly HttpClient _httpClient;

        public SplitService(HttpClient client)
        {
            _httpClient = client;
        }


        // Split a specific card manually.

        public async Task<bool> SplitCard(string deckId, string originalHand, string newHand, string cardCode)
        {
            bool removed = await RemoveFromHand(deckId, originalHand, cardCode);
            if (!removed) throw new Exception("Failed to remove card from original hand");

            bool added = await AddToHand(deckId, newHand, cardCode);
            if (!added) throw new Exception("Failed to add card to new hand");

            return true;
        }


        // Automatically splits the first pair in the hand.

        public async Task<bool> SplitFirstPair(string deckId, string originalHand, string newHand)
        {
            var handCards = await ListHand(deckId, originalHand);

            // Find first pair
            var grouped = handCards.GroupBy(c => c.Value)
                                   .FirstOrDefault(g => g.Count() >= 2);

            if (grouped == null)
                throw new Exception("No pair found to split");

            // Pick one card from the pair
            var cardToSplit = grouped.ElementAt(1).Code;

            return await SplitCard(deckId, originalHand, newHand, cardToSplit);
        }


        // List all cards in a hand

        public async Task<List<CardDTO>> ListHand(string deckId, string handName)
        {
            string url = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/list/";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("piles", out var piles) ||
                !piles.TryGetProperty(handName, out var hand))
            {
                return new List<CardDTO>();
            }

            var cardsProp = hand.GetProperty("cards");
            return JsonSerializer.Deserialize<List<CardDTO>>(cardsProp.GetRawText()) ?? new List<CardDTO>();
        }


        /// Removes a card from a hand.

        private async Task<bool> RemoveFromHand(string deckId, string handName, string cardCode)
        {
            string url = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/draw/?cards={cardCode}";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }


        /// Adds a card to a hand (creates the hand if it does not exist)

        private async Task<bool> AddToHand(string deckId, string handName, string cardCode)
        {
            string url = $"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/add/?cards={cardCode}";
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
    }
}