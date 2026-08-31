namespace Vetly.Application.Interfaces;

/// <summary>
/// Porta de saída da renderização de documentos em PDF (RN-090).
///
/// O Responsável precisa levar o documento para fora do app — outra clínica, um pet
/// shop, um seguro. Texto puro no board não serve para isso; PDF serve, e é o formato
/// que qualquer lugar aceita.
///
/// Fica atrás de porta porque a renderização é a parte mais provável de trocar: hoje
/// um gerador simples, amanhã um com identidade visual e QR de verificação.
/// </summary>
public interface IGeradorDePdf
{
    /// <summary>
    /// Renderiza o texto do documento em PDF. O título vira metadado do arquivo, e é
    /// o que aparece na aba do leitor.
    /// </summary>
    byte[] Renderizar(string titulo, string conteudo);
}
