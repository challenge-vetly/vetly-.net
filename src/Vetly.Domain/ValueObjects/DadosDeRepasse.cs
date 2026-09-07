namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Conta em que o prestador recebe o repasse do split (§4.1, RN-072).
///
/// O produto pede no onboarding "banco, agência, conta, CPF ou CNPJ do titular e
/// chave Pix", e é exatamente isso que este objeto guarda. Ele fica embutido no
/// registro do prestador — veterinário autônomo ou empresa —, no mesmo modelo 1:1 de
/// <see cref="Endereco"/>: conta bancária não tem vida própria fora do titular, e
/// tabela separada só criaria um caminho a mais para ela existir órfã.
///
/// <b>Por que existe no MVP, se não há liquidação.</b> O split já grava o
/// destinatário do repasse (<c>DestinatarioRepasseId</c>) e o consolidado financeiro
/// já separa o que cabe a cada um. Sem esta informação, o destinatário é um id sem
/// endereço de pagamento: apura-se quanto, nunca para onde. Registrar agora é o que
/// permite que ligar o gateway seja trocar o adaptador de pagamento, e não descobrir
/// no dia que falta o cadastro de todo mundo.
///
/// <b>Sigilo.</b> Estes dados são pessoais e a §7.3 veda expressamente ao
/// administrador da unidade "dados bancários pessoais" dos vets vinculados. Quem lê é
/// o próprio titular, e mesmo ele lê a conta mascarada
/// (<see cref="ContaMascarada"/>) — a tela de conferência precisa dos últimos
/// dígitos, não do número inteiro.
/// </summary>
public sealed class DadosDeRepasse
{
    /// <summary>Quantos dígitos finais aparecem na conta mascarada.</summary>
    private const int DigitosVisiveis = 4;

    /// <summary>Código ou nome do banco (ex.: <c>341</c>, <c>Itaú</c>).</summary>
    public string Banco { get; private set; }

    /// <summary>Agência, sem o dígito verificador quando ele for separado.</summary>
    public string Agencia { get; private set; }

    /// <summary>Número da conta, com dígito.</summary>
    public string Conta { get; private set; }

    /// <summary>
    /// CPF ou CNPJ do titular, só dígitos.
    ///
    /// Guardado normalizado porque é o que o comprovante de repasse confere: o mesmo
    /// documento digitado com e sem pontuação viraria dois cadastros diferentes na
    /// hora de bater a titularidade da conta.
    /// </summary>
    public string DocumentoTitular { get; private set; }

    /// <summary>
    /// Chave Pix do titular. O produto a pede junto dos dados bancários porque é o
    /// caminho de liquidação mais provável em produção — e ela vale por si, sem
    /// depender de agência e conta.
    /// </summary>
    public string ChavePix { get; private set; }

    /// <summary>Quando o titular informou ou atualizou a conta.</summary>
    public DateTime AtualizadoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private DadosDeRepasse()
    {
        Banco = null!;
        Agencia = null!;
        Conta = null!;
        DocumentoTitular = null!;
        ChavePix = null!;
    }

    /// <summary>
    /// Registra a conta de repasse. Todos os campos são obrigatórios: conta
    /// pela metade não paga ninguém, e aceitá-la faria o cadastro parecer completo.
    /// </summary>
    public DadosDeRepasse(
        string banco, string agencia, string conta, string documentoTitular, string chavePix)
    {
        Banco = Obrigatorio(banco, nameof(banco), "O banco é obrigatório.");
        Agencia = Obrigatorio(agencia, nameof(agencia), "A agência é obrigatória.");
        Conta = Obrigatorio(conta, nameof(conta), "A conta é obrigatória.");
        ChavePix = Obrigatorio(chavePix, nameof(chavePix), "A chave Pix é obrigatória.");

        DocumentoTitular = NormalizarDocumento(documentoTitular);
        AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Conta com só os últimos dígitos à mostra (ex.: <c>****4321</c>).
    ///
    /// É o que a API devolve ao titular. Uma conta curta demais para mascarar sai
    /// inteiramente coberta: expor "os três dígitos" de uma conta de três dígitos não
    /// mascara nada.
    /// </summary>
    public string ContaMascarada() =>
        Conta.Length <= DigitosVisiveis
            ? new string('*', Conta.Length)
            : new string('*', Conta.Length - DigitosVisiveis) + Conta[^DigitosVisiveis..];

    /// <summary>
    /// Chave Pix mascarada, preservando o suficiente para o titular se reconhecer.
    ///
    /// A chave pode ser e-mail, telefone, CPF/CNPJ ou uma aleatória, e mascarar cada
    /// formato de um jeito só produziria regra frágil. Os quatro últimos caracteres
    /// bastam para "é esta mesmo" sem entregar a chave a quem leu a resposta.
    /// </summary>
    public string ChavePixMascarada() =>
        ChavePix.Length <= DigitosVisiveis
            ? new string('*', ChavePix.Length)
            : new string('*', ChavePix.Length - DigitosVisiveis) + ChavePix[^DigitosVisiveis..];

    /// <summary>
    /// Documento do titular só com os dígitos, validado por comprimento.
    ///
    /// A conferência é de <b>formato</b>, não de dígito verificador: sem gateway não
    /// há a quem consultar a titularidade, e recusar por checksum daria a impressão de
    /// uma validação que a plataforma não faz. O comprimento, esse sim, separa um
    /// documento de um campo preenchido no chute.
    /// </summary>
    private static string NormalizarDocumento(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
            throw new ArgumentException("O CPF ou CNPJ do titular é obrigatório.", nameof(documento));

        var digitos = new string([.. documento.Where(char.IsDigit)]);

        if (digitos.Length is not (11 or 14))
            throw new ArgumentException(
                "O documento do titular deve ser um CPF (11 dígitos) ou um CNPJ (14 dígitos).",
                nameof(documento));

        return digitos;
    }

    private static string Obrigatorio(string valor, string campo, string mensagem) =>
        string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException(mensagem, campo) : valor.Trim();
}
