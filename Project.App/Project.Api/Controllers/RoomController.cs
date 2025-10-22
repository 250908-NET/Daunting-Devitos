using Microsoft.AspNetCore.Mvc;
using Project.Api.Services.Interface;

namespace Project.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomController(IRoomSSEService roomSSEService) : ControllerBase
{
    private readonly IRoomSSEService _roomSSEService = roomSSEService;

    [HttpGet("{roomId}/events")]
    public async Task GetRoomEvents(Guid roomId)
    {
        if (HttpContext.Request.Headers.Accept == "text/event-stream")
        {
            await _roomSSEService.AddConnectionAsync(roomId, HttpContext.Response);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync(
                "This endpoint requires the header 'Accept: text/event-stream'."
            );
        }
    }
}
