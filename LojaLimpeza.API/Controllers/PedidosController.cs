using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LojaLimpeza.API.Data;
using LojaLimpeza.API.DTOs;
using LojaLimpeza.API.Models;
using LojaLimpeza.API.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LojaLimpeza.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PedidosController : ControllerBase
{
    private readonly AppDbContext _context;

    public PedidosController(AppDbContext context)
    {
        _context = context;
    }

    // O painel administrativo consulta os pedidos por meio da API.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetTodosPedidos()
    {
        var pedidos = await _context.Pedidos.AsNoTracking()
            .OrderByDescending(p => p.DataCriacao)
            .Select(p => new
            {
                p.Id,
                p.UsuarioId,
                p.DataCriacao,
                p.ValorTotal,
                p.Status,
                QuantidadeItens = p.Itens.Count
            })
            .ToListAsync();

        return Ok(pedidos);
    }

    // O cliente cria um pedido Pendente, sem descontar estoque.
    [HttpPost]
    [Authorize(Roles = "Cliente,Admin")]
    public async Task<IActionResult> CriarPedido([FromBody] CriarPedidoDto entrada)
    {
        var identificacao = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(identificacao, out int usuarioId) || usuarioId <= 0)
            return Unauthorized("Não foi possível identificar o usuário.");

        if (entrada.ChaveIdempotencia == Guid.Empty)
            return BadRequest("Informe uma ChaveIdempotencia válida.");

        if (entrada.Itens == null || entrada.Itens.Count == 0)
            return BadRequest("O pedido deve conter pelo menos um item.");

        if (entrada.Itens.Any(i => i.ProdutoId <= 0 || i.Quantidade <= 0))
            return BadRequest("Os itens precisam ter produto e quantidade válidos.");

        if (entrada.Itens.Select(i => i.ProdutoId).Distinct().Count() != entrada.Itens.Count)
            return BadRequest("Não repita o mesmo produto: envie apenas um item com a quantidade total.");

        // Reenvios da mesma operação devem resultar no mesmo pedido.
        var existente = await BuscarPedidoPelaChave(entrada.ChaveIdempotencia);
        if (existente != null)
            return ResultadoIdempotente(existente, usuarioId, entrada);

        // List<int>.Contains evita a escolha da sobrecarga baseada em ReadOnlySpan<int>
        // na tradução da expressão LINQ pelo Entity Framework Core.
        var ids = entrada.Itens.Select(i => i.ProdutoId).ToList();
        var produtos = await _context.Produtos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var itens = new List<ItemPedido>();
        foreach (var solicitado in entrada.Itens)
        {
            if (!produtos.TryGetValue(solicitado.ProdutoId, out var produto) || !produto.Ativo)
                return BadRequest($"Produto {solicitado.ProdutoId} inexistente ou inativo.");

            if (produto.Preco <= 0)
                return BadRequest($"Produto {produto.Id} sem preço válido.");

            if (produto.QuantidadeEstoque < solicitado.Quantidade)
                return Conflict($"Estoque insuficiente para {produto.Nome}.");

            // O valor unitário é retirado do MySQL, nunca do aplicativo.
            itens.Add(new ItemPedido
            {
                ProdutoId = produto.Id,
                Quantidade = solicitado.Quantidade,
                PrecoUnitario = produto.Preco
            });
        }

        var pedido = new Pedido
        {
            UsuarioId = usuarioId,
            Status = StatusPedido.Pendente,
            DataCriacao = DateTime.UtcNow,
            ChaveIdempotencia = entrada.ChaveIdempotencia,
            Itens = itens,
            ValorTotal = itens.Sum(i => i.PrecoUnitario * i.Quantidade)
        };

        _context.Pedidos.Add(pedido);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Trata a corrida em que a mesma chave é enviada simultaneamente.
            var criadoPorOutraRequisicao = await BuscarPedidoPelaChave(entrada.ChaveIdempotencia);
            if (criadoPorOutraRequisicao == null)
                throw;

            return ResultadoIdempotente(criadoPorOutraRequisicao, usuarioId, entrada);
        }

        return StatusCode(201, new { pedido.Id, pedido.Status, pedido.ValorTotal });
    }

    // Confirmar status e baixar o estoque são uma única transação.
    [HttpPut("{id:int}/confirmar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmarPedido(int id)
    {
        if (id <= 0)
            return BadRequest("Identificador do pedido inválido.");

        await using var transacao = await _context.Database.BeginTransactionAsync();

        // UPDATE condicional bloqueia confirmações simultâneas do mesmo pedido.
        int alterados = await _context.Pedidos
            .Where(p => p.Id == id && p.Status == StatusPedido.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedido.Confirmado));

        if (alterados == 0)
        {
            bool existe = await _context.Pedidos.AsNoTracking().AnyAsync(p => p.Id == id);
            return existe ? Conflict("Somente pedidos Pendentes podem ser confirmados.")
                          : NotFound("Pedido não encontrado.");
        }

        var itens = await _context.ItensPedido.AsNoTracking()
            .Where(i => i.PedidoId == id)
            .OrderBy(i => i.ProdutoId)
            .ToListAsync();

        if (itens.Count == 0)
        {
            await transacao.RollbackAsync();
            return Conflict("Não é possível confirmar um pedido sem itens.");
        }

        foreach (var item in itens)
        {
            if (item.Quantidade <= 0)
            {
                await transacao.RollbackAsync();
                return Conflict("Há um item com quantidade inválida.");
            }

            // UPDATE atômico: só desconta se houver saldo suficiente.
            int atualizados = await _context.Produtos
                .Where(p => p.Id == item.ProdutoId && p.Ativo
                    && p.QuantidadeEstoque >= item.Quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    p => p.QuantidadeEstoque,
                    p => p.QuantidadeEstoque - item.Quantidade));

            if (atualizados != 1)
            {
                await transacao.RollbackAsync();
                return Conflict($"Estoque insuficiente ou produto inativo: {item.ProdutoId}.");
            }
        }

        await transacao.CommitAsync();
        return Ok(new { Id = id, Status = StatusPedido.Confirmado, Mensagem = "Pedido confirmado e estoque atualizado." });
    }

    // ETAPA 9.2: Cliente cancela somente pedidos proprios Pendentes.
    // Admin cancela qualquer Pendente ou Confirmado; ao cancelar Confirmado,
    // devolve o estoque exatamente uma vez, dentro de uma transacao.
    [HttpPut("{id:int}/cancelar")]
    [Authorize(Roles = "Cliente,Admin")]
    public async Task<IActionResult> CancelarPedido(int id)
    {
        if (id <= 0)
            return BadRequest("Identificador do pedido inválido.");

        var identificacao = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(identificacao, out int usuarioId) || usuarioId <= 0)
            return Unauthorized("Não foi possível identificar o usuário.");

        bool ehAdmin = User.IsInRole("Admin");

        if (!ehAdmin)
        {
            // O cliente nao pode cancelar pedidos Confirmados, mesmo sendo o dono.
            // O UPDATE condicional tambem impede a confirmacao de um Pendente ja cancelado.
            int alteradosCliente = await _context.Pedidos
                .Where(p => p.Id == id
                    && p.UsuarioId == usuarioId
                    && p.Status == StatusPedido.Pendente)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedido.Cancelado));

            if (alteradosCliente == 1)
                return Ok(new
                {
                    Id = id,
                    Status = StatusPedido.Cancelado,
                    Mensagem = "Pedido pendente cancelado. O estoque não foi alterado."
                });

            // Nao informa ao cliente se o pedido pertence a outra pessoa.
            bool pedidoDoCliente = await _context.Pedidos.AsNoTracking()
                .AnyAsync(p => p.Id == id && p.UsuarioId == usuarioId);

            return pedidoDoCliente
                ? Conflict("Somente pedidos Pendentes podem ser cancelados pelo Cliente.")
                : NotFound("Pedido não encontrado.");
        }

        // Admin: protege alteracao de status e movimentacao de estoque
        // na MESMA transacao (importante para cancelamento de Confirmado).
        await using var transacao = await _context.Database.BeginTransactionAsync();

        int pendenteAlterado = await _context.Pedidos
            .Where(p => p.Id == id && p.Status == StatusPedido.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedido.Cancelado));

        if (pendenteAlterado == 1)
        {
            await transacao.CommitAsync();
            return Ok(new
            {
                Id = id,
                Status = StatusPedido.Cancelado,
                Mensagem = "Pedido pendente cancelado. O estoque não foi alterado."
            });
        }

        // A linha do pedido e alterada de Confirmado para Cancelado
        // antes da devolucao. Atualizacoes concorrentes do mesmo pedido
        // so conseguem passar por essa condicao UMA vez.
        int confirmadoAlterado = await _context.Pedidos
            .Where(p => p.Id == id && p.Status == StatusPedido.Confirmado)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedido.Cancelado));

        if (confirmadoAlterado == 1)
        {
            var itens = await _context.ItensPedido.AsNoTracking()
                .Where(i => i.PedidoId == id)
                .OrderBy(i => i.ProdutoId)
                .ToListAsync();

            if (itens.Count == 0 || itens.Any(i => i.Quantidade <= 0))
            {
                await transacao.RollbackAsync();
                return Conflict("Pedido sem itens válidos. Cancelamento não realizado.");
            }

            foreach (var item in itens)
            {
                // Produto inativo tambem recebe devolucao: Ativo nao e filtro.
                // O limite evita ultrapassar o maximo de um inteiro com sinal.
                int atualizado = await _context.Produtos
                    .Where(p => p.Id == item.ProdutoId
                        && p.QuantidadeEstoque <= int.MaxValue - item.Quantidade)
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        p => p.QuantidadeEstoque,
                        p => p.QuantidadeEstoque + item.Quantidade));

                if (atualizado != 1)
                {
                    // Desfaz o status e todas as devolucoes ja efetuadas.
                    await transacao.RollbackAsync();
                    return Conflict($"Nao foi possivel devolver estoque ao produto {item.ProdutoId}.");
                }
            }

            await transacao.CommitAsync();
            return Ok(new
            {
                Id = id,
                Status = StatusPedido.Cancelado,
                Mensagem = "Pedido confirmado cancelado e estoque devolvido."
            });
        }

        bool existe = await _context.Pedidos.AsNoTracking().AnyAsync(p => p.Id == id);
        return existe
            ? Conflict("Somente pedidos Pendentes ou Confirmados podem ser cancelados pelo Admin.")
            : NotFound("Pedido não encontrado.");
    }

    private Task<Pedido?> BuscarPedidoPelaChave(Guid chave) =>
        _context.Pedidos.AsNoTracking().Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.ChaveIdempotencia == chave);

    private IActionResult ResultadoIdempotente(Pedido existente, int usuarioId, CriarPedidoDto entrada)
    {
        bool mesmosItens = existente.Itens.Count == entrada.Itens.Count &&
            entrada.Itens.All(i => existente.Itens.Any(
                x => x.ProdutoId == i.ProdutoId && x.Quantidade == i.Quantidade));

        if (existente.UsuarioId != usuarioId || !mesmosItens)
            return Conflict("Essa chave de idempotência já foi utilizada em outro pedido.");

        return Ok(new { existente.Id, existente.Status, existente.ValorTotal, Reenvio = true });
    }
}
