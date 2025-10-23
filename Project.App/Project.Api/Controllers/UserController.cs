using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Services.Interface;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        public UserController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // GET: api/user
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAllUsers()
        {
            var users = await _userRepository.GetAllAsync();
            return Ok(users);
        }

        // GET: api/user/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUserById(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound($"User with ID {id} not found.");
            return Ok(user);
        }

        // POST: api/user
        [HttpPost]
        public async Task<ActionResult<User>> CreateUser([FromBody] User user)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _userRepository.AddAsync(user);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }

        // PUT: api/user/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] User user)
        {
            if (id != user.Id)
                return BadRequest("User ID mismatch.");

            var existingUser = await _userRepository.GetByIdAsync(id);
            if (existingUser == null)
                return NotFound($"User with ID {id} not found.");

            // Update allowed fields
            existingUser.Name = user.Name;
            existingUser.Email = user.Email;
            existingUser.Balance = user.Balance;

            await _userRepository.UpdateAsync(existingUser);
            return NoContent();
        }

        // DELETE: api/user/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var existingUser = await _userRepository.GetByIdAsync(id);
            if (existingUser == null)
                return NotFound($"User with ID {id} not found.");

            await _userRepository.DeleteAsync(id);
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me([FromServices] IUserService users)
        {
            // Identity comes from the auth cookie
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email);
            if (email is null)
                return Unauthorized();

            var u = await users.GetByEmailAsync(email.Value);
            if (u is null)
                return NotFound();
            //temporary user dto since we dont have one made
            return Ok(
                new
                {
                    id = u.Id,
                    name = u.Name,
                    email = u.Email,
                    balance = u.Balance,
                    avatarUrl = u.AvatarUrl,
                }
            );
        }
    }
}
