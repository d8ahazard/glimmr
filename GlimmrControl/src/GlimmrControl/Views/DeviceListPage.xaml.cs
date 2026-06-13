using GlimmrControl.ViewModels;

namespace GlimmrControl.Views;

public partial class DeviceListPage : ContentPage
{
	private readonly DeviceListViewModel _vm;

	public DeviceListPage(DeviceListViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_vm.AppearingCommand.CanExecute(null))
		{
			_vm.AppearingCommand.Execute(null);
		}
	}
}
