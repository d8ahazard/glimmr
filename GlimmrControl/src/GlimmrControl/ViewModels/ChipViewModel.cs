using CommunityToolkit.Mvvm.ComponentModel;

namespace GlimmrControl.ViewModels;

/// <summary>
///     A selectable chip (lighting mode, ambient/audio scene, ...). Its fill/border/text follow the
///     live accent color when active and the flat OLED palette otherwise - matching the web UI's
///     accent-driven chip styling.
/// </summary>
public partial class ChipViewModel : ObservableObject
{
	private static readonly Color InactiveBg = Color.FromArgb("#0A0A0C");
	private static readonly Color InactiveBorder = Color.FromRgba(255, 255, 255, 18);
	private static readonly Color InactiveText = Color.FromArgb("#9B9BA6");

	private Color _accent = Color.FromArgb("#46C8FF");

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ChipBackground), nameof(ChipBorder), nameof(ChipText))]
	private bool _isActive;

	public ChipViewModel(int id, string name, string glyph = "")
	{
		Id = id;
		Name = name;
		Glyph = glyph;
	}

	public int Id { get; }
	public string Name { get; }

	/// <summary>SVG path data for the chip's icon (empty for text-only scene chips).</summary>
	public string Glyph { get; }

	public bool HasGlyph => !string.IsNullOrEmpty(Glyph);

	public Color Accent
	{
		get => _accent;
		set
		{
			if (_accent.Equals(value))
			{
				return;
			}

			_accent = value;
			OnPropertyChanged(nameof(ChipBackground));
			OnPropertyChanged(nameof(ChipBorder));
			OnPropertyChanged(nameof(ChipText));
		}
	}

	public Color ChipBackground => IsActive
		? _accent.WithAlpha(0.12f)
		: InactiveBg;

	public Color ChipBorder => IsActive
		? _accent.WithAlpha(0.55f)
		: InactiveBorder;

	public Color ChipText => IsActive ? _accent : InactiveText;
}
