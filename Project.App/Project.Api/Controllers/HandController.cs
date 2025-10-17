using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using Project.Api.DTOs;
using Project.Api.Models;
using Project.Api.Repositories;
using Project.Api.Services;
using Project.DTOs;
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
            var hand = await _handService.GetHandByIdAsync(handId);
            if (hand == null || hand.RoomPlayer == null || hand.RoomPlayer.RoomId != roomId)
            {
                return NotFound();
            }
            var handDto = _mapper.Map<HandDTO>(hand);
            return Ok(handDto);
        }

        [HttpGet("/user/{userId}", Name = "GetHandsByUserId")]
        public async Task<IActionResult> GetHandsByUserId(Guid userId, Guid roomId)
        {
            var hands = await _handService.GetHandsByUserIdAsync(roomId, userId);
            var handsDto = _mapper.Map<List<HandDTO>>(hands);
            return Ok(handsDto);
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
            return Ok();
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
