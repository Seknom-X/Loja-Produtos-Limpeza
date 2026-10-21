using LojaLimpeza.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LojaLimpeza.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Representação das tabelas no banco de dados
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Produto> Produtos { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<ItemPedido> ItensPedido { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Garantir precisão dos valores monetários
        modelBuilder.Entity<Produto>().Property(p => p.Preco).HasPrecision(18, 2);
        modelBuilder.Entity<Pedido>().Property(p => p.ValorTotal).HasPrecision(18, 2);
        modelBuilder.Entity<ItemPedido>().Property(i => i.PrecoUnitario).HasPrecision(18, 2);
        
        // Regra de Negócio: Impedir duplicação de pedidos forçando a ChaveIdempotencia a ser única
        modelBuilder.Entity<Pedido>().HasIndex(p => p.ChaveIdempotencia).IsUnique();
        
        // Regra de Negócio: Garantir que não existam clientes com e-mails duplicados
        modelBuilder.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
    }
}