namespace Vetly.Application.DTOs.Veterinario;

/// <summary>
/// Resposta do conselho regional sobre um registro de CRMV (RN-107).
/// </summary>
public class ResultadoCrmvDto
{
    /// <summary>Situação do registro segundo o conselho.</summary>
    public ResultadoValidacaoCrmv Resultado { get; set; }

    /// <summary>Data/hora da consulta ao conselho (UTC).</summary>
    public DateTime ConsultadoEm { get; set; }

    /// <summary>Detalhe legível do resultado, para log e para a notificação ao profissional.</summary>
    public string? Mensagem { get; set; }
}

/// <summary>
/// Resultado possível da consulta ao conselho (RN-107, contrato do vetly-tech §7.5).
/// <see cref="Indisponivel"/> é parte obrigatória do contrato: quando o conselho não
/// responde, o perfil fica pendente de validação — nunca aprovado por omissão.
/// </summary>
public enum ResultadoValidacaoCrmv
{
    /// <summary>Registro válido e ativo.</summary>
    Valido = 1,

    /// <summary>Registro inexistente ou inválido.</summary>
    Invalido = 2,

    /// <summary>Registro existente, porém suspenso.</summary>
    Suspenso = 3,

    /// <summary>Conselho não respondeu. Perfil segue pendente e não é publicado.</summary>
    Indisponivel = 4
}
