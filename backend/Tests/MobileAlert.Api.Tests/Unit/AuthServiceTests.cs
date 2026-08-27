using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Dtos;
using MobileAlert.Api.Services;
using Xunit;

namespace MobileAlert.Api.Tests.Unit;

/// <summary>AuthService decide, por institución, si la contraseña se valida
/// acá (BCrypt local) o se reenvía al backend propio del cuartel — ver
/// AuthService.LoginAsync. Las dos ramas tienen su propio código y su propio
/// modo de fallar, así que se prueban por separado.</summary>
public class AuthServiceTests
{
    private const string Secret = "unit-test-secret-de-al-menos-32-caracteres";

    private sealed class StubHandler(HttpStatusCode statusCode, object? body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(statusCode);
            if (body is not null)
            {
                response.Content = JsonContent.Create(body);
            }
            return response;
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static AuthService BuildService(AppDbContext db, HttpMessageHandler? delegatedLoginHandler = null)
    {
        var tokens = new JwtTokenService(Options.Create(new JwtOptions { Secret = Secret, Issuer = "mobile-alert-api" }));
        var httpClient = new HttpClient(delegatedLoginHandler ?? new StubHandler(HttpStatusCode.ServiceUnavailable, null));
        return new AuthService(db, tokens, new SingleClientFactory(httpClient));
    }

    [Fact]
    public async Task LoginAsync_LocalInstitution_CorrectPassword_ReturnsTokenAndFirefighter()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution { Code = "TEST-CENTRAL", Name = "Test Central", PrimaryColor = "#000" };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();
        db.Firefighters.Add(new Firefighter
        {
            Name = "Juan Pérez",
            Username = "juan",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            InstitutionId = institution.Id,
        });
        await db.SaveChangesAsync();

        var result = await BuildService(db).LoginAsync(new LoginRequestDto("TEST-CENTRAL", "juan", "1234"));

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("Juan Pérez", result.Firefighter.Name);
    }

    [Fact]
    public async Task LoginAsync_LocalInstitution_WrongPassword_ThrowsInvalidLogin()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution { Code = "TEST-CENTRAL", Name = "Test Central", PrimaryColor = "#000" };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();
        db.Firefighters.Add(new Firefighter
        {
            Name = "Juan Pérez",
            Username = "juan",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            InstitutionId = institution.Id,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidLoginException>(() =>
            BuildService(db).LoginAsync(new LoginRequestDto("TEST-CENTRAL", "juan", "contraseña-incorrecta")));
    }

    [Fact]
    public async Task LoginAsync_UnknownInstitutionCode_ThrowsInvalidLogin()
    {
        await using var db = InMemoryDb.New();

        await Assert.ThrowsAsync<InvalidLoginException>(() =>
            BuildService(db).LoginAsync(new LoginRequestDto("NO-EXISTE", "juan", "1234")));
    }

    [Fact]
    public async Task LoginAsync_UnknownUsername_ThrowsInvalidLogin()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution { Code = "TEST-CENTRAL", Name = "Test Central", PrimaryColor = "#000" };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidLoginException>(() =>
            BuildService(db).LoginAsync(new LoginRequestDto("TEST-CENTRAL", "no-existe", "1234")));
    }

    [Fact]
    public async Task LoginAsync_DelegatedInstitution_ForwardsCredentials_AndCreatesLocalFirefighter()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution
        {
            Code = "TEST-DELEGADA",
            Name = "Test Delegada",
            PrimaryColor = "#000",
            LoginBackendUrl = "https://cuartel.example.org/login",
        };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();

        var handler = new StubHandler(HttpStatusCode.OK, new { ExternalId = "ext-1", Name = "María Gómez" });
        var service = BuildService(db, handler);

        var result = await service.LoginAsync(new LoginRequestDto("TEST-DELEGADA", "maria", "lo-que-sea"));

        // Le reenvió exactamente usuario/contraseña que mandó la app, tal
        // cual — es la contraseña real del bombero, no la valida acá.
        Assert.Equal("https://cuartel.example.org/login", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("\"maria\"", handler.LastRequestBody);
        Assert.Contains("\"lo-que-sea\"", handler.LastRequestBody);

        Assert.Equal("María Gómez", result.Firefighter.Name);
        // No queda contraseña guardada para un bombero de institución
        // delegada — la valida siempre el backend del cuartel, nunca acá.
        var stored = await db.Firefighters.SingleAsync(f => f.Username == "maria");
        Assert.Null(stored.PasswordHash);
    }

    [Fact]
    public async Task LoginAsync_DelegatedInstitution_ExistingFirefighter_SyncsNameOnEachLogin()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution
        {
            Code = "TEST-DELEGADA",
            Name = "Test Delegada",
            PrimaryColor = "#000",
            LoginBackendUrl = "https://cuartel.example.org/login",
        };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();
        db.Firefighters.Add(new Firefighter { Name = "Nombre Viejo", Username = "maria", InstitutionId = institution.Id });
        await db.SaveChangesAsync();

        var handler = new StubHandler(HttpStatusCode.OK, new { ExternalId = "ext-1", Name = "Nombre Nuevo" });
        await BuildService(db, handler).LoginAsync(new LoginRequestDto("TEST-DELEGADA", "maria", "1234"));

        var stored = await db.Firefighters.SingleAsync(f => f.Username == "maria");
        Assert.Equal("Nombre Nuevo", stored.Name);
    }

    [Fact]
    public async Task LoginAsync_DelegatedInstitution_ExternalBackendRejects_ThrowsInvalidLogin()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution
        {
            Code = "TEST-DELEGADA",
            Name = "Test Delegada",
            PrimaryColor = "#000",
            LoginBackendUrl = "https://cuartel.example.org/login",
        };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();

        var handler = new StubHandler(HttpStatusCode.Unauthorized, null);
        var service = BuildService(db, handler);

        await Assert.ThrowsAsync<InvalidLoginException>(() =>
            service.LoginAsync(new LoginRequestDto("TEST-DELEGADA", "maria", "mal")));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulado: backend del cuartel inalcanzable");
    }

    [Fact]
    public async Task LoginAsync_DelegatedInstitution_BackendUnreachable_ThrowsInvalidLogin_NotRawException()
    {
        await using var db = InMemoryDb.New();
        var institution = new Institution
        {
            Code = "TEST-DELEGADA",
            Name = "Test Delegada",
            PrimaryColor = "#000",
            LoginBackendUrl = "https://cuartel.example.org/login",
        };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();

        // El HttpClient revienta con HttpRequestException (simula el
        // backend del cuartel caído/inalcanzable) — le importa al bombero
        // un mensaje claro ("intentá de nuevo"), no que esa excepción de
        // red se cuele sin capturar hasta la respuesta HTTP.
        var service = BuildService(db, new ThrowingHandler());

        await Assert.ThrowsAsync<InvalidLoginException>(() =>
            service.LoginAsync(new LoginRequestDto("TEST-DELEGADA", "maria", "1234")));
    }
}
