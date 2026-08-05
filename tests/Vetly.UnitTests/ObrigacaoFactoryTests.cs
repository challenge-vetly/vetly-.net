using Vetly.Application.Factories;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios das factories de calendario de obrigacoes do pet (RN-069).
/// Cobre a selecao por especie e o fallback generico sempre aplicavel.
/// </summary>
public class ObrigacaoFactoryTests
{
    private static readonly DateTime DataCadastro = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ObrigacaoCaninaFactory_Aplicavel_SoParaCanino()
    {
        var factory = new ObrigacaoCaninaFactory();

        Assert.True(factory.Aplicavel("Canino"));
        Assert.True(factory.Aplicavel("canino")); // case-insensitive
        Assert.False(factory.Aplicavel("Felino"));
    }

    [Fact]
    public void ObrigacaoCaninaFactory_GerarCalendario_CriaQuatroObrigacoesPendentes()
    {
        var factory = new ObrigacaoCaninaFactory();
        var animalId = Guid.NewGuid();

        var obrigacoes = factory.GerarCalendario(animalId, DataCadastro).ToList();

        Assert.Equal(4, obrigacoes.Count);
        Assert.All(obrigacoes, o => Assert.Equal(StatusObrigacao.Pendente, o.Status));
        Assert.All(obrigacoes, o => Assert.Equal(animalId, o.AnimalId));
        Assert.Contains(obrigacoes, o => o.Tipo == TipoObrigacao.Vacina);
        Assert.Contains(obrigacoes, o => o.Tipo == TipoObrigacao.Vermifugo);
        Assert.Contains(obrigacoes, o => o.Tipo == TipoObrigacao.CheckUp);
        Assert.Contains(obrigacoes, o => o.Tipo == TipoObrigacao.Retorno);
    }

    [Fact]
    public void ObrigacaoFelinaFactory_Aplicavel_SoParaFelino()
    {
        var factory = new ObrigacaoFelinaFactory();

        Assert.True(factory.Aplicavel("Felino"));
        Assert.False(factory.Aplicavel("Canino"));
    }

    [Fact]
    public void ObrigacaoGenericaFactory_Aplicavel_SempreTrue()
    {
        var factory = new ObrigacaoGenericaFactory();

        Assert.True(factory.Aplicavel("Ave"));
        Assert.True(factory.Aplicavel("Réptil"));
        Assert.True(factory.Aplicavel("Canino"));
    }

    [Fact]
    public void SelecaoPorEnumerable_EspecieSemFactoryDedicada_CaiNoFallbackGenerico()
    {
        IEnumerable<IObrigacaoFactory> factories =
            [new ObrigacaoCaninaFactory(), new ObrigacaoFelinaFactory(), new ObrigacaoGenericaFactory()];

        var selecionada = factories.First(f => f.Aplicavel("Ave"));

        Assert.IsType<ObrigacaoGenericaFactory>(selecionada);
    }
}
