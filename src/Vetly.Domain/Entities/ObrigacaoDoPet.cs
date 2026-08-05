using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.Domain.Entities;

/// <summary>
/// Evento do calendário de cuidado do pet — vacina, vermífugo, retorno ou check-up —
/// gerado por espécie no cadastro do animal (RN-069). Cumprir uma obrigação no prazo,
/// via consulta na Vetly, é o motor de pontuação da fidelidade (RN-070).
/// </summary>
public class ObrigacaoDoPet
{
    /// <summary>Identificador único da obrigação (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do animal a que esta obrigação pertence.</summary>
    public Guid AnimalId { get; private set; }

    /// <summary>Tipo do evento de cuidado (RN-069).</summary>
    public TipoObrigacao Tipo { get; private set; }

    /// <summary>Data-limite para cumprir a obrigação.</summary>
    public DateTime DataLimite { get; private set; }

    /// <summary>Status persistido — "atrasada" é derivado, não um estado próprio.</summary>
    public StatusObrigacao Status { get; private set; }

    /// <summary>Id da consulta que cumpriu a obrigação, se já cumprida.</summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>Momento em que a obrigação foi marcada como cumprida, se já cumprida.</summary>
    public DateTime? DataCumprimento { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private ObrigacaoDoPet() { }

    /// <summary>Cria uma nova obrigação pendente. Usado pelas <c>IObrigacaoFactory</c> ao gerar o calendário.</summary>
    public ObrigacaoDoPet(Guid animalId, TipoObrigacao tipo, DateTime dataLimite)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        Tipo = tipo;
        DataLimite = dataLimite;
        Status = StatusObrigacao.Pendente;
    }

    /// <summary>
    /// Marca a obrigação como cumprida por uma consulta (RN-070). Só é possível a partir
    /// de Pendente — cumprir a mesma obrigação duas vezes é um erro de fluxo.
    /// </summary>
    public void MarcarCumprida(Guid consultaId, DateTime agora)
    {
        if (Status != StatusObrigacao.Pendente)
            throw new DomainException("OBRIGACAO-001", "Esta obrigação já foi cumprida.");

        Status = StatusObrigacao.Cumprida;
        ConsultaId = consultaId;
        DataCumprimento = agora;
    }

    /// <summary>True se ainda pendente e dentro do prazo em <paramref name="agora"/>.</summary>
    public bool EstaNoPrazo(DateTime agora) => Status == StatusObrigacao.Pendente && agora <= DataLimite;

    /// <summary>True se ainda pendente e já passou da data-limite em <paramref name="agora"/> ("atrasada").</summary>
    public bool EstaAtrasada(DateTime agora) => Status == StatusObrigacao.Pendente && agora > DataLimite;
}
