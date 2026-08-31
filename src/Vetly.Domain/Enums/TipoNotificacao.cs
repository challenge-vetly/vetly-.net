namespace Vetly.Domain.Enums;

/// <summary>
/// Natureza de uma notificação ao Responsável (RN-092/RN-093).
///
/// O tipo define ícone, agrupamento e prioridade no app. Separar importa porque
/// misturar "sua vacina venceu" com "seu documento está pronto" na mesma caixa faz o
/// Responsável parar de ler as duas.
/// </summary>
public enum TipoNotificacao
{
    /// <summary>Obrigação de cuidado vencendo ou vencida (RN-045/RN-094).</summary>
    ObrigacaoVencendo = 1,

    /// <summary>Consulta confirmada após o pagamento (RN-006).</summary>
    ConsultaConfirmada = 2,

    /// <summary>Lembrete da consulta que se aproxima.</summary>
    ConsultaProxima = 3,

    /// <summary>Documento publicado no board do pet (RN-011/RN-090).</summary>
    DocumentoPublicado = 4,

    /// <summary>Convite para avaliar o atendimento (RN-055).</summary>
    AvaliacaoPendente = 5,

    /// <summary>Horário da lista de espera liberado (RN-037).</summary>
    HorarioDisponivel = 6,

    /// <summary>Pontos de fidelidade prestes a expirar (RN-051).</summary>
    PontosExpirando = 7
}

/// <summary>
/// Situação da entrega de uma notificação (RN-092).
///
/// <c>NaoEntregue</c> não é o fim: a notificação permanece na caixa de entrada do
/// app, porque push perdido não pode significar aviso perdido.
/// </summary>
public enum StatusNotificacao
{
    /// <summary>Gravada, aguardando o momento do envio.</summary>
    Pendente = 1,

    /// <summary>Entregue por push a pelo menos um dispositivo.</summary>
    Enviada = 2,

    /// <summary>Tentativas esgotadas. Segue visível na caixa de entrada do app.</summary>
    NaoEntregue = 3,

    /// <summary>O Responsável abriu no app.</summary>
    Lida = 4
}
