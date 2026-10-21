using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LojaLimpeza.API.Controllers;
using LojaLimpeza.API.Data;
using LojaLimpeza.API.DTOs;
using LojaLimpeza.API.Models;
using LojaLimpeza.API.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace LojaLimpeza.Tests;

// Estes testes acessam MySQL de verdade. NAO testam o middleware HTTP/JWT.
// Uma colecao serial impede que os testes de integracao concorram entre si.
[CollectionDefinition("LojaLimpeza_Banco_MySQL_Testes", DisableParallelization = true)]
public sealed class ColecaoBancoMySqlTestes { }

[Collection("LojaLimpeza_Banco_MySQL_Testes")]
public sealed class PedidosMySqlIntegracaoTests
{
    [Fact]
    public async Task CriarPedido_PendenteNaoDescontaEstoque()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        var chave = Guid.NewGuid();

        await using (var contexto = cenario.NovoContexto())
        {
            var resultado = await CriarController(contexto, cenario.UsuarioId, "Cliente")
                .CriarPedido(NovaEntrada(cenario.ProdutoId, 2, chave));

            var criado = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(201, criado.StatusCode);
        }

        await using var verificacao = cenario.NovoContexto();
        var pedido = await verificacao.Pedidos.AsNoTracking()
            .SingleAsync(p => p.ChaveIdempotencia == chave);
        Assert.Equal(StatusPedido.Pendente, pedido.Status);
        Assert.Equal(7.00m, pedido.ValorTotal);
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task ConfirmarPedido_DescontaUmaVezERecusaRepeticao()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        var id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
        {
            var resultado = await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id);
            Assert.IsType<OkObjectResult>(resultado);
        }

        await using (var contexto = cenario.NovoContexto())
        {
            var segundaTentativa = await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id);
            Assert.IsType<ConflictObjectResult>(segundaTentativa);
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(20, await EstoqueAsync(verificacao, cenario.ProdutoId));
        Assert.Equal(StatusPedido.Confirmado, await StatusAsync(verificacao, id));
    }

    [Fact]
    public async Task ConfirmarPedido_EstoqueInsuficienteMantemPedidoPendente()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        // Estoque inicial: 22. Dois pedidos de 15 podem ser criados como Pendentes.
        var pedidoA = await CriarPedidoPendenteAsync(cenario, 15);
        var pedidoB = await CriarPedidoPendenteAsync(cenario, 15);

        await using (var contexto = cenario.NovoContexto())
        {
            var resultado = await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(pedidoA);
            Assert.IsType<OkObjectResult>(resultado);
        }

        await using (var contexto = cenario.NovoContexto())
        {
            var resultado = await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(pedidoB);
            Assert.IsType<ConflictObjectResult>(resultado);
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(7, await EstoqueAsync(verificacao, cenario.ProdutoId));
        Assert.Equal(StatusPedido.Confirmado, await StatusAsync(verificacao, pedidoA));
        Assert.Equal(StatusPedido.Pendente, await StatusAsync(verificacao, pedidoB));
    }

    [Fact]
    public async Task CriarPedido_ReenvioMesmaChaveNaoDuplicaPedido()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        var chave = Guid.NewGuid();

        await using (var contexto = cenario.NovoContexto())
        {
            var primeiro = await CriarController(contexto, cenario.UsuarioId, "Cliente")
                .CriarPedido(NovaEntrada(cenario.ProdutoId, 2, chave));
            var criado = Assert.IsType<ObjectResult>(primeiro);
            Assert.Equal(201, criado.StatusCode);
        }

        await using (var contexto = cenario.NovoContexto())
        {
            var reenvio = await CriarController(contexto, cenario.UsuarioId, "Cliente")
                .CriarPedido(NovaEntrada(cenario.ProdutoId, 2, chave));
            Assert.IsType<OkObjectResult>(reenvio);
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(1, await verificacao.Pedidos.AsNoTracking()
            .CountAsync(p => p.ChaveIdempotencia == chave));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    // ETAPA 9: Cancelamento de pedidos Pendentes (MySQL real).
    [Fact]
    public async Task CancelarPedido_ClienteDonoCancelaPendenteSemBaixarEstoque()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
        {
            var resposta = await CriarController(contexto, cenario.UsuarioId, "Cliente")
                .CancelarPedido(id);
            Assert.IsType<OkObjectResult>(resposta);
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_AdminPodeCancelarPendenteDeOutroUsuario()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
        {
            // O Admin possui uma identidade diferente do dono do pedido.
            var resposta = await CriarController(contexto, cenario.UsuarioId + 100000, "Admin")
                .CancelarPedido(id);
            Assert.IsType<OkObjectResult>(resposta);
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_ClienteNaoPodeCancelarPedidoDeOutroUsuario()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
        {
            var resposta = await CriarController(contexto, cenario.UsuarioId + 100000, "Cliente")
                .CancelarPedido(id);
            // 404 evita expor a existencia de pedidos de outros clientes.
            Assert.IsType<NotFoundObjectResult>(resposta);
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Pendente, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_SegundaTentativaRetornaConflito()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
        {
            var controller = CriarController(contexto, cenario.UsuarioId, "Cliente");
            Assert.IsType<OkObjectResult>(await controller.CancelarPedido(id));
            Assert.IsType<ConflictObjectResult>(await controller.CancelarPedido(id));
        }

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    // ETAPA 9.2: Apenas Admin pode cancelar Confirmado e devolver estoque.
    [Fact]
    public async Task CancelarPedido_ConfirmadoAdminDevolveEstoqueApenasUmaVez()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        // Cliente continua proibido de cancelar Confirmado, mesmo sendo dono.
        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<ConflictObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Cliente")
                .CancelarPedido(id));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .CancelarPedido(id));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<ConflictObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .CancelarPedido(id));

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_ConfirmadoAdminDeOutroUsuarioPodeCancelar()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId + 100000, "Admin")
                .CancelarPedido(id));

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_ConfirmadoProdutoInativoDevolveEstoque()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        await using (var contexto = cenario.NovoContexto())
            await contexto.Produtos.Where(p => p.Id == cenario.ProdutoId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Ativo, false));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .CancelarPedido(id));

        await using var verificacao = cenario.NovoContexto();
        var produto = await verificacao.Produtos.AsNoTracking().SingleAsync(p => p.Id == cenario.ProdutoId);
        Assert.False(produto.Ativo);
        Assert.Equal(22, produto.QuantidadeEstoque);
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
    }

    [Theory]
    [InlineData(StatusPedido.EmPreparacao)]
    [InlineData(StatusPedido.Concluido)]
    public async Task CancelarPedido_AdminNaoCancelaPedidoForaDosEstadosPermitidos(StatusPedido status)
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        await using (var contexto = cenario.NovoContexto())
            await contexto.Pedidos.Where(p => p.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<ConflictObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .CancelarPedido(id));

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(status, await StatusAsync(verificacao, id));
        Assert.Equal(20, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_ConfirmadoComEstoqueNoLimiteReverteStatus()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        // Cenario extremo: nao cabe a devolucao no campo inteiro de estoque.
        await using (var contexto = cenario.NovoContexto())
            await contexto.Produtos.Where(p => p.Id == cenario.ProdutoId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.QuantidadeEstoque, int.MaxValue));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<ConflictObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .CancelarPedido(id));

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Confirmado, await StatusAsync(verificacao, id));
        Assert.Equal(int.MaxValue, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task CancelarPedido_DoisAdminsSimultaneosDevolvemEstoqueUmaVez()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        await using var contextoA = cenario.NovoContexto();
        await using var contextoB = cenario.NovoContexto();
        var tarefaA = CriarController(contextoA, cenario.UsuarioId, "Admin").CancelarPedido(id);
        var tarefaB = CriarController(contextoB, cenario.UsuarioId + 100000, "Admin").CancelarPedido(id);
        var respostas = await Task.WhenAll(tarefaA, tarefaB);

        Assert.Single(respostas.OfType<OkObjectResult>());
        Assert.Single(respostas.OfType<ConflictObjectResult>());

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    [Fact]
    public async Task ConfirmarPedido_DepoisDeCancelarRetornaConflito()
    {
        await using var cenario = await CenarioTeste.CriarAsync();
        int id = await CriarPedidoPendenteAsync(cenario, 2);

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<OkObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Cliente")
                .CancelarPedido(id));

        await using (var contexto = cenario.NovoContexto())
            Assert.IsType<ConflictObjectResult>(await CriarController(contexto, cenario.UsuarioId, "Admin")
                .ConfirmarPedido(id));

        await using var verificacao = cenario.NovoContexto();
        Assert.Equal(StatusPedido.Cancelado, await StatusAsync(verificacao, id));
        Assert.Equal(22, await EstoqueAsync(verificacao, cenario.ProdutoId));
    }

    private static CriarPedidoDto NovaEntrada(int produtoId, int quantidade, Guid chave) => new()
    {
        ChaveIdempotencia = chave,
        Itens = new List<CriarItemPedidoDto>
        {
            new() { ProdutoId = produtoId, Quantidade = quantidade }
        }
    };

    private static PedidosController CriarController(AppDbContext contexto, int usuarioId, string perfil)
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new Claim(ClaimTypes.Role, perfil)
        }, "TesteLocalNaoJWT"));

        return new PedidosController(contexto)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static async Task<int> CriarPedidoPendenteAsync(CenarioTeste cenario, int quantidade)
    {
        var chave = Guid.NewGuid();
        await using var contexto = cenario.NovoContexto();
        var resultado = await CriarController(contexto, cenario.UsuarioId, "Cliente")
            .CriarPedido(NovaEntrada(cenario.ProdutoId, quantidade, chave));
        var criado = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(201, criado.StatusCode);
        return await contexto.Pedidos.AsNoTracking()
            .Where(p => p.ChaveIdempotencia == chave)
            .Select(p => p.Id).SingleAsync();
    }

    private static Task<int> EstoqueAsync(AppDbContext contexto, int id) =>
        contexto.Produtos.AsNoTracking().Where(p => p.Id == id)
            .Select(p => p.QuantidadeEstoque).SingleAsync();

    private static Task<StatusPedido> StatusAsync(AppDbContext contexto, int id) =>
        contexto.Pedidos.AsNoTracking().Where(p => p.Id == id)
            .Select(p => p.Status).SingleAsync();

    private sealed class CenarioTeste : IAsyncDisposable
    {
        private readonly DbContextOptions<AppDbContext> _opcoes;
        public int UsuarioId { get; private set; }
        public int CategoriaId { get; private set; }
        public int ProdutoId { get; private set; }

        private CenarioTeste(DbContextOptions<AppDbContext> opcoes) => _opcoes = opcoes;

        public AppDbContext NovoContexto() => new(_opcoes);

        public static async Task<CenarioTeste> CriarAsync()
        {
            // Falha ANTES de conectar ao banco se a variavel nao estiver explicita
            // ou se apontar para um banco/usuario diferente do exclusivo de testes.
            var conexao = Environment.GetEnvironmentVariable("LOJALIMPEZA_TESTES_CONNECTION");
            if (string.IsNullOrWhiteSpace(conexao))
                throw new InvalidOperationException(
                    "Defina LOJALIMPEZA_TESTES_CONNECTION com a conexao exclusiva dos testes.");

            var config = new MySqlConnectionStringBuilder(conexao);
            if (!string.Equals(config.Database, "LojaLimpeza_Testes", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(config.UserID, "loja_testes", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "PROTECAO: os testes exigem Database=LojaLimpeza_Testes e User ID=loja_testes.");

            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(conexao, ServerVersion.AutoDetect(conexao))
                .Options;

            var cenario = new CenarioTeste(opcoes);
            await using var contexto = cenario.NovoContexto();

            var usuario = new Usuario
            {
                Nome = "Usuario Teste Integracao",
                Email = $"teste-{Guid.NewGuid():N}@example.invalid",
                PasswordHash = "apenas-dados-ficticios",
                Perfil = PerfilUsuario.Cliente
            };
            var categoria = new Categoria { Nome = "Categoria Integracao " + Guid.NewGuid().ToString("N") };
            var produto = new Produto
            {
                Nome = "Produto Integracao " + Guid.NewGuid().ToString("N"),
                Categoria = categoria,
                Descricao = "Criado exclusivamente por testes xUnit",
                Preco = 3.50m,
                QuantidadeEstoque = 22,
                Ativo = true
            };

            contexto.Usuarios.Add(usuario);
            contexto.Categorias.Add(categoria);
            contexto.Produtos.Add(produto);
            await contexto.SaveChangesAsync();

            cenario.UsuarioId = usuario.Id;
            cenario.CategoriaId = categoria.Id;
            cenario.ProdutoId = produto.Id;
            return cenario;
        }

        public async ValueTask DisposeAsync()
        {
            await using var contexto = NovoContexto();

            // Excluir somente os dados pertencentes ao usuario/categoria/produto
            // criados por ESTE teste. Nao existe TRUNCATE, DROP ou EnsureDeleted.
            var ids = await contexto.Pedidos.AsNoTracking()
                .Where(p => p.UsuarioId == UsuarioId)
                .Select(p => p.Id).ToListAsync();
            foreach (var id in ids)
                await contexto.ItensPedido.Where(i => i.PedidoId == id).ExecuteDeleteAsync();

            await contexto.Pedidos.Where(p => p.UsuarioId == UsuarioId).ExecuteDeleteAsync();
            await contexto.Produtos.Where(p => p.Id == ProdutoId).ExecuteDeleteAsync();
            await contexto.Categorias.Where(c => c.Id == CategoriaId).ExecuteDeleteAsync();
            await contexto.Usuarios.Where(u => u.Id == UsuarioId).ExecuteDeleteAsync();
        }
    }
}
