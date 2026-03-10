using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using RealTimeDemo.Hubs;
using RealTimeDemo.Models;
using Serilog;

namespace RealTimeDemo;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var accessKey = builder.Configuration["JWT:AccessKey"];
        var refreshKey = builder.Configuration["JWT:RefreshKey"];
        var issuer = builder.Configuration["JWT:Issuer"];
        var audience = builder.Configuration["JWT:Audience"];

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessKey)),
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience
                };

                options.Events = new JwtBearerEvents()
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("PaidSubscriber", p =>
            {
                p.RequireClaim("subscription", "pro", "enterprise");
            });
        });
        
   
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerBuilder => tracerBuilder.AddSource("RealTimeDemo")
                .AddAzureMonitorTraceExporter(options =>
                {
                    
                }));
          

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("dev", policy =>
                policy.WithOrigins(
                        "http://localhost:4200",
                        "http://localhost:3000",
                        "null",
                        "https://localhost:7216"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseCors("dev");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/hubs/chat");

        app.MapHub<StockHub>("/hubs/stocks");
        app.MapGet("/", () => "Hello World!");

        var refreshTokens = new Dictionary<string, string>();

        app.MapPost("/login", (UserLogin login) =>
        {
            if (login.Username != "nick" || login.Password != "password")
            {
                if (login.Username != "nick-pro" || login.Password != "password")
                {
                    return Results.Unauthorized();
                }
            }

            Claim[] claims = login.Username switch
            {
                "nick-pro" => new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, login.Username),
                    new Claim("subscription", "pro"),
                    new Claim(ClaimTypes.Role, "Sales")
                },
                "nick" => new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, login.Username),
                    new Claim(ClaimTypes.Role, "Sales")
                },
                _ => new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, login.Username)
                }
            };

            var accessToken = GenerateJwtToken(claims, accessKey, issuer, audience, TimeSpan.FromMinutes(30));
            var refreshToken = GenerateJwtToken(claims, refreshKey, issuer, audience, TimeSpan.FromDays(7));
            refreshTokens.Add(refreshToken, login.Username);
            return Results.Ok(new { access_token = accessToken, refresh_token = refreshToken });
        });

        app.MapPost("/refreshToken", (TokenPair tokenPair) =>
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(refreshKey)),
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience
            };

            try
            {
                var principal = handler.ValidateToken(tokenPair.refreshToken, validationParams, out _);
                var username = principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

                if (username == null || !refreshTokens.ContainsKey(tokenPair.refreshToken))
                {
                    return Results.Unauthorized();
                }

                var newAccessToken = GenerateJwtToken(principal.Claims, accessKey, issuer, audience, TimeSpan.FromMinutes(30));
                return Results.Ok(new { access_token = newAccessToken });
            }
            catch
            {
                return Results.Unauthorized();
            }
        });

        app.Run();
    }

    private static string GenerateJwtToken(IEnumerable<Claim> claims, string accessKey, string issuer, string audience, TimeSpan lifeTime)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifeTime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private record UserLogin(string Username, string Password);
    private record TokenPair(string accessToken, string refreshToken);
}

