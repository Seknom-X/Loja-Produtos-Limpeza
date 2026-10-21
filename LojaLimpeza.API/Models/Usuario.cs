using LojaLimpeza.API.Models.Enums;

namespace LojaLimpeza.API.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Cliente;
    public DateTime DataRegisto { get; set; } = DateTime.UtcNow;
}