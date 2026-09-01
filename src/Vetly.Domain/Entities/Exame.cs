using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um exame laboratorial ou de imagem solicitado durante o atendimento.
/// O resultado só pode ser liberado ao tutor após validação do veterinário.
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
    /// Indica se o resultado foi liberado para visualização pelo tutor.
    /// Só pode ser liberado após o resultado estar disponível.
    /// </summary>
    public bool LiberadoAoTutor { get; private set; }

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
    /// <summary>
    /// Midias do laudo (PDF, imagem), separadas por ";" (RN-104).
    ///
    /// Resultado de exame raramente e so texto: o laudo vem em PDF e a imagem vem do
    /// equipamento. Guardar so o texto obrigaria o veterinario a transcrever o que ja
    /// existe em arquivo, e a transcricao e onde o dado se perde.
    /// </summary>
    [MaxLength(2000)]
    public string? MidiaIds { get; private set; }

    /// <summary>Ids das midias do laudo, ja separados.</summary>
    public IReadOnlyList<Guid> Midias() =>
        string.IsNullOrWhiteSpace(MidiaIds) || MidiaIds == ";"
            ? []
            : [.. MidiaIds.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse)];

    public void RegistrarResultado(string resultado)
    {
        Resultado = resultado;
        DataResultado = DateTime.UtcNow;
    }

    /// <summary>
    /// Libera o resultado para visualização pelo tutor.
    /// Lança exceção se o resultado ainda não foi registrado.
    /// </summary>
    /// <summary>
    /// Anexa as midias do laudo ao resultado (RN-104).
    ///
    /// ";" em vez de vazio: no Oracle a string vazia E NULL, e distinguir "laudo sem
    /// anexo" de "ainda nao informado" importa na leitura.
    /// </summary>
    public void AnexarMidias(IEnumerable<Guid>? midias)
    {
        var lista = midias?.ToList() ?? [];

        MidiaIds = lista.Count == 0 ? ";" : string.Join(';', lista);
    }

    public void LiberarAoTutor()
    {
        if (string.IsNullOrWhiteSpace(Resultado))
            throw new InvalidOperationException("Não é possível liberar exame sem resultado registrado.");

        LiberadoAoTutor = true;
    }
}
