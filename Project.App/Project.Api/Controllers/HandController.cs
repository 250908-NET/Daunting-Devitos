using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Repositories;
using Project.Api.Services;
using Serilog;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("api/rooms/{roomId}/hands")]
    public class HandController : ControllerBase
    {
        private readonly ILogger<HandController> _logger;
        private readonly IHandService _handService;

        private readonly IMapper _mapper;

        public HandController(
            ILogger<HandController> logger,
            IHandService handService,
            IMapper mapper
        )
        {
            _logger = logger;
            _handService = handService;
            _mapper = mapper;
        }

        [HttpGet("/", Name = "GetHandsByRoomId")]
        public async Task<IActionResult> GetHandsByRoomId(Guid roomId)
        {
            var hands = await _handService.GetHandsByRoomIdAsync(roomId);
            var handsDto = _mapper.Map<List<HandDTO>>(hands);
            return Ok(handsDto);
        }

        [HttpGet("/{handId}", Name = "GetHandById")]
        public async Task<IActionResult> GetHandById(Guid handId, Guid roomId)
        {
            try
            {
                var hand = await _handService.GetHandByIdAsync(handId);
                var handDto = _mapper.Map<HandDTO>(hand);
                return Ok(handDto);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error getting hand {handId} in room {roomId}: {e.Message}");
                return NotFound(e.Message);
            }
        }

        [HttpGet("/user/{userId}", Name = "GetHandsByUserId")]
        public async Task<IActionResult> GetHandsByUserId(Guid userId, Guid roomId)
        {
            try
            {
                var hands = await _handService.GetHandsByUserIdAsync(roomId, userId);
                var handsDto = _mapper.Map<List<HandDTO>>(hands);
                return Ok(handsDto);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    $"Error getting hands for user {userId} in room {roomId}: {e.Message}"
                );
                return NotFound(e.Message);
            }
        }

        [HttpPost("/", Name = "CreateHand")]
        public async Task<IActionResult> CreateHand(Guid roomId, [FromBody] HandDTO handDto)
        {
            var hand = _mapper.Map<Hand>(handDto);
            var createdHand = await _handService.CreateHandAsync(hand);
            var createdHandDto = _mapper.Map<HandDTO>(createdHand);
            return Ok(createdHandDto);
        }

        [HttpPatch("/{handId}", Name = "AddCardsToHand")]
        public async Task<IActionResult> AddCardsToHand(Guid handId, string cardsJSON)
        {
            var updatedHand = await _handService.PatchHandAsync(handId, CardsJson: cardsJSON);
            var updatedHandDto = _mapper.Map<HandDTO>(updatedHand);
            return Ok(updatedHandDto);
        }

        [HttpPatch("/{handId}/bet", Name = "UpdateHandBet")]
        public async Task<IActionResult> UpdateHandBet(Guid handId, int newBet)
        {
            if (newBet < 0)
            {
                return BadRequest("Bet amount cannot be negative or 0");
            }
            var updatedHand = await _handService.PatchHandAsync(handId, Bet: newBet);
            var updatedHandDto = _mapper.Map<HandDTO>(updatedHand);
            return Ok(updatedHandDto);
        }

        [HttpDelete("/{handId}", Name = "DeleteHand")]
        public async Task<IActionResult> DeleteHand(Guid handId)
        {
            var deletedHand = await _handService.DeleteHandAsync(handId);
            var deletedHandDto = _mapper.Map<HandDTO>(deletedHand);
            return Ok(deletedHandDto);
        }
    }
}
