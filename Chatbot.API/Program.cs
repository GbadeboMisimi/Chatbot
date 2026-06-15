using Chatbot.API.Core.Data;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Implementation;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Implementation;
using Chatbot.API.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter your JWT token here"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    //builder.Services.AddDbContext<AppDbContext>(options =>
    //    options.UseSqlServer(
    //        builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(600)));

    builder.Services.AddHttpClient();

    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IChatHistoryRepository, ChatHistoryRepository>();
    builder.Services.AddScoped<IChatDocumentRepository, ChatDocumentRepository>();
    builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();

    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]))
        };
    });
    builder.Services.AddAuthorization();

    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IScraperService, ScraperService>();
    builder.Services.AddScoped<IRetrievalService, RetrievalService>();
    builder.Services.AddScoped<IAiService, AiService>();
    builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
    builder.Services.AddScoped<ISessionService, SessionService>();
    builder.Services.AddScoped<IChatService, ChatService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    // Default admin
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!context.Users.Any(u => u.Role == "admin"))
        {
            context.Users.Add(new User
            {
                FullName = "Admin",
                Email = "admin@chatbot.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }
    }

    app.Run();

}
catch (Exception ex)
{
    Console.WriteLine($"STARTUP ERROR: {ex.Message}");
    Console.WriteLine($"INNER: {ex.InnerException?.Message}");
    Console.WriteLine($"STACK: {ex.StackTrace}");
    Console.ReadLine();
}