namespace Vetly.Application.Interfaces;

/// <summary>Pedido de assinatura de um documento clínico (RN-087).</summary>
/// <param name="DocumentoId">Documento a assinar.</param>
/// <param name="Tipo">Tipo do documento, que define o que a assinatura precisa afirmar.</param>
/// <param name="NomeDoVeterinario">Nome registrado do profissional.</param>
/// <param name="Crmv">CRMV do signatário.</param>
/// <param name="UfAtuacao">UF do conselho.</param>
/// <param name="NomeDigitado">Nome como o profissional o digitou no ato de assinar.</param>
public readonly record struct SolicitacaoDeAssinaturaDto(
    Guid DocumentoId,
    string Tipo,
    string NomeDoVeterinario,
    string Crmv,
    string UfAtuacao,
    string? NomeDigitado);

/// <summary>Assinatura produzida pelo adaptador (RN-087).</summary>
/// <param name="Metodo">Como se assinou. É o que diz o quanto a assinatura vale.</param>
/// <param name="Carimbo">Texto impresso no documento.</param>
/// <param name="AssinadoEm">Instante da assinatura.</param>
/// <param name="HabilitaDispensacaoExterna">
/// Se a assinatura tem validade para dispensação de medicamento controlado fora da
/// plataforma. No MVP é sempre falso, e o documento diz isso.
/// </param>
public readonly record struct AssinaturaDto(
    string Metodo,
    string Carimbo,
    DateTime AssinadoEm,
    bool HabilitaDispensacaoExterna);

/// <summary>
/// Porta de saída da assinatura de documentos clínicos (RN-087, §5).
///
/// Existe como porta porque a assinatura é a parte do sistema com maior distância
/// entre o MVP e a produção: hoje o nome digitado pelo profissional, amanhã um
/// certificado ICP-Brasil vinculado ao CRMV. O que não pode mudar é o resto do
/// fluxo em volta — quem pode assinar, quando, e o que a assinatura habilita.
/// </summary>
public interface IAssinaturaAdapter
{
    /// <summary>Assina o documento e devolve o método e o carimbo a gravar.</summary>
    Task<AssinaturaDto> AssinarAsync(SolicitacaoDeAssinaturaDto solicitacao);
}
