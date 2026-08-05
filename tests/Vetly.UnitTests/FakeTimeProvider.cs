namespace Vetly.UnitTests;

/// <summary>
/// TimeProvider controlável para testes de janelas de tempo (lock de checkout, no-show,
/// etc.) sem depender do relógio real do sistema.
/// </summary>
public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _agora;

    public FakeTimeProvider(DateTime agoraInicial) => _agora = agoraInicial;

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan intervalo) => _agora += intervalo;
}
