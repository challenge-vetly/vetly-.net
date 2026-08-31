using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.Documento;
using Vetly.Application.Exceptions;
using Vetly.Application.Factories;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Conteudo dos documentos clinicos (RN-083/RN-086).
///
/// Gerar documento e formatar o estado final aprovado pelo veterinario. A factory
/// formata; ela nao infere nada de novo.
/// </summary>
public class DocumentoConteudoTests
{
    private static ContextoDoDocumentoDto Contexto(
        ConteudoDoProntuarioDto? conteudo = null,
        decimal? pesoKg = 28m,
        TipoAtestado? subtipo = null) => new()
    {
        ConsultaId = Guid.NewGuid(),
        DataDoAtendimento = new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc),
        Modalidade = ModalidadeAtendimento.Presencial,
        VeterinarioNome = "Dra. Marina Costa",
        Crmv = "12345-SP",
        UfAtuacao = "SP",
        AnimalNome = "Thor",
        Especie = "Canino",
        Raca = "SRD",
        DataNascimento = new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        PesoKg = pesoKg,
        Sexo = "Macho",
        TutorNome = "Ana Souza",
        SubtipoAtestado = subtipo,
        ValorDoAtendimento = 180.00m,
        Conteudo = conteudo ?? new ConteudoDoProntuarioDto
        {
            Anamnese = "Vomito ha dois dias, sem diarreia.",
            ExameFisico = "Mucosas normocoradas, abdome sensivel a palpacao.",
            HipotesesDiagnosticas = ["Gastrite aguda", "Corpo estranho"],
            Conduta = "Jejum de 12h. Ondansetrona 0,5 mg/kg a cada 12h por 3 dias.",
            Orientacoes = "Oferecer agua em pequenas quantidades. Retornar se o vomito persistir."
        }
    };

    // ── Identificação comum (RN-083) ─────────────────────────────────────────

    [Fact]
    public void TodoDocumento_IdentificaOProfissionalHabilitadoEOAnimal()
    {
        var contexto = Contexto();

        var documento = new ProntuarioFactory().Criar(contexto);

        // Documento clinico sem CRMV nao serve para nada fora do app
        Assert.Contains("CRMV 12345-SP/SP", documento.Conteudo);
        Assert.Contains("Dra. Marina Costa", documento.Conteudo);
        Assert.Contains("Thor", documento.Conteudo);
        Assert.Contains("Ana Souza", documento.Conteudo);
        Assert.Contains("28 kg", documento.Conteudo);
    }

    [Fact]
    public void TodoDocumento_TrazAIdadeCalculadaNaDataDoAtendimento()
    {
        var documento = new ProntuarioFactory().Criar(Contexto());

        // Nascido em 03/2022, atendido em 08/2026
        Assert.Contains("4 ano(s)", documento.Conteudo);
    }

    // ── Prontuário ───────────────────────────────────────────────────────────

    [Fact]
    public void Prontuario_RegistraOAtendimentoNaOrdemClinica()
    {
        var documento = new ProntuarioFactory().Criar(Contexto());

        var texto = documento.Conteudo!;

        Assert.True(texto.IndexOf("ANAMNESE") < texto.IndexOf("EXAME FISICO"));
        Assert.True(texto.IndexOf("EXAME FISICO") < texto.IndexOf("HIPOTESES DIAGNOSTICAS"));
        Assert.True(texto.IndexOf("HIPOTESES DIAGNOSTICAS") < texto.IndexOf("CONDUTA"));

        // Hipoteses saem numeradas, da mais provavel a menos
        Assert.Contains("1. Gastrite aguda", texto);
        Assert.Contains("2. Corpo estranho", texto);
    }

    [Fact]
    public void Prontuario_OmiteSecaoSemConteudo()
    {
        var documento = new ProntuarioFactory().Criar(Contexto(new ConteudoDoProntuarioDto
        {
            Anamnese = "Consulta de rotina, sem queixas."
        }));

        // Secao vazia impressa em branco parece documento incompleto, e nao e
        Assert.DoesNotContain("EXAME FISICO", documento.Conteudo);
        Assert.DoesNotContain("HIPOTESES DIAGNOSTICAS", documento.Conteudo);
        Assert.Contains("ANAMNESE", documento.Conteudo);
    }

    // ── Receita (RN-081/RN-083/RN-087) ───────────────────────────────────────

    [Fact]
    public void Receita_SaiDaCondutaAprovadaEMarcaUsoVeterinario()
    {
        var documento = new ReceitaVeterinariaFactory().Criar(Contexto());

        Assert.Contains("USO VETERINARIO", documento.Conteudo);
        Assert.Contains("Ondansetrona 0,5 mg/kg", documento.Conteudo);
        Assert.Equal(TipoDocumento.ReceitaVeterinaria, documento.TipoDocumento);
    }

    [Fact]
    public void Receita_SemConduta_NaoEEmitida()
    {
        var semConduta = new ConteudoDoProntuarioDto
        {
            Anamnese = "Retorno de acompanhamento.",
            HipotesesDiagnosticas = ["Quadro resolvido"]
        };

        // Receita sem prescricao nao e receita: emitir vazia pareceria valida
        var ex = Assert.Throws<BusinessRuleException>(
            () => new ReceitaVeterinariaFactory().Criar(Contexto(semConduta)));

        Assert.Equal("RN-083", ex.Codigo);
    }

    [Fact]
    public void Receita_SemPesoRegistrado_AvisaQuemVaiDispensar()
    {
        var documento = new ReceitaVeterinariaFactory().Criar(Contexto(pesoKg: null));

        // Dose se calcula sobre o peso: quem dispensa precisa poder conferir (RN-081)
        Assert.Contains("peso do animal nao registrado", documento.Conteudo);
    }

    [Fact]
    public void Receita_ComPeso_NaoPoluiODocumentoComAviso()
    {
        var documento = new ReceitaVeterinariaFactory().Criar(Contexto());

        Assert.DoesNotContain("peso do animal nao registrado", documento.Conteudo);
    }

    // ── Atestado (RN-086) ────────────────────────────────────────────────────

    [Fact]
    public void Atestado_DeObito_RegistraAMorte()
    {
        var documento = new AtestadoFactory().Criar(Contexto(subtipo: TipoAtestado.Obito));

        Assert.Equal(TipoAtestado.Obito, documento.Subtipo);
        Assert.Contains("obito do animal Thor", documento.Conteudo);
    }

    [Fact]
    public void Atestado_DeSaude_AfirmaOExameClinicoENaoOObito()
    {
        var documento = new AtestadoFactory().Criar(Contexto(subtipo: TipoAtestado.Saude));

        Assert.Contains("submetido a exame clinico", documento.Conteudo);
        Assert.DoesNotContain("obito", documento.Conteudo);
    }

    [Fact]
    public void Atestado_DeVacinacao_FalaDaSituacaoVacinal()
    {
        var documento = new AtestadoFactory().Criar(Contexto(subtipo: TipoAtestado.Vacinacao));

        Assert.Contains("situacao vacinal", documento.Conteudo);
    }

    [Fact]
    public void Atestado_SemSubtipo_CaiNoMaisConservador()
    {
        var documento = new AtestadoFactory().Criar(Contexto());

        // Saude nao afirma obito nem comprova vacina que pode nao ter sido aplicada
        Assert.Equal(TipoAtestado.Saude, documento.Subtipo);
    }

    // ── Nota fiscal ──────────────────────────────────────────────────────────

    [Fact]
    public void NotaFiscal_TrazOValorEDizQueNaoSubstituiDocumentoFiscal()
    {
        var documento = new NotaFiscalFactory().Criar(Contexto());

        Assert.Contains("180,00", documento.Conteudo);

        // Deixar ambiguo seria pior que nao emitir
        Assert.Contains("Nao substitui documento fiscal", documento.Conteudo);
    }

    [Fact]
    public void NotaFiscal_SemValorApurado_DizQueNaoFoiInformado()
    {
        var contexto = Contexto();
        contexto.ValorDoAtendimento = null;

        var documento = new NotaFiscalFactory().Criar(contexto);

        Assert.Contains("nao informado", documento.Conteudo);
    }
}
