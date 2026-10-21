using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using LojaLimpeza.API.Controllers;
using LojaLimpeza.API.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LojaLimpeza.Tests;

public class PedidosValidacaoTests
{
    // O Controller executa estas validacoes antes de consultar o banco.
    // Por isso, nos cenarios abaixo, nao e preciso criar um AppDbContext.
    private static PedidosController CriarControllerComUsuario()
    {
        var contexto = new DefaultHttpContext();
        var identidade = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "10") },
            "TesteLocal");

        contexto.User = new ClaimsPrincipal(identidade);

        return new PedidosController(null!)
        {
            ControllerContext = new ControllerContext { HttpContext = contexto }
        };
    }

    [Fact]
    public async Task CriarPedido_SemChaveIdempotencia_DeveRetornar400()
    {
        var controller = CriarControllerComUsuario();
        var entrada = new CriarPedidoDto
        {
            ChaveIdempotencia = Guid.Empty,
            Itens = new List<CriarItemPedidoDto>
            {
                new() { ProdutoId = 2, Quantidade = 2 }
            }
        };

        var resultado = await controller.CriarPedido(entrada);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task CriarPedido_SemItens_DeveRetornar400()
    {
        var controller = CriarControllerComUsuario();
        var entrada = new CriarPedidoDto
        {
            ChaveIdempotencia = Guid.NewGuid(),
            Itens = new List<CriarItemPedidoDto>()
        };

        var resultado = await controller.CriarPedido(entrada);

        var erro = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Contains("pelo menos um item", erro.Value?.ToString());
    }

    [Fact]
    public async Task CriarPedido_QuantidadeZero_DeveRetornar400()
    {
        var controller = CriarControllerComUsuario();
        var entrada = new CriarPedidoDto
        {
            ChaveIdempotencia = Guid.NewGuid(),
            Itens = new List<CriarItemPedidoDto>
            {
                new() { ProdutoId = 2, Quantidade = 0 }
            }
        };

        var resultado = await controller.CriarPedido(entrada);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task CriarPedido_ProdutoDuplicado_DeveRetornar400()
    {
        var controller = CriarControllerComUsuario();
        var entrada = new CriarPedidoDto
        {
            ChaveIdempotencia = Guid.NewGuid(),
            Itens = new List<CriarItemPedidoDto>
            {
                new() { ProdutoId = 2, Quantidade = 1 },
                new() { ProdutoId = 2, Quantidade = 2 }
            }
        };

        var resultado = await controller.CriarPedido(entrada);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task ConfirmarPedido_IdInvalido_DeveRetornar400()
    {
        var controller = CriarControllerComUsuario();

        var resultado = await controller.ConfirmarPedido(0);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }
}
