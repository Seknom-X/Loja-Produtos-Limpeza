using LojaLimpeza.API.Data;
using LojaLimpeza.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LojaLimpeza.API.Controllers;

[ApiController]
[Route("api/[controller]")]

public class ProdutosController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProdutosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProdutos()
    {
        var produtos = await _context.Produtos.ToListAsync();
        return Ok(produtos);
    }
    [Authorize(Roles = "Admin")]

    [HttpPost]
    public async Task<IActionResult> CreateProdutos(Produto produto)
    {
        _context.Produtos.Add(produto);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProdutos), new { id = produto.Id }, produto);
    }
    // Endpoint para ATUALIZAR um produto existente (Método PUT)
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduto(int id, Produto produtoAtualizado)
    {
        // 1. Verifica se o ID do URL corresponde ao ID do corpo do JSON
        if (id != produtoAtualizado.Id)
        {
            return BadRequest("O ID do produto não corresponde.");
        }

        // 2. Avisa o Entity Framework que este objeto foi modificado
        _context.Entry(produtoAtualizado).State = EntityState.Modified;

        try
        {
            // 3. Tenta guardar na base de dados
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 4. Se o produto não existir mais na base, retorna 404 (Not Found)
            if (!ProdutoExists(id))
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
    public async Task<IActionResult> DeleteProduto(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);
        if (produto == null)
        {
            return NotFound();
        }

        // Aplicação da tua Regra de Negócio: Exclusão Lógica
        produto.Ativo = false;
        
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Método auxiliar privado para verificar se o produto existe
    private bool ProdutoExists(int id)
    {
        return _context.Produtos.Any(e => e.Id == id);
    }
}