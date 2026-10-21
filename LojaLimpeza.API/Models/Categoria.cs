namespace LojaLimpeza.API.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    // Propriedade de navegação: Uma categoria tem muitos produtos (Relacionamento 1:N)
    public ICollection<Produto> Produtos { get; set; } = new List<Produto>();
}