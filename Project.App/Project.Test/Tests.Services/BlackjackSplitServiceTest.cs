using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;
using Project.Api.Services;
using Project.Api.DTOs;
using RichardSzalay.MockHttp;
using System.Collections.Generic;

namespace Project.Test.Services
{
    public class SplitServiceTests
    {
        private SplitService CreateSplitService(MockHttpMessageHandler mockHttp)
        {
            var client = mockHttp.ToHttpClient();
            return new SplitService(client);
        }

        [Fact]
        public async Task ListHand_ShouldReturnCards()
        {
            // Arrange
            var mockHttp = new MockHttpMessageHandler();

            string deckId = "testdeck";
            string handName = "player1";
            var cardsJson = new
            {
                piles = new Dictionary<string, object>
                {
                    [handName] = new {
                        cards = new[]
                        {
                            new { code = "AS", value = "ACE", suit = "SPADES", image = "" }
                        }
                    }
                }
            };

            mockHttp.When($"https://deckofcardsapi.com/api/deck/{deckId}/pile/{handName}/list/")
                    .Respond("application/json", JsonSerializer.Serialize(cardsJson));

            var service = CreateSplitService(mockHttp);

            // Act
            var result = await service.ListHand(deckId, handName);

            // Assert
            Assert.Single(result);
            Assert.Equal("ACE", result[0].Value);
            Assert.Equal("AS", result[0].Code);
        }

        [Fact]
        public async Task SplitCard_ShouldCallAddAndRemove_ReturnTrue()
        {
            // Arrange
            var mockHttp = new MockHttpMessageHandler();
            string deckId = "testdeck";
            string originalHand = "hand1";
            string newHand = "hand2";
            string cardCode = "AS";

            // Mock remove
            mockHttp.When($"https://deckofcardsapi.com/api/deck/{deckId}/pile/{originalHand}/draw/?cards={cardCode}")
                    .Respond(HttpStatusCode.OK);

            // Mock add
            mockHttp.When($"https://deckofcardsapi.com/api/deck/{deckId}/pile/{newHand}/add/?cards={cardCode}")
                    .Respond(HttpStatusCode.OK);

            var service = CreateSplitService(mockHttp);

            // Act
            var result = await service.SplitCard(deckId, originalHand, newHand, cardCode);

            // Assert
            Assert.True(result);
        }
    }
}
