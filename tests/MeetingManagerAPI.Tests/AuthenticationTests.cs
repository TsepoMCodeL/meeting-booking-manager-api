using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MeetingManagerAPI.Tests;

// Verifies API endpoints reject invalid bearer tokens and accept valid ones.
public class AuthenticationTests : IClassFixture<AuthenticationTests.ApiFactory>
{
    private const string Issuer = "https://issuer.example.test";
    private const string Audience = "meeting-manager-api";
    private static readonly SymmetricSecurityKey SigningKey = new(
        System.Text.Encoding.UTF8.GetBytes("a-test-signing-key-that-is-at-least-32-bytes-long"));
    private readonly HttpClient _client;

    public AuthenticationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Api_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/rooms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("invalid-token", Audience, false)]
    [InlineData("", "another-api", false)]
    [InlineData("", Audience, true)]
    public async Task Api_RejectsInvalidTokensAndAcceptsValidToken(string rawToken, string audience, bool expired)
    {
        var token = rawToken.Length > 0 ? rawToken : CreateToken(audience, expired);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/rooms");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(expired || audience != Audience || rawToken.Length > 0
            ? HttpStatusCode.Unauthorized
            : HttpStatusCode.OK, response.StatusCode);
    }

    private static string CreateToken(string audience, bool expired)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            notBefore: now.AddMinutes(-10),
            expires: expired ? now.AddMinutes(-5) : now.AddMinutes(5),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Hosts the API with test JWT settings and an isolated SQLite database.
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        private readonly Dictionary<string, string?> _originalEnvironment = new();

        public ApiFactory()
        {
            SetEnvironment("Authentication__Authority", Issuer);
            SetEnvironment("Authentication__Audience", Audience);
            SetEnvironment("ConnectionStrings__MeetingRooms", $"Data Source={_databasePath};Default Timeout=5;Pooling=False");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                    configuration.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                    options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                foreach (var (key, value) in _originalEnvironment)
                    Environment.SetEnvironmentVariable(key, value);

                if (File.Exists(_databasePath))
                    File.Delete(_databasePath);
            }
        }

        private void SetEnvironment(string key, string value)
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
