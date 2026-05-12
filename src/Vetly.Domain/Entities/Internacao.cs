using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa uma internação hospitalar de um animal na plataforma Vetly.
/// O valor total é apurado ao longo dos dias com base nos procedimentos realizados.
/// </summary>
public class Internacao
{
    /// <summary>Identificador único da internação (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do animal internado. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Id do veterinário responsável pela internação. Chave estrangeira para TB_VETERINARIO.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>
    /// Valor da caução cobrado na abertura da internação.
    /// Serve como garantia e é descontado do valor total apurado na alta.
    /// </summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "O valor da caução deve ser positivo.")]
    public decimal ValorCaucao { get; private set; }

    /// <summary>
    /// Valor total acumulado dos procedimentos realizados durante a internação.
    /// Apurado diariamente pelo InternacaoService.
    /// </summary>
    public decimal ValorTotalApurado { get; private set; }

    /// <summary>Data e hora de abertura da internação.</summary>
    public DateTime DataAbertura { get; private set; }

    /// <summary>Data e hora da alta. Nulo enquanto o animal ainda está internado.</summary>
    public DateTime? DataAlta { get; private set; }

    /// <summary>
    /// Procedimentos realizados a cada dia, armazenados como JSON no banco.
    /// Exemplo: [{"data":"2025-01-01","procedimento":"Soro IV","valor":150.00}]
    /// </summary>
    public string ProcedimentosDiarios { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Internacao()
    {
        ProcedimentosDiarios = null!;
    }

    /// <summary>Abre uma nova internação com caução e inicia o registro de procedimentos vazio.</summary>
    public Internacao(Guid animalId, Guid veterinarioId, decimal valorCaucao)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        ValorCaucao = valorCaucao;
        DataAbertura = DateTime.UtcNow;
        ProcedimentosDiarios = "[]"; // JSON array vazio; serializado pelo serviço
    }

    /// <summary>Atualiza o JSON de procedimentos diários.</summary>
    public void RegistrarProcedimentoDiario(string procedimentoJson)
    {
        ProcedimentosDiarios = procedimentoJson;
    }

    /// <summary>Acumula um valor ao total apurado da internação.</summary>
    public void ApurarValor(decimal valor) => ValorTotalApurado += valor;

    /// <summary>
    /// Encerra a internação registrando a data de alta.
    /// Lança exceção se a internação já foi encerrada anteriormente.
    /// </summary>
    public void DarAlta()
    {
        if (DataAlta.HasValue)
            throw new InvalidOperationException("Internação já encerrada anteriormente.");

        DataAlta = DateTime.UtcNow;
    }

    /// <summary>Retorna verdadeiro se o animal ainda está internado (sem data de alta).</summary>
    public bool EstaAtiva() => !DataAlta.HasValue;

    /// <summary>
    /// Calcula o número de dias internado.
    /// Usa DataAlta se disponível, senão usa a data atual.
    /// Mínimo de 1 dia (diária do dia de abertura sempre é cobrada).
    /// </summary>
    public int DiasInternado() =>
        (int)((DataAlta ?? DateTime.UtcNow) - DataAbertura).TotalDays + 1;
}
