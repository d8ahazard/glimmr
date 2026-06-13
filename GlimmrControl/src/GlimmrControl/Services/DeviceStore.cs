using System.Collections.ObjectModel;
using System.Text.Json;
using GlimmrControl.Models;

namespace GlimmrControl.Services;

/// <summary>
///     Persists the user's known Glimmr devices to <see cref="Preferences" /> and exposes them as an
///     observable collection. De-duplicates on device id (preferred) or host.
/// </summary>
public sealed class DeviceStore
{
	private const string Key = "glimmr_devices";

	public ObservableCollection<GlimmrDevice> Devices { get; } = new();

	public void Load()
	{
		Devices.Clear();
		var raw = Preferences.Get(Key, "");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return;
		}

		try
		{
			var list = JsonSerializer.Deserialize<List<GlimmrDevice>>(raw, GlimmrJson.Options);
			if (list != null)
			{
				foreach (var d in list)
				{
					Devices.Add(d);
				}
			}
		}
		catch
		{
			// corrupt store - start fresh
		}
	}

	public void Save()
	{
		try
		{
			Preferences.Set(Key, JsonSerializer.Serialize(Devices.ToList(), GlimmrJson.Options));
		}
		catch
		{
			// non-fatal
		}
	}

	/// <summary>Add a device or update the matching existing one. Returns the stored instance.</summary>
	public GlimmrDevice AddOrUpdate(GlimmrDevice device)
	{
		var existing = Find(device);
		if (existing != null)
		{
			existing.Host = device.Host;
			if (!string.IsNullOrEmpty(device.Name))
			{
				existing.Name = device.Name;
			}

			if (!string.IsNullOrEmpty(device.DeviceId))
			{
				existing.DeviceId = device.DeviceId;
			}

			existing.Reachability = device.Reachability;
			existing.DeviceMode = device.DeviceMode;
			Save();
			return existing;
		}

		Devices.Add(device);
		Save();
		return device;
	}

	public void Remove(GlimmrDevice device)
	{
		var existing = Find(device);
		if (existing != null)
		{
			Devices.Remove(existing);
			Save();
		}
	}

	public GlimmrDevice? Find(GlimmrDevice device)
	{
		if (!string.IsNullOrEmpty(device.DeviceId))
		{
			var byId = Devices.FirstOrDefault(d => d.DeviceId == device.DeviceId);
			if (byId != null)
			{
				return byId;
			}
		}

		return Devices.FirstOrDefault(d =>
			string.Equals(d.Host, device.Host, StringComparison.OrdinalIgnoreCase));
	}
}
