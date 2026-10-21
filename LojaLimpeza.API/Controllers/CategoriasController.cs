using LojaLimpeza.API.Data;
using LojaLimpeza.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LojaLimpeza.API.Controllers;

// Estas anotações dizem ao .NET que esta classe é uma API e responde no endereço "/api/categorias"
[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;

    // Injeção de Dependência: O .NET entrega o banco de dados automaticamente aqui
    public CategoriasController(AppDbContext context)
    {
        _context = context;
    }

    // Endpoint para LER todas as categorias (Método GET)
    [HttpGet]
    public async Task<IActionResult> GetCategorias()
    {
        // O Entity Framework vai ao MySQL, faz um "SELECT * FROM Categorias" e devolve como lista
        var categorias = await _context.Categorias.ToListAsync();
        
        return Ok(categorias); // Retorna o Código HTTP 200 (Sucesso) com os dados
    }

    // Endpoint para CRIAR uma nova categoria (Método POST)
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCategoria(Categoria categoria)
    {
        // Adiciona a categoria à memória do Entity Framework
        _context.Categorias.Add(categoria);
        
        // Salva fisicamente no MySQL (Commit)
        await _context.SaveChangesAsync();
        
        // Retorna HTTP 201 (Criado) e devolve o objeto criado com o seu novo ID do banco
        return CreatedAtAction(nameof(GetCategorias), new { id = categoria.Id }, categoria);
    }
    // Endpoint para ATUALIZAR um produto existente (Método PUT)
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategoria(int id, Categoria categoriaAtualizado)
    {
        // 1. Verifica se o ID do URL corresponde ao ID do corpo do JSON
        if (id != categoriaAtualizado.Id)
        {
            return BadRequest("O ID da categoria não corresponde.");
        }

        // 2. Avisa o Entity Framework que este objeto foi modificado
        _context.Entry(categoriaAtualizado).State = EntityState.Modified;

        try
        {
            // 3. Tenta guardar na base de dados
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 4. Se o produto não existir mais na base, retorna 404 (Not Found)
            if (!CategoriaExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        // Retorna 204 (No Content), padrão para atualizações bem-sucedidas
        return NoContent(); 
    }

    // Endpoint para DESATIVAR um produto (Exclusão Lógica - Método DELETE)
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategoria(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
        {
            return NotFound();
        }

        // Aplicação da tua Regra de Negócio: Exclusão Lógica
        categoria.Ativo = false;
        
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Método auxiliar privado para verificar se a categoria existe
    private bool CategoriaExists(int id)
    {
        return _context.Categorias.Any(e => e.Id == id);
    }
}