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
    PontosExpirando = 7,

    /// <summary>Exame solicitado, com as orientações de preparo (RN-103).</summary>
    ExameSolicitado = 8,

    /// <summary>Atualização diária da internação (RN-100).</summary>
    AtualizacaoInternacao = 9,

    /// <summary>
    /// Comunicação promocional. Exige opt-in específico e tem opt-out em um toque
    /// (RN-093) — é o único tipo que o Responsável pode desligar.
    /// </summary>
    Promocao = 10,

    /// <summary>Mudança no atendimento decidida pelo prestador (RN-025/RN-045).</summary>
    CancelamentoPeloPrestador = 11,

    /// <summary>
    /// Reembolso apurado no cancelamento (RN-014/RN-041/RN-042, §6.2).
    ///
    /// Separado de <see cref="CancelamentoPeloPrestador"/> porque responde a outra
    /// pergunta: aquele diz que o atendimento mudou, este diz o que aconteceu com o
    /// dinheiro. Cancelamento sem reembolso também avisa — "não vai voltar nada" é
    /// justamente o que o Responsável precisa saber sem ter de perguntar.
    /// </summary>
    ReembolsoConfirmado = 12,

    /// <summary>
    /// Pontos creditados e, quando for o caso, o tier que mudou (RN-016/RN-047/RN-048,
    /// §6.2).
    ///
    /// Distinto de <see cref="PontosExpirando"/>: aquele é perda iminente e pede ação
    /// imediata; este é reforço do comportamento que o programa quer premiar, e
    /// misturar os dois na mesma caixa faria o aviso de expiração perder urgência.
    /// </summary>
    PontosCreditados = 13
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
