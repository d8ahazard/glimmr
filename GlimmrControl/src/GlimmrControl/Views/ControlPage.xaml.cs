using GlimmrControl.ViewModels;

namespace GlimmrControl.Views;

public partial class ControlPage : ContentPage
{
	private readonly ControlViewModel _vm;
	private bool _connected;

	public ControlPage(ControlViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (!_connected)
		{
			_connected = true;
			await _vm.ConnectAsync();
		}
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		// Tear down the live connection only when this page is popped off the stack (e.g. back to
		// the device list) - not when pushing the Advanced settings page on top of it.
		var stillInStack = Shell.Current?.Navigation?.NavigationStack.Contains(this) == true;
		if (!stillInStack)
		{
			_vm.Dispose();
		}
	}
}
