using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using KironAPI.Models;
using Microsoft.AspNetCore.Authorization;

// using KironAPI.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
// using Models.UserRegisterDto;

namespace KironAPI.Controllers
{

    [ApiController] 
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly UserRepository _userRepository;
         private readonly IConfiguration _configuration;

        public UserController(ILogger<UserController> logger, UserRepository userRepository, IConfiguration configuration)
        {
            _logger = logger;
            _userRepository = userRepository;
            _configuration = configuration;
        }

       [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto userDto)
{
    var response = new ServiceResponse<string>();

    try
    {
        // Check if the username already exists
        var existingUser = await _userRepository.GetUserByUsername(userDto.Username);
        if (existingUser != null)
        {
            response.Success = false;
            response.Message = "Username is already taken";
            return BadRequest(response);
        }

        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password + salt);

        // Create user and store in database
        var newUser = new User 
        { 
            Username = userDto.Username, 
            PasswordHash = hashedPassword,
            Salt = salt 
        };
        await _userRepository.AddUser(newUser);

        response.Success = true;
        response.Message = "Registration successful!";
        return Ok(response);
    }
    catch(Exception ex)
    {
        response.Success = false;
        response.Message = "Registration failed. Please try again later.";
        // Optionally log the exception for debugging purposes.
        return BadRequest(response);
    }
}

[HttpPost("login")]
public async Task<IActionResult> Login(UserRegisterDto userDto)
{
    var user = await _userRepository.GetUserByUsername(userDto.Username);
    if(user == null || !BCrypt.Net.BCrypt.Verify(userDto.Password + user.Salt, user.PasswordHash))
    {
        return Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("Jwt:Key").Value));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.Now.AddHours(1),
        SigningCredentials = creds
    };

    var tokenHandler = new JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);

    return Ok(new { token = tokenHandler.WriteToken(token) });
    }
    }
}