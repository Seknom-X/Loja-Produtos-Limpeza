using System.ComponentModel.DataAnnotations;

namespace LojaLimpeza.API.DTOs;

public class RegistrarUsuarioDto
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Senha { get; set; } = string.Empty;
}
