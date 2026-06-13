namespace GlimmrControl;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// Glimmr's UI is dark-first; match the web app regardless of system theme.
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell()) { Title = "Glimmr" };
	}
}
