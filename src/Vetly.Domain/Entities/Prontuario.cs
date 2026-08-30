using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Registro clínico de uma consulta. Suporta auto-relacionamento para versionamento de correções.
/// RN-088/RN-089: correções dentro de 24h não exigem justificativa; após 24h, é obrigatória.
/// </summary>
public class Prontuario
{
    /// <summary>Identificador único do prontuário (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id da consulta à qual este prontuário pertence. Chave estrangeira para TB_CONSULTA.</summary>
    [Required]
    public Guid ConsultaId { get; private set; }

    /// <summary>Id do animal ao qual este prontuário pertence. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Dados clínicos registrados pelo veterinário (anamnese, diagnóstico, conduta).</summary>
    [Required(ErrorMessage = "Os dados clínicos são obrigatórios.")]
    public string DadosClinicos { get; private set; }

    /// <summary>
    /// Id do prontuário original do qual este é uma correção.
    /// Nulo quando é o registro original, preenchido quando é uma versão corrigida.
    /// </summary>
    public Guid? VersaoOriginalId { get; private set; }

    /// <summary>Data e hora em que a correção foi registrada.</summary>
    public DateTime? DataCorrecao { get; private set; }

    /// <summary>
    /// Justificativa para a correção.
    /// Obrigatória quando a correção ocorre após 24h do registro original (RN-089).
    /// </summary>
    [MaxLength(1000)]
    public string? JustificativaCorrecao { get; private set; }

    /// <summary>CRMV do veterinário que solicitou a correção. Pode diferir do veterinário original.</summary>
    [MaxLength(15)]
    public string? CrmvSolicitanteCorrecao { get; private set; }

    /// <summary>Data e hora de criação do prontuário original.</summary>
    public DateTime DataCriacao { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Prontuario()
    {
        DadosClinicos = null!;
    }

    /// <summary>Cria um novo prontuário original (sem versionamento).</summary>
    public Prontuario(Guid consultaId, Guid animalId, string dadosClinicos)
    {
        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        AnimalId = animalId;
        DadosClinicos = dadosClinicos;
        DataCriacao = DateTime.UtcNow;
    }

    /// <summary>
    /// Cria um novo prontuário como correção deste, mantendo rastreabilidade via VersaoOriginalId.
    /// A justificativa é opcional dentro de 24h e obrigatória após (ver <see cref="ExigeJustificativa"/>).
    /// </summary>
    public Prontuario CriarCorrecao(string novosDadosClinicos, string? justificativa, string crmvSolicitante)
    {
        return new Prontuario(ConsultaId, AnimalId, novosDadosClinicos)
        {
            VersaoOriginalId = Id,       // aponta para o prontuário que está sendo corrigido
            DataCorrecao = DateTime.UtcNow,
            JustificativaCorrecao = justificativa,
            CrmvSolicitanteCorrecao = crmvSolicitante
        };
    }

    /// <summary>
    /// Indica se a correção deste prontuário exige justificativa.
    /// Verdadeiro quando já passou mais de 24h da criação (RN-089).
    /// </summary>
    public bool ExigeJustificativa() =>
        DataCriacao < DateTime.UtcNow.AddHours(-24);
}
