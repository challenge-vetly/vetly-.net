using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um exame laboratorial ou de imagem solicitado durante o atendimento.
/// O resultado só pode ser liberado ao responsavel após validação do veterinário.
/// </summary>
public class Exame
{
    /// <summary>Identificador único do exame (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do animal ao qual o exame pertence. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Id do veterinário solicitante. Chave estrangeira para TB_VETERINARIO.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Tipo do exame solicitado (ex: Hemograma, Raio-X, Ultrassom).</summary>
    [Required(ErrorMessage = "O tipo de solicitação é obrigatório.")]
    [MaxLength(200)]
    public string TipoSolicitacao { get; private set; }

    /// <summary>
    /// Resultado do exame após processamento pelo laboratório.
    /// Nulo enquanto o resultado ainda não foi registrado.
    /// </summary>
    public string? Resultado { get; private set; }

    /// <summary>
    /// Indica se o resultado foi liberado para visualização pelo responsavel.
    /// Só pode ser liberado após o resultado estar disponível.
    /// </summary>
    public bool LiberadoAoResponsavel { get; private set; }

    /// <summary>Data e hora em que o exame foi solicitado.</summary>
    public DateTime DataSolicitacao { get; private set; }

    /// <summary>Data e hora em que o resultado foi registrado. Nulo enquanto aguarda resultado.</summary>
    public DateTime? DataResultado { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Exame()
    {
        TipoSolicitacao = null!;
    }

    /// <summary>Cria uma nova solicitação de exame.</summary>
    public Exame(Guid animalId, Guid veterinarioId, string tipoSolicitacao)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        TipoSolicitacao = tipoSolicitacao;
        DataSolicitacao = DateTime.UtcNow;
    }

    /// <summary>Registra o resultado do exame e marca a data de conclusão.</summary>
    public void RegistrarResultado(string resultado)
    {
        Resultado = resultado;
        DataResultado = DateTime.UtcNow;
    }

    /// <summary>
    /// Libera o resultado para visualização pelo responsavel.
    /// Lança exceção se o resultado ainda não foi registrado.
    /// </summary>
    public void LiberarAoResponsavel()
    {
        if (string.IsNullOrWhiteSpace(Resultado))
            throw new InvalidOperationException("Não é possível liberar exame sem resultado registrado.");

        LiberadoAoResponsavel = true;
    }
}
