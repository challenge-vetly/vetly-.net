using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Veterinario;

/// <summary>
/// Extrato dos atendimentos realizados por um veterinário (RN-024).
///
/// É a única coisa que o profissional desativado continua alcançando, e o formato
/// segue disso: <b>sem nome de Responsável, sem nome de animal, sem conteúdo
/// clínico</b>. O que ele precisa é do registro financeiro do próprio trabalho — para
/// conferir repasses, para a contabilidade, para uma eventual disputa. Nada disso
/// exige saber de quem era o pet.
/// </summary>
public class ExtratoDoVeterinarioDto
{
    public Guid VeterinarioId { get; set; }

    /// <summary>Nome do próprio profissional — o dado é dele.</summary>
    public string Nome { get; set; } = string.Empty;

    public string Crmv { get; set; } = string.Empty;

    /// <summary>Se o cadastro está ativo. Falso não impede o extrato (RN-024).</summary>
    public bool CadastroAtivo { get; set; }

    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim { get; set; }

    /// <summary>Atendimentos realizados no período.</summary>
    public int TotalDeAtendimentos { get; set; }

    /// <summary>Soma cobrada dos Responsáveis nos atendimentos do período.</summary>
    public decimal ValorBruto { get; set; }

    /// <summary>Soma retida pela plataforma (RN-070).</summary>
    public decimal ComissaoDaPlataforma { get; set; }

    /// <summary>Soma que cabe ao profissional ou à clínica (RN-072).</summary>
    public decimal RepasseTotal { get; set; }

    /// <summary>Parte do repasse já liquidada.</summary>
    public decimal RepasseLiquidado { get; set; }

    /// <summary>Parte do repasse ainda pendente — é o que o profissional vem conferir.</summary>
    public decimal RepassePendente { get; set; }

    public List<ItemDoExtratoDto> Itens { get; set; } = [];
}

/// <summary>
/// Uma linha do extrato (RN-024).
///
/// Identifica o atendimento pela data e pelo id da consulta, e nada mais. Sem espécie,
/// sem nome, sem diagnóstico: o extrato é financeiro, e dado clínico aqui seria dado
/// vazando por uma porta que a RN-022 fechou.
/// </summary>
public class ItemDoExtratoDto
{
    public Guid ConsultaId { get; set; }
    public DateTime DataDoAtendimento { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
    public StatusConsulta Status { get; set; }

    public decimal Valor { get; set; }
    public decimal? Comissao { get; set; }
    public decimal? Repasse { get; set; }

    /// <summary>Plano que definiu o take rate deste atendimento (RN-070).</summary>
    public PlanoAssinatura? PlanoAplicado { get; set; }

    public StatusPagamento StatusPagamento { get; set; }

    /// <summary>Se o repasse deste atendimento já foi liquidado.</summary>
    public bool Liquidado { get; set; }
}
