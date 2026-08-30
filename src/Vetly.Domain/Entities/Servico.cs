using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Serviço oferecido por um prestador, com valor e duração (RN-032/RN-074).
///
/// É o que o Responsável filtra na busca e o que define o preço da consulta —
/// antes disso o valor vinha solto no pagamento, sem nada que o justificasse.
///
/// Serviço clínico do próprio vet/clínica <b>não</b> paga taxa de listagem: já é
/// monetizado pela assinatura e pelo split (RN-074).
/// </summary>
public class Servico
{
    /// <summary>Identificador único do serviço (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Prestador que oferece o serviço.</summary>
    [Required]
    public Guid PrestadorId { get; private set; }

    /// <summary>Necessidade atendida.</summary>
    [Required]
    public TipoServico Tipo { get; private set; }

    /// <summary>Valor cobrado.</summary>
    [Range(0, 999999.99)]
    public decimal Valor { get; private set; }

    /// <summary>Indica se o prestador aceita plano de saúde pet neste serviço.</summary>
    public bool AceitaPlanoPet { get; private set; }

    /// <summary>Duração do atendimento, em minutos.</summary>
    public int DuracaoMinutos { get; private set; }

    /// <summary>Indica se o serviço está ativo na vitrine do prestador.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Servico() { }

    /// <summary>Cria um serviço oferecido pelo prestador.</summary>
    public Servico(Guid prestadorId, TipoServico tipo, decimal valor, int duracaoMinutos, bool aceitaPlanoPet = false)
    {
        if (valor < 0)
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor não pode ser negativo.");

        if (duracaoMinutos <= 0)
            throw new ArgumentOutOfRangeException(nameof(duracaoMinutos), "A duração deve ser maior que zero.");

        Id = Guid.NewGuid();
        PrestadorId = prestadorId;
        Tipo = tipo;
        Valor = valor;
        DuracaoMinutos = duracaoMinutos;
        AceitaPlanoPet = aceitaPlanoPet;
        Ativo = true;
    }

    /// <summary>Atualiza valor, duração e aceitação de plano pet.</summary>
    public void Atualizar(decimal valor, int duracaoMinutos, bool aceitaPlanoPet)
    {
        if (valor < 0)
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor não pode ser negativo.");

        if (duracaoMinutos <= 0)
            throw new ArgumentOutOfRangeException(nameof(duracaoMinutos), "A duração deve ser maior que zero.");

        Valor = valor;
        DuracaoMinutos = duracaoMinutos;
        AceitaPlanoPet = aceitaPlanoPet;
    }

    /// <summary>Retira o serviço da vitrine sem apagar o histórico de quem já o contratou.</summary>
    public void Desativar() => Ativo = false;

    /// <summary>Recoloca o serviço na vitrine.</summary>
    public void Reativar() => Ativo = true;
}
