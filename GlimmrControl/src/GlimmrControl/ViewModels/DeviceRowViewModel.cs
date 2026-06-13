using CommunityToolkit.Mvvm.ComponentModel;
using GlimmrControl.Models;
using GlimmrControl.Services;

namespace GlimmrControl.ViewModels;

/// <summary>
///     A single device row in the control screen: brightness slider (debounced, like the web UI's
///     commit-on-release) and an enable toggle (immediate). Pushes changes through the live
///     connection.
/// </summary>
public partial class DeviceRowViewModel : ObservableObject
{
	private readonly GlimmrConnection _connection;
	private CancellationTokenSource? _debounce;
	private bool _suppress;

	[ObservableProperty] private int _brightness;
	[ObservableProperty] private bool _enable;
	[ObservableProperty] private int _maxBrightness = 100;
	[ObservableProperty] private string _name = "";

	public DeviceRowViewModel(DeviceData data, GlimmrConnection connection)
	{
		_connection = connection;
		Data = data;
		Apply(data);
	}

	public DeviceData Data { get; private set; }
	public string Id => Data.Id;

	/// <summary>Refresh from a server-pushed device update without re-triggering commits.</summary>
	public void Apply(DeviceData data)
	{
		Data = data;
		_suppress = true;
		Name = string.IsNullOrEmpty(data.Name) ? data.Id : data.Name;
		MaxBrightness = data.MaxBrightness is > 0 ? data.MaxBrightness.Value : 100;
		Brightness = Math.Clamp(data.Brightness, 0, MaxBrightness);
		Enable = data.Enable;
		_suppress = false;
	}

	partial void OnBrightnessChanged(int value)
	{
		if (_suppress)
		{
			return;
		}

		// Debounce: every commit is a hub round-trip, so coalesce rapid slider movement.
		_debounce?.Cancel();
		_debounce = new CancellationTokenSource();
		var token = _debounce.Token;
		Task.Run(async () =>
		{
			try
			{
				await Task.Delay(350, token);
				if (token.IsCancellationRequested)
				{
					return;
				}

				Data.Brightness = value;
				await _connection.UpdateDeviceAsync(Data);
			}
			catch (TaskCanceledException)
			{
				// superseded by a newer value
			}
		}, token);
	}

	partial void OnEnableChanged(bool value)
	{
		if (_suppress)
		{
			return;
		}

		Data.Enable = value;
		_ = _connection.UpdateDeviceAsync(Data);
	}
}
