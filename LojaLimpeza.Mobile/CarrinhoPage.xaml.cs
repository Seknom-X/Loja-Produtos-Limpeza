using LojaLimpeza.Mobile.Services;

namespace LojaLimpeza.Mobile;

public partial class CarrinhoPage : ContentPage
{
    private readonly CarrinhoService _carrinhoService;
    private readonly LojaApiService _apiService; // 1. Adicionamos a referência à API

    // 2. Injetamos a API no construtor
    public CarrinhoPage(CarrinhoService carrinhoService, LojaApiService apiService)
    {
        InitializeComponent();
        _carrinhoService = carrinhoService;
        _apiService = apiService; 
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CarregarCarrinho();
    }

    private void CarregarCarrinho()
    {
        ItensCollectionView.ItemsSource = null; 
        ItensCollectionView.ItemsSource = _carrinhoService.Itens;
        
        TotalLabel.Text = $"Total: R$ {_carrinhoService.CalcularTotal():F2}";
    }

    private async void OnFinalizarPedidoClicked(object sender, EventArgs e)
    {
        if (!_carrinhoService.Itens.Any())
        {
            await DisplayAlert("Aviso", "O teu carrinho está vazio!", "OK");
            return;
        }

        var total = _carrinhoService.CalcularTotal();
        
        // Aguardamos o texto (pode ser "Sucesso" ou um texto gigante de erro)
        // Vai ao OnFinalizarPedidoClicked e altera apenas esta linha:
    
    // Antes estava: string resultado = await _apiService.EnviarPedidoAsync(total);
    // AGORA FICA ASSIM:
    string resultado = await _apiService.EnviarPedidoAsync(total, _carrinhoService.Itens);

        if (resultado == "Sucesso")
        {
            await DisplayAlert("Sucesso!", "O teu pedido foi enviado para a loja!", "OK");
            _carrinhoService.LimparCarrinho();
            await Navigation.PopAsync(); 
        }
        else
        {
            // Agora o telemóvel vai mostrar-te o raio-X do problema real!
            await DisplayAlert("Diagnóstico da API", resultado, "OK");
        }
    }
}