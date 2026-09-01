namespace Vetly.Application.DTOs.Prontuario;

/// <summary>
/// Oculta ou volta a exibir um registro do histórico no board do Responsável
/// (RN-068).
///
/// O corpo carrega o estado desejado, e não um verbo: <c>ocultar</c> sem parâmetro
/// obrigaria uma segunda rota para desfazer, e a tela precisa das duas direções.
/// </summary>
public class OcultarRegistroDto
{
    /// <summary>Verdadeiro para esconder do board; falso para voltar a exibir.</summary>
    public bool Oculto { get; set; } = true;
}
