// Controllers/AuthController.cs
using EnrollmentSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EnrollmentSystem.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        // In a real application, validate against your user database.
        private readonly List<User> _users = new List<User>
        {
            new User { UserId = 1, Username = "student1", Role = "student" },
            new User { UserId = 2, Username = "teacher1", Role = "teacher" }
        };

        private readonly string _secretKey = "YourVeryStrongSecretKey"; // Use a secure key from configuration

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // For demonstration, we use hard-coded passwords.
            if ((request.Username == "student1" && request.Password == "pass1") ||
                (request.Username == "teacher1" && request.Password == "pass2"))
            {
                // Find user details
                var user = _users.FirstOrDefault(u => u.Username == request.Username);
                if (user == null)
                    return Unauthorized(new { message = "Invalid credentials" });

                var token = GenerateJwtToken(user);
                return Ok(new { token });
            }

            return Unauthorized(new { message = "Invalid credentials" });
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Create claims for the token payload
            var claims = new List<Claim>
            {
                new Claim("user_id", user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
