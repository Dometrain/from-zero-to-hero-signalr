using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Protocol;
using RealTimeDemo.HubProtocols;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RealTimeDemo.Hubs;

var builder = WebApplication.CreateBuilder(args);
var accessKey = builder.Configuration["JWT:AccessKey"];
var refreshKey = builder.Configuration["JWT:RefreshKey"];
var issuer = builder.Configuration["JWT:Issuer"];
var audience = builder.Configuration["JWT:Audience"];

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

                if (string.IsNullOrEmpty(token) == false && path.StartsWithSegments("/hubs"))
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



builder.Services.AddSignalR();



    // OPTIONAL ADD MESSAGEPACK PROTOCOL
    //.AddMessagePackProtocol();
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
                .AllowCredentials()
        );
});

var app = builder.Build();
//app.UseHttpsRedirection();
app.UseCors("dev"); 
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHub<StockHub>("/hubs/stocks"); 
app.MapHub<CrossPlatformHub>("/hubs/crossplatform");
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

    Claim[] claims = null;

    if (login.Username == "nick-pro")
    {
        claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, login.Username),
            new Claim("subscription", "pro"),
            new Claim(ClaimTypes.Role, "Sales"),
        };
    }
    else if (login.Username == "nick")
    {
        claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, login.Username),
            new Claim(ClaimTypes.Role, "Sales"),
        };
    }
    else
    {
        claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, login.Username),
        };
    }
    

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

        if (username == null || refreshTokens.ContainsKey(tokenPair.refreshToken) == false)
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

static string GenerateJwtToken(IEnumerable<Claim> claims, string accessKey, string issuer, string audience, TimeSpan lifeTime )
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
    



record UserLogin(string Username, string Password);

record TokenPair(string accessToken, string refreshToken);

