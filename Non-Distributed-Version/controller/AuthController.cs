// Controllers/AuthController.cs
using EnrollmentSystem.Models;
using Microsoft.Data.Sqlite;
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
        private readonly string _secretKey = "YourVeryVeryVerySecureSecretKey123!"; // Use a secure key from configuration

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            using var connection = new SqliteConnection("Data Source=./schema/Users.db");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT user_id, username, role
                FROM Users
                WHERE username = $username AND password_hash = $password";
            command.Parameters.AddWithValue("$username", request.Username);
            command.Parameters.AddWithValue("$password", request.Password); // hashed ideally

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return Unauthorized(new { message = "Invalid credentials" });

            var user = new User
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = reader.GetString(2)
            };

            var token = GenerateJwtToken(user);
            return Ok(new { token });
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
