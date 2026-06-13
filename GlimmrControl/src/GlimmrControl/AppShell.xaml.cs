using GlimmrControl.Views;

namespace GlimmrControl;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Routes for detail navigation (devices list is the Shell root).
		Routing.RegisterRoute("onboarding", typeof(OnboardingPage));
		Routing.RegisterRoute("control", typeof(ControlPage));
		Routing.RegisterRoute("advanced", typeof(AdvancedSettingsPage));
	}
}
