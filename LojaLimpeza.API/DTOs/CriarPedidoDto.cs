namespace LojaLimpeza.API.DTOs;

public class CriarPedidoDto
{
    public Guid ChaveIdempotencia { get; set; }
    public List<CriarItemPedidoDto> Itens { get; set; } = new();
}

public class CriarItemPedidoDto
{
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
}
