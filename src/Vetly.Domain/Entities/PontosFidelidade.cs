using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.Domain.Entities;

/// <summary>
/// Lançamento de pontos de fidelidade gerado por uma consulta realizada (RN-070) — seja
/// cumprindo uma obrigação no prazo, seja como consulta avulsa (peso menor). Expira em
/// 12 meses (RN-074): como cada lançamento carrega sua própria <see cref="ExpiraEm"/>,
/// os mais antigos vencem primeiro naturalmente — não é preciso uma fila FIFO à parte.
/// </summary>
public class PontosFidelidade
{
    /// <summary>Identificador único do lançamento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do responsável dono dos pontos.</summary>
    public Guid ResponsavelId { get; private set; }

    /// <summary>Id da consulta que originou o lançamento — todo lançamento nasce de uma consulta realizada.</summary>
    public Guid ConsultaId { get; private set; }

    /// <summary>Evento que originou os pontos (RN-070).</summary>
    public OrigemPontos Origem { get; private set; }

    /// <summary>Quantidade de pontos concedidos.</summary>
    public int Pontos { get; private set; }

    /// <summary>Momento do lançamento.</summary>
    public DateTime Data { get; private set; }

    /// <summary>Momento de expiração — 12 meses após <see cref="Data"/> (RN-074).</summary>
    public DateTime ExpiraEm { get; private set; }

    /// <summary>True se estornado por cancelamento/reembolso da consulta de origem (RN-075).</summary>
    public bool Estornado { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private PontosFidelidade() { }

    /// <summary>Cria um novo lançamento de pontos, válido por 12 meses a partir de <paramref name="data"/>.</summary>
    public PontosFidelidade(Guid responsavelId, Guid consultaId, OrigemPontos origem, int pontos, DateTime data)
    {
        if (pontos <= 0)
            throw new DomainException("FIDELIDADE-001", "A quantidade de pontos deve ser maior que zero.");

        Id = Guid.NewGuid();
        ResponsavelId = responsavelId;
        ConsultaId = consultaId;
        Origem = origem;
        Pontos = pontos;
        Data = data;
        ExpiraEm = data.AddMonths(12);
    }

    /// <summary>True se ainda válido (não estornado e não expirado) em <paramref name="agora"/>.</summary>
    public bool Valido(DateTime agora) => !Estornado && agora <= ExpiraEm;

    /// <summary>Estorna o lançamento por cancelamento/reembolso da consulta de origem (RN-075).</summary>
    public void Estornar() => Estornado = true;
}
