using MobileAlert.Api.Services;
using Xunit;

namespace MobileAlert.Api.Tests.Unit;

/// <summary>ApiKeyAuth.Hash es lo único entre la API key real de un cuartel
/// (que solo se ve una vez, al crearla) y lo que queda guardado en
/// ApiKeyRecord.KeyHash — si esto se rompe, o deja de ser determinístico, o
/// dos keys distintas empiezan a resolver al mismo hash, ninguna API key
/// vuelve a funcionar (o peor, una le abre la puerta a otra).</summary>
public class ApiKeyAuthTests
{
    [Fact]
    public void Hash_SameInput_AlwaysProducesSameHash()
    {
        var a = ApiKeyAuth.Hash("mi-key-secreta-de-cuartel");
        var b = ApiKeyAuth.Hash("mi-key-secreta-de-cuartel");

        // Tiene que ser así: ApiKeyAuthenticationHandler hashea la key que
        // llega en cada request y la compara contra KeyHash — si el hash de
        // la MISMA key variara, ninguna key volvería a autenticar después
        // de la primera vez.
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_DifferentInput_ProducesDifferentHash()
    {
        Assert.NotEqual(ApiKeyAuth.Hash("key-del-cuartel-a"), ApiKeyAuth.Hash("key-del-cuartel-b"));
    }

    [Fact]
    public void Hash_ReturnsLowercaseHexSha256_AndNeverLeaksTheRawKey()
    {
        var hash = ApiKeyAuth.Hash("una-key-cualquiera-bien-larga");

        Assert.Equal(64, hash.Length); // SHA-256 en hex = 32 bytes = 64 chars
        Assert.Equal(hash, hash.ToLowerInvariant());
        Assert.DoesNotContain("una-key-cualquiera-bien-larga", hash);
    }
}
