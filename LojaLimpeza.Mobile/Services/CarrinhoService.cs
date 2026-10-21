using LojaLimpeza.Mobile.Models;

namespace LojaLimpeza.Mobile.Services;

public class CarrinhoService
{
    // A lista que funciona como o nosso cesto de compras
    public List<Produto> Itens { get; private set; } = new List<Produto>();

    public void AdicionarProduto(Produto produto)
    {
        Itens.Add(produto);
    }

    public void LimparCarrinho()
    {
        Itens.Clear();
    }

    public decimal CalcularTotal()
    {
        return Itens.Sum(p => p.Preco);
    }
}