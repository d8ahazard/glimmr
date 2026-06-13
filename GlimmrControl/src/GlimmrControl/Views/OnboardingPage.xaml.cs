using GlimmrControl.ViewModels;

namespace GlimmrControl.Views;

public partial class OnboardingPage : ContentPage
{
	public OnboardingPage(OnboardingViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
