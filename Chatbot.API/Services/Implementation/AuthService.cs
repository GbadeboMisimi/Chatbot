using Chatbot.API.Core.DTOs;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Interface;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Chatbot.API.Services.Implementation
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<ServiceResponseDto> RegisterAsync(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return new ServiceResponseDto
                {
                    Success = false,
                    Message = "Email and password are required"
                };
            }

            if (await _userRepository.EmailExistsAsync(dto.Email))
            {
                return new ServiceResponseDto
                {
                    Success = false,
                    Message = "Email already exists"
                };
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                return new ServiceResponseDto
                {
                    Success = false,
                    Message = "Passwords do not match"
                };
            }

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            return new ServiceResponseDto
            {
                Success = true,
                Message = "User registered successfully"
            };
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                return null;
            }

            var user =
                await _userRepository.GetByEmailAsync(dto.Email);

            if (user == null)
            {
                return null;
            }

            bool passwordIsValid =
                BCrypt.Net.BCrypt.Verify(
                    dto.Password,
                    user.PasswordHash);

            if (!passwordIsValid)
            {
                return null;
            }

            var token = GenerateJwtToken(user);

            return new LoginResponseDto
            {
                FullName = user.FullName,
                AccessToken = token,
                ExpiresIn =_configuration.GetValue<int>("JwtSettings:ExpiryInHours")* 60 * 60
            };
        }

        private string GenerateJwtToken(User user)
        {
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience =_configuration["JwtSettings:Audience"];
            var secretKey =_configuration["JwtSettings:SecretKey"];
            var expiryHours =_configuration.GetValue<int>("JwtSettings:ExpiryInHours");

            var key =
                Encoding.UTF8.GetBytes(secretKey!);

            var tokenHandler =
                new JwtSecurityTokenHandler();

            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.FullName),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role)
                }),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                Expires = DateTime.UtcNow.AddHours(
                                    int.Parse(_configuration["JwtSettings:ExpiryInHours"]!)),
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };

            var token =
                tokenHandler.CreateToken(
                    tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}




























































//using Chatbot.API.Core.DTOs;
//using Chatbot.API.Core.Models;
//using Chatbot.API.Repositories.Interface;
//using Chatbot.API.Services.Interface;

//namespace Chatbot.API.Services.Implementation
//{
//    public class AuthService : IAuthService
//    {
//        private readonly IUserRepository _userRepository;
//        private readonly IConfiguration _configuration;
//        public AuthService(IUserRepository userRepository, IConfiguration configuration)
//        {
//            _userRepository = userRepository;
//            _configuration = configuration;
//        }
//        public async Task<string> RegisterAsync(RegisterDto dto)
//        {
//            if(await _userRepository.EmailExistsAsync(dto.Email))
//            {
//                return "Email already exists";
//            }
//            if(dto.Password != dto.ConfirmPassword)
//            {
//                return "Passwords do not match";
//            }
//            var user = new User
//            {
//                FullName = dto.FullName,
//                Email = dto.Email,
//                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
//                Role = "User",
//                CreatedAt = DateTime.UtcNow,
//            };  
//            await _userRepository.AddAsync(user);
//            await _userRepository.SaveChangesAsync();

//            return "User registered successfully";
//        }
//        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
//        {
//            var user = await _userRepository.GetByEmailAsync(dto.Email);
//            if(user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
//            {
//                return null;
//            }
//            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
//            var key = System.Text.Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]!);
//            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
//            {
//                Subject = new System.Security.Claims.ClaimsIdentity(new[]
//                {
//                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
//                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.FullName),
//                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
//                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role)
//                }),
//                Issuer = _configuration["JwtSettings:Issuer"],
//                Audience = _configuration["JwtSettings:Audience"],
//                Expires = DateTime.UtcNow.AddHours(
//                    int.Parse(_configuration["JwtSettings:ExpiryInHours"]!)),
//                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
//                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
//                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
//            };
//            var token = tokenHandler.CreateToken(tokenDescriptor);
//            return tokenHandler.WriteToken(token);
//        }
//    }
//}


