using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um documento clínico gerado na plataforma Vetly.
/// Pode estar vinculado a uma consulta ou a uma internação.
/// RN-087: todo documento deve ser assinado digitalmente antes da finalização da consulta.
/// </summary>
public class Documento
{
    /// <summary>Identificador único do documento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Id da consulta à qual o documento está vinculado.
    /// Nulo quando o documento é gerado a partir de uma internação.
    /// </summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>
    /// Id da internação à qual o documento está vinculado.
    /// Nulo quando o documento é gerado a partir de uma consulta.
    /// </summary>
    public Guid? InternacaoId { get; private set; }

    /// <summary>Tipo do documento (Prontuário, Receita, Atestado ou Nota Fiscal).</summary>
    [Required]
    public TipoDocumento TipoDocumento { get; private set; }

    /// <summary>
    /// Versão do documento. Começa em 1 e é incrementada a cada correção.
    /// Garante rastreabilidade do histórico de alterações.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Versao { get; private set; }

    /// <summary>Data e hora de geração do documento.</summary>
    public DateTime DataGeracao { get; private set; }

    /// <summary>CRMV do veterinário que assinou o documento (signatário legal).</summary>
    [Required]
    [MaxLength(15)]
    public string CrmvSignatario { get; private set; }

    /// <summary>
    /// Indica se o documento foi assinado digitalmente.
    /// RN-087: a assinatura é pré-requisito para finalização da consulta.
    /// </summary>
    public bool AssinadoDigitalmente { get; private set; }

    /// <summary>Id do documento original quando este é uma versão corrigida (RN-088).</summary>
    public Guid? VersaoOriginalId { get; private set; }

    /// <summary>Data e hora em que a correção foi realizada (RN-088).</summary>
    public DateTime? DataCorrecao { get; private set; }

    /// <summary>CRMV do veterinário que solicitou a correção (RN-088).</summary>
    [MaxLength(15)]
    public string? CrmvSolicitanteCorrecao { get; private set; }

    // ── Conteúdo, assinatura e publicação (RN-083, RN-087, RN-090) ───────────

    /// <summary>
    /// Conteúdo do documento. Até esta migration a tabela guardava só metadados e o
    /// conteúdo não persistia — sem ele o documento não existe de fato para o
    /// Responsável no board do pet (RN-090).
    /// A geração parte do estado final aprovado pelo veterinário: é formatação,
    /// não nova inferência clínica (RN-083).
    /// </summary>
    public string? Conteudo { get; private set; }

    /// <summary>Id da mídia com o PDF renderizado do documento, no storage de objetos.</summary>
    public Guid? PdfMidiaId { get; private set; }

    /// <summary>
    /// Subtipo do atestado (saúde, óbito ou transporte). Nulo nos demais tipos de
    /// documento (RN-086).
    /// </summary>
    public TipoAtestado? Subtipo { get; private set; }

    /// <summary>
    /// Método da assinatura. No MVP é o nome digitado; em produção, certificado
    /// ICP-Brasil vinculado ao CRMV (RN-087).
    /// </summary>
    [MaxLength(50)]
    public string? AssinaturaMetodo { get; private set; }

    /// <summary>Carimbo textual da assinatura, exibido no documento (RN-087).</summary>
    [MaxLength(300)]
    public string? AssinaturaCarimbo { get; private set; }

    /// <summary>
    /// Data em que o documento foi publicado no board do pet. Nulo enquanto o
    /// documento existe mas ainda não chegou ao Responsável (RN-011/RN-090).
    /// </summary>
    public DateTime? PublicadoEm { get; private set; }

    /// <summary>Data em que o Responsável abriu o documento no app.</summary>
    public DateTime? LidoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Documento()
    {
        CrmvSignatario = null!;
    }

    /// <summary>
    /// Cria um novo documento associado a uma consulta ou internação.
    /// O documento começa na versão 1 e não assinado.
    /// </summary>
    public Documento(TipoDocumento tipo, string crmvSignatario, Guid? consultaId = null, Guid? internacaoId = null)
    {
        Id = Guid.NewGuid();
        TipoDocumento = tipo;
        CrmvSignatario = crmvSignatario;
        ConsultaId = consultaId;
        InternacaoId = internacaoId;
        Versao = 1;
        DataGeracao = DateTime.UtcNow;
    }

    /// <summary>Registra a assinatura digital do documento (RN-087).</summary>
    public void Assinar() => AssinadoDigitalmente = true;

    /// <summary>
    /// Indica se este tipo de documento precisa de assinatura para valer (RN-087).
    ///
    /// Receita e atestado precisam: saem da plataforma e fazem uma afirmacao em nome
    /// de um profissional habilitado — sem assinatura, quem os recebe nao tem como
    /// saber de quem vieram. Prontuario e o registro interno do atendimento, e a nota
    /// fiscal e recibo: nenhum dos dois faz essa afirmacao para fora, e exigir
    /// assinatura neles travaria consultas que nao prescreveram nada.
    /// </summary>
    public bool ExigeAssinatura() =>
        TipoDocumento is TipoDocumento.ReceitaVeterinaria or TipoDocumento.Atestado;

    /// <summary>Verdadeiro quando o documento precisa de assinatura e ainda não a tem.</summary>
    public bool PendenteDeAssinatura() => ExigeAssinatura() && !AssinadoDigitalmente;

    /// <summary>
    /// Registra a assinatura com o método e o carimbo produzidos pelo adaptador de
    /// assinatura (RN-087). No MVP o método é o nome digitado, que não habilita
    /// dispensação externa de controlados.
    /// </summary>
    public void RegistrarAssinatura(string metodo, string carimbo)
    {
        if (string.IsNullOrWhiteSpace(metodo))
            throw new ArgumentException("O método da assinatura é obrigatório.", nameof(metodo));

        AssinaturaMetodo = metodo;
        AssinaturaCarimbo = carimbo;
        AssinadoDigitalmente = true;
    }

    /// <summary>
    /// Grava o conteúdo do documento, formatado a partir do estado final aprovado
    /// pelo veterinário (RN-083).
    /// </summary>
    public void RegistrarConteudo(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ArgumentException("O conteúdo do documento não pode ser vazio.", nameof(conteudo));

        Conteudo = conteudo;
    }

    /// <summary>Vincula o PDF renderizado do documento, guardado no storage de objetos.</summary>
    public void AnexarPdf(Guid pdfMidiaId) => PdfMidiaId = pdfMidiaId;

    /// <summary>Define o subtipo do atestado. Só faz sentido em documentos do tipo Atestado (RN-086).</summary>
    public void DefinirSubtipoAtestado(TipoAtestado subtipo)
    {
        if (TipoDocumento != TipoDocumento.Atestado)
            throw new InvalidOperationException("Somente atestados possuem subtipo.");

        Subtipo = subtipo;
    }

    /// <summary>
    /// Publica o documento no board do pet (RN-011/RN-090). Idempotente: republicar
    /// preserva a data original, que é a referência da notificação ao Responsável.
    /// </summary>
    public void Publicar(DateTime publicadoEm) => PublicadoEm ??= publicadoEm;

    /// <summary>Registra a leitura do documento pelo Responsável no app.</summary>
    public void MarcarComoLido(DateTime lidoEm) => LidoEm ??= lidoEm;

    /// <summary>Incrementa a versão do documento ao criar uma correção.</summary>
    public void IncrementarVersao() => Versao++;

    /// <summary>
    /// Marca este documento como versão corrigida de outro (RN-088).
    /// Vincula ao documento original e registra o responsável pela correção.
    /// </summary>
    public void Corrigir(Guid versaoOriginalId, DateTime dataCorrecao, string crmvSolicitante)
    {
        VersaoOriginalId = versaoOriginalId;
        DataCorrecao = dataCorrecao;
        CrmvSolicitanteCorrecao = crmvSolicitante;
    }
}
