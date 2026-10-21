namespace LojaLimpeza.Mobile;

public partial class App : Application
{
    public App(MainPage mainPage)
    {
        InitializeComponent();
        
        // Envolvemos a página principal num "NavigationPage"
        MainPage = new NavigationPage(mainPage); 
    }
}