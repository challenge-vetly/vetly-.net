using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// A janela de captura de uma consulta (RN-008/RN-079) e o ciclo de documentação que
/// vem depois dela (§7.3).
///
/// O veterinário abre com "iniciar consulta" e fecha com "encerrar consulta". Fora
/// dessa janela a IA não captura áudio nem produz conteúdo clínico — é um limite
/// explícito de gravação, auditável e sob controle do profissional.
/// </summary>
public class SessaoCaptura
{
    /// <summary>Identificador da sessão (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Consulta a que a sessão pertence.</summary>
    [Required]
    public Guid ConsultaId { get; private set; }

    /// <summary>Estado no ciclo da §7.3.</summary>
    [Required]
    public EstadoSessaoCaptura Estado { get; private set; }

    /// <summary>Quando o veterinário iniciou a consulta (RN-008).</summary>
    public DateTime IniciadaEm { get; private set; }

    /// <summary>Quando encerrou. Nulo enquanto a janela está aberta.</summary>
    public DateTime? EncerradaEm { get; private set; }

    /// <summary>
    /// Indica que a captura de áudio está ativa. Falso no plano Básico, que registra a
    /// consulta mas não tem IA na consulta (RN-085).
    /// </summary>
    public bool CapturaAtiva { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private SessaoCaptura() { }

    /// <summary>
    /// Abre a janela de captura de uma consulta (RN-008).
    /// <paramref name="capturaAtiva"/> vem do plano do prestador: no Básico a consulta
    /// inicia normalmente, mas sem captura (RN-085).
    /// </summary>
    public SessaoCaptura(Guid consultaId, bool capturaAtiva)
    {
        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        IniciadaEm = DateTime.UtcNow;
        CapturaAtiva = capturaAtiva;

        // Sem captura nao ha o que transcrever: a sessao ja nasce no caminho manual
        Estado = capturaAtiva ? EstadoSessaoCaptura.Capturando : EstadoSessaoCaptura.SemTranscricao;
    }

    /// <summary>Verdadeiro enquanto a janela aceita novos segmentos (RN-079).</summary>
    public bool JanelaAberta() => Estado == EstadoSessaoCaptura.Capturando;

    /// <summary>
    /// Fecha a janela (RN-008). O estado seguinte depende do que foi capturado:
    /// sem segmento nenhum, vai direto ao caminho manual.
    /// </summary>
    public void Encerrar(int segmentosRecebidos)
    {
        if (EncerradaEm is not null)
            throw new InvalidOperationException("Esta consulta já foi encerrada.");

        EncerradaEm = DateTime.UtcNow;

        if (!CapturaAtiva || segmentosRecebidos == 0)
        {
            Estado = EstadoSessaoCaptura.SemTranscricao;
            return;
        }

        Estado = EstadoSessaoCaptura.AguardandoTranscricao;
    }

    /// <summary>
    /// Registra o desfecho da transcrição depois que todos os segmentos tiveram
    /// resposta (RN-009).
    ///
    /// Falha em parte dos segmentos não interrompe o fluxo: o rascunho é gerado com o
    /// texto disponível e o aviso vai ao veterinário, que resolve na correção. Perder
    /// a consulta inteira porque um trecho falhou seria pior que um rascunho parcial.
    /// </summary>
    public void RegistrarDesfechoDaTranscricao(int transcritos, int falhados)
    {
        if (Estado != EstadoSessaoCaptura.AguardandoTranscricao)
            return;

        Estado = (transcritos, falhados) switch
        {
            (0, _) => EstadoSessaoCaptura.SemTranscricao,
            (_, > 0) => EstadoSessaoCaptura.TranscricaoParcial,
            _ => EstadoSessaoCaptura.GerandoRascunho
        };
    }

    /// <summary>Marca o início da estruturação pela IA (RN-080).</summary>
    public void IniciarEstruturacao()
    {
        if (Estado is not (EstadoSessaoCaptura.GerandoRascunho or EstadoSessaoCaptura.TranscricaoParcial))
            throw new InvalidOperationException("A sessão não está pronta para gerar rascunho.");

        Estado = EstadoSessaoCaptura.GerandoRascunho;
    }

    /// <summary>Rascunho disponível para a decisão do veterinário (RN-082).</summary>
    public void RascunhoDisponivel() => Estado = EstadoSessaoCaptura.RascunhoPronto;

    /// <summary>
    /// A estruturação falhou. Cai no caminho manual em vez de travar a consulta —
    /// o atendimento aconteceu e precisa virar prontuário de algum jeito (RN-085).
    /// </summary>
    public void EstruturacaoFalhou() => Estado = EstadoSessaoCaptura.SemTranscricao;

    /// <summary>O veterinário aprovou ou corrigiu: documentos serão gerados (RN-082/RN-083).</summary>
    public void IniciarDocumentacao() => Estado = EstadoSessaoCaptura.Documentando;

    /// <summary>O veterinário não aprovou: o ciclo encerra sem documentos (RN-082).</summary>
    public void EncerrarSemDocumentos() => Estado = EstadoSessaoCaptura.EncerradaSemDocumentos;

    /// <summary>Documentos gerados, assinados e publicados.</summary>
    public void Concluir() => Estado = EstadoSessaoCaptura.Concluida;
}
