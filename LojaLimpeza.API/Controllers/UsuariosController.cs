using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LojaLimpeza.API.Data;
using LojaLimpeza.API.DTOs;
using LojaLimpeza.API.Models;
using LojaLimpeza.API.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LojaLimpeza.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public UsuariosController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // Somente administradores podem listar os usuários cadastrados.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUsuarios()
    {
        var usuarios = await _context.Usuarios.AsNoTracking()
            .Select(u => new
            {
                u.Id,
                u.Nome,
                u.Email,
                u.Perfil,
                u.DataRegisto
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    // Cadastro público: recebe SOMENTE nome, email e senha.
    [HttpPost("registrar")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarUsuario([FromBody] RegistrarUsuarioDto entrada)
    {
        var email = entrada.Email.Trim().ToLowerInvariant();
        var nome = entrada.Nome.Trim();

        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest("O nome não pode ficar em branco.");

        if (await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == email))
            return Conflict("Este e-mail já está em uso.");

        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(entrada.Senha),
            Perfil = PerfilUsuario.Cliente, // Nunca confiar no perfil enviado pelo cliente.
            DataRegisto = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Trata possível cadastro simultâneo com o mesmo e-mail.
            if (await _context.Usuarios.AsNoTracking()
                .AnyAsync(u => u.Email.ToLower() == email))
            {
                return Conflict("Este e-mail já está em uso.");
            }
            throw;
        }

        return StatusCode(201, new
        {
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.Perfil
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (usuario == null ||
            !BCrypt.Net.BCrypt.Verify(request.Senha, usuario.PasswordHash))
        {
            return Unauthorized("E-mail ou senha incorretos.");
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Perfil.ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credenciais = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credenciais);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Senha { get; set; } = string.Empty;
}
