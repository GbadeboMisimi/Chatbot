using Chatbot.API.Core.Data;
using Chatbot.API.Core.Handlers;
using Chatbot.API.Core.Models;
using Chatbot.API.Repositories.Implementation;
using Chatbot.API.Repositories.Interface;
using Chatbot.API.Services.Implementation;
using Chatbot.API.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using OpenAI;
using System.ClientModel;
using System.Text;

//try
//{
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var credential = new ApiKeyCredential(
builder.Configuration["OpenAi:Token"]
?? throw new InvalidOperationException("Missing OpenAi:Token"));

var ghModelsClient = new OpenAIClient(credential);

builder.Services.AddSingleton(
    ghModelsClient.GetChatClient("gpt-4o-mini").AsIChatClient());

builder.Services.AddSingleton(
    ghModelsClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator());


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
    var jwtSecretKey = jwtSettings["SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
    {
        throw new InvalidOperationException("JwtSettings:SecretKey must be configured and at least 32 characters long.");
    }
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
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });
    builder.Services.AddAuthorization();

    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IScraperService, ScraperService>();
    builder.Services.AddScoped<IRetrievalService, RetrievalService>();
    builder.Services.AddScoped<IAiService, AiService>();
    builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
    builder.Services.AddScoped<IWebSearchService, WebSearchService>();
    builder.Services.AddScoped<ISessionService, SessionService>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IChatHandler, ChatHandler>();

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

        await context.Database.MigrateAsync();

        if (!context.Users.Any(u => u.Role.ToLower() == "admin"))
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

//}
//catch (Exception ex)
//{
//    Console.WriteLine($"STARTUP ERROR: {ex.Message}");
//    Console.WriteLine($"INNER: {ex.InnerException?.Message}");
//    Console.WriteLine($"STACK: {ex.StackTrace}");
//    Environment.ExitCode = 1;
//}
