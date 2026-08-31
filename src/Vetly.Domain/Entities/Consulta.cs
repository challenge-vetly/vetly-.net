using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa uma consulta veterinária agendada ou realizada na plataforma Vetly.
/// O agendamento só é confirmado após pagamento processado (RN-006).
/// </summary>
public class Consulta
{
    /// <summary>Identificador único da consulta (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Data e hora agendada para a consulta. Indexada no banco para buscas por período.</summary>
    [Required]
    public DateTime DataHora { get; private set; }

    /// <summary>Modalidade da consulta. No MVP apenas presencial (RN-039).</summary>
    [Required]
    public ModalidadeAtendimento Modalidade { get; private set; }

    /// <summary>Id do veterinário responsável pela consulta. Chave estrangeira para TB_VETERINARIO.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Id do animal atendido. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Id do tutor responsável. Chave estrangeira para TB_TUTOR.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>
    /// Indica se o veterinário validou o diagnóstico sugerido pela IA.
    /// RN-082: documentos só podem ser gerados após validação manual.
    /// </summary>
    public bool DiagnosticoValidado { get; private set; }

    /// <summary>Indica se o veterinário validou o protocolo de tratamento sugerido pela IA.</summary>
    public bool ProtocoloValidado { get; private set; }

    /// <summary>Status do pagamento associado a esta consulta.</summary>
    [Required]
    public StatusPagamento StatusPagamento { get; private set; }

    /// <summary>
    /// Estado da consulta na máquina de estados do agendamento (RN-035/RN-038).
    /// Fonte de verdade do ciclo de vida — os booleanos abaixo são escritos em
    /// paralelo por uma release e serão removidos depois.
    /// </summary>
    [Required]
    public StatusConsulta Status { get; private set; }

    /// <summary>
    /// Indica se a consulta foi cancelada.
    /// <b>Dupla escrita:</b> mantido em sincronia com <see cref="Status"/> para não
    /// quebrar os consumidores atuais (filtro <c>?cancelada=</c> e testes).
    /// </summary>
    public bool Cancelada { get; private set; }

    /// <summary>
    /// Indica se a consulta foi finalizada (receita assinada obrigatória — RN-087).
    /// <b>Dupla escrita:</b> mantido em sincronia com <see cref="Status"/>.
    /// </summary>
    public bool Finalizada { get; private set; }

    /// <summary>
    /// Horário da agenda reservado para esta consulta. Nulo na emergência presencial,
    /// que não passa por slot (RN-040).
    /// </summary>
    public Guid? SlotId { get; private set; }

    /// <summary>Serviço contratado, que define o valor da consulta (RN-032).</summary>
    public Guid? ServicoId { get; private set; }

    /// <summary>
    /// Clínica que designou o profissional, quando o agendamento foi feito com ela
    /// e não com um autônomo (RN-003).
    /// </summary>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Como a consulta entrou na plataforma (RN-035/RN-040).</summary>
    public OrigemConsulta Origem { get; private set; }

    /// <summary>
    /// Quando o veterinário acionou "iniciar consulta". Abre a janela de captura da
    /// IA (RN-008/RN-079).
    /// </summary>
    public DateTime? IniciadaEm { get; private set; }

    /// <summary>
    /// Quando acionou "encerrar consulta". Fecha a janela e dispara os processos
    /// pós-consulta (RN-008).
    /// </summary>
    public DateTime? EncerradaEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Consulta() { }

    /// <summary>
    /// Cria uma nova consulta com status de pagamento pendente.
    /// O agendamento só deve ser confirmado após chamar <see cref="ConfirmarPagamento"/> (RN-006).
    /// </summary>
    public Consulta(DateTime dataHora, ModalidadeAtendimento modalidade, Guid veterinarioId, Guid animalId, Guid tutorId)
    {
        Id = Guid.NewGuid();
        DataHora = dataHora;
        Modalidade = modalidade;
        VeterinarioId = veterinarioId;
        AnimalId = animalId;
        TutorId = tutorId;
        StatusPagamento = StatusPagamento.Pendente; // pagamento confirmado é pré-requisito (RN-006)
        Status = StatusConsulta.EmCheckout;
        Origem = OrigemConsulta.Emergencia;
    }

    /// <summary>
    /// Cria a consulta do fluxo de checkout do app: nasce em <c>EmCheckout</c>, presa
    /// ao horário travado, e só a confirmação do pagamento a promove (RN-006/RN-035).
    /// </summary>
    public static Consulta ParaCheckout(
        DateTime dataHora, Guid veterinarioId, Guid animalId, Guid tutorId,
        Guid slotId, Guid servicoId, Guid? empresaId = null)
    {
        var consulta = new Consulta(dataHora, ModalidadeAtendimento.Presencial, veterinarioId, animalId, tutorId)
        {
            SlotId = slotId,
            ServicoId = servicoId,
            EmpresaId = empresaId,
            Origem = OrigemConsulta.Checkout
        };

        return consulta;
    }

    /// <summary>Marca o pagamento como confirmado, liberando o agendamento (RN-006).</summary>
    public void ConfirmarPagamento()
    {
        StatusPagamento = StatusPagamento.Confirmado;

        // Só promove a partir do checkout: confirmar o pagamento de uma consulta já
        // cancelada ou realizada não pode reabri-la (RN-038).
        if (Status == StatusConsulta.EmCheckout)
            Status = StatusConsulta.Confirmada;
    }

    /// <summary>Registra a validação manual do diagnóstico pelo veterinário (RN-082).</summary>
    public void ValidarDiagnostico() => DiagnosticoValidado = true;

    /// <summary>Registra a validação manual do protocolo de tratamento pelo veterinário.</summary>
    public void ValidarProtocolo() => ProtocoloValidado = true;

    /// <summary>Cancela a consulta. O reembolso é calculado pelo Strategy de cancelamento (RN-014/RN-041/RN-042).</summary>
    public void Cancelar()
    {
        Cancelada = true;
        Status = StatusConsulta.Cancelada;
    }

    /// <summary>
    /// Finaliza a consulta após confirmação de receita assinada digitalmente (RN-087).
    /// Hoje é o único evento que marca a consulta como <see cref="StatusConsulta.Realizada"/>;
    /// quando <c>POST /api/consultas/{id}/encerrar</c> entrar (onda 5), ele passa a ser o
    /// evento que fecha o atendimento, e <c>finalizar</c> vira o fecho documental (P-01).
    /// </summary>
    public void Finalizar()
    {
        Finalizada = true;
        Status = StatusConsulta.Realizada;
    }

    /// <summary>
    /// Registra o não comparecimento do Responsável (RN-044). Sem reembolso, seguindo
    /// a faixa "menos de 2h ou no ato" da RN-014.
    /// </summary>
    public void RegistrarNoShow() => Status = StatusConsulta.NoShow;

    /// <summary>Expira a consulta cujo lock de checkout venceu sem pagamento (RN-035).</summary>
    public void Expirar() => Status = StatusConsulta.Expirada;

    /// <summary>Registra o início do atendimento pelo veterinário (RN-008).</summary>
    public void RegistrarInicio(DateTime quando) => IniciadaEm = quando;

    /// <summary>Registra o encerramento do atendimento pelo veterinário (RN-008).</summary>
    public void RegistrarEncerramento(DateTime quando) => EncerradaEm = quando;

    /// <summary>Reagenda a consulta para uma nova data e hora.</summary>
    public void Reagendar(DateTime novaDataHora) => DataHora = novaDataHora;

    /// <summary>
    /// Verifica se a consulta está apta para geração de documentos.
    /// Requer diagnóstico validado E pagamento confirmado.
    /// </summary>
    public bool PodeGerarDocumentos() =>
        DiagnosticoValidado && StatusPagamento == StatusPagamento.Confirmado;
}
