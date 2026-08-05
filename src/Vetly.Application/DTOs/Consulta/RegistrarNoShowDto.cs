using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de entrada para registrar no-show de uma das partes (RN-064/066).</summary>
public class RegistrarNoShowDto
{
    [Required(ErrorMessage = "A parte é obrigatória.")]
    public ParteNoShow Parte { get; set; }
}
