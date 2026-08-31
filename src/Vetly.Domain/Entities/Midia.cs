using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Um arquivo no storage de objetos (§2.6, §11).
///
/// A API nunca carrega os bytes: ela registra a mídia, entrega uma URL assinada e o
/// app envia direto ao storage. Áudio de consulta e imagem clínica não passam pelo
/// processo da API.
///
/// O <c>MidiaId</c> é o que viaja nos payloads de negócio — nunca a URL, que expira.
/// </summary>
public class Midia
{
    /// <summary>Validade padrão de uma URL assinada.</summary>
    public static readonly TimeSpan ValidadeDaUrl = TimeSpan.FromMinutes(15);

    /// <summary>Identificador da mídia (chave primária). É ele que viaja nos payloads.</summary>
    public Guid Id { get; private set; }

    /// <summary>Natureza do arquivo.</summary>
    [Required]
    public TipoMidia Tipo { get; private set; }

    /// <summary>Caminho do objeto no storage.</summary>
    [Required]
    [MaxLength(300)]
    public string ChaveStorage { get; private set; }

    /// <summary>Tipo MIME declarado no registro.</summary>
    [Required]
    [MaxLength(100)]
    public string ContentType { get; private set; }

    /// <summary>Situação do arquivo.</summary>
    [Required]
    public StatusMidia Status { get; private set; }

    /// <summary>Responsável dono da mídia, quando aplicável.</summary>
    public Guid? TutorId { get; private set; }

    /// <summary>Consulta a que a mídia pertence, quando aplicável.</summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>Tamanho em bytes, conhecido depois do upload.</summary>
    public long? TamanhoBytes { get; private set; }

    public DateTime CriadaEm { get; private set; }

    /// <summary>
    /// Até quando o arquivo é guardado. Áudio de consulta tem 30 dias (P-06);
    /// conteúdo clínico não expira, por guarda regulatória (RN-062).
    /// </summary>
    public DateTime? RetencaoAte { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Midia()
    {
        ChaveStorage = null!;
        ContentType = null!;
    }

    /// <summary>Registra uma mídia e reserva o lugar dela no storage.</summary>
    public Midia(TipoMidia tipo, string contentType, Guid? tutorId = null, Guid? consultaId = null)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("O content type é obrigatório.", nameof(contentType));

        Id = Guid.NewGuid();
        Tipo = tipo;
        ContentType = contentType;
        TutorId = tutorId;
        ConsultaId = consultaId;
        Status = StatusMidia.AguardandoUpload;
        CriadaEm = DateTime.UtcNow;
        RetencaoAte = CalcularRetencao(tipo, CriadaEm);

        // A chave inclui o tipo para que o storage fique navegavel por prefixo
        ChaveStorage = $"{tipo.ToString().ToLowerInvariant()}/{CriadaEm:yyyy/MM}/{Id}";
    }

    /// <summary>Marca o arquivo como enviado e disponível.</summary>
    public void ConfirmarUpload(long tamanhoBytes)
    {
        if (tamanhoBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(tamanhoBytes), "O arquivo enviado está vazio.");

        Status = StatusMidia.Disponivel;
        TamanhoBytes = tamanhoBytes;
    }

    /// <summary>Marca o arquivo como removido do storage.</summary>
    public void MarcarComoRemovida() => Status = StatusMidia.Removida;

    /// <summary>Verdadeiro quando a mídia pode ser lida.</summary>
    public bool Disponivel() => Status == StatusMidia.Disponivel;

    /// <summary>
    /// Retenção por tipo. O áudio bruto da consulta é o único com prazo curto: 30 dias
    /// para reprocessamento e depois some (P-06). O resto é conteúdo clínico e fica.
    /// </summary>
    private static DateTime? CalcularRetencao(TipoMidia tipo, DateTime criadaEm) => tipo switch
    {
        TipoMidia.AudioConsulta => criadaEm.AddDays(30),
        _ => null
    };
}
