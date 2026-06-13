#region

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

#endregion

namespace GlimmrTray;

internal static class Program {
	private const uint EventSystemMinimizeStart = 0x0016;

	private const int SwHide = 0;
	private const int SwShow = 5;
	private const uint WinEventOutOfContext = 0;

	private static Process _glimmr;
	private static IntPtr _hookHandle;
	private static bool _winShown;

	// Need to ensure delegate is not collected while we're using it,
	// storing it in a class field is simplest way to do this.
	private static readonly WinEventDelegate ProcDelegate = WinEventProc;

	[DllImport("user32.dll")]
	private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr
			modWinEventProc, WinEventDelegate winEventProc, uint idProcess,
		uint idThread, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

	private static void Main() {
		try {
			var dir = AppDomain.CurrentDomain.BaseDirectory;
			var path = Path.Combine(dir, "Glimmr.exe");
			Run(path, dir);
		} catch (Exception e) {
			Console.Error.WriteLine(e.ToString());
			Environment.ExitCode = -1;
		}
	}

	private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
		IntPtr handle, int idObject, int idChild, uint dwEventThread, uint wmsEventTime) {
		// The Glimmr console window was minimized; hide it to the tray.
		ShowWindow(handle, SwHide);
		_winShown = false;
	}


	private static void Run(string path, string baseDirectory) {
		Icon trayIcon = null;
		Console.WriteLine("Path should be: " + path);
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			trayIcon = Icon.ExtractAssociatedIcon(Path.Combine(AppContext.BaseDirectory, "GlimmrTray.exe"));
		}

		var processStartInfo = new ProcessStartInfo {
			FileName = path,
			ErrorDialog = true,
			WorkingDirectory = baseDirectory
		};

		_glimmr = Process.Start(processStartInfo);
		if (_glimmr == null) {
			Console.Error.WriteLine("Unable to start Glimmr.");
			return;
		}

		ExitAtSame(_glimmr);
		_winShown = true;

		var trayText = "Glimmr TV System Tray";
		var contextMenu = new ContextMenuStrip();
		contextMenu.Items.Add("Show &UI", null, ShowUi);
		contextMenu.Items.Add("Open &Data Folder", null, OpenData);
		contextMenu.Items.Add("E&xit", null, CloseApp);
		var tray = new NotifyIcon {
			Icon = trayIcon,
			Text = trayText,
			BalloonTipTitle = trayText,
			BalloonTipText = trayText,
			Visible = true,
			ContextMenuStrip = contextMenu
		};

		// Watch for the Glimmr console window being minimized so we can hide
		// it to the tray. Scope the hook to the Glimmr process id.
		_hookHandle = SetWinEventHook(EventSystemMinimizeStart, EventSystemMinimizeStart, IntPtr.Zero,
			ProcDelegate, (uint)_glimmr.Id, 0, WinEventOutOfContext);

		tray.MouseDoubleClick += (_, _) => ToggleGlimmrWindow();
		Application.Run();
	}

	private static void ToggleGlimmrWindow() {
		if (_glimmr == null || _glimmr.HasExited) {
			return;
		}

		var hWnd = _glimmr.MainWindowHandle;
		if (hWnd == IntPtr.Zero) {
			return;
		}

		ShowWindow(hWnd, _winShown ? SwHide : SwShow);
		_winShown = !_winShown;
	}

	private static void OpenData(object sender, EventArgs e) {
		var dataDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Glimmr");
		Process.Start("explorer.exe", dataDir);
	}

	private static void ShowUi(object sender, EventArgs e) {
		Process.Start("explorer", "http://localhost");
	}

	private static void CloseApp(object sender, EventArgs e) {
		if (_hookHandle != IntPtr.Zero) {
			UnhookWinEvent(_hookHandle);
			_hookHandle = IntPtr.Zero;
		}

		// Stop Glimmr too - exiting the tray should shut the whole app down.
		ExitProcess(_glimmr);
		Application.Exit();
	}

	private static void ExitProcess(Process p) {
		try {
			if (p is { HasExited: false }) {
				p.Kill();
			}
		} catch {
			// Ignored
		}
	}

	private static void ExitAtSame(Process p) {
		p.EnableRaisingEvents = true;
		p.Exited += (_, _) => { Environment.Exit(0); };
		AppDomain.CurrentDomain.ProcessExit += (_, _) => { ExitProcess(p); };
	}

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
		IntPtr window, int idObject, int idChild, uint dwEventThread, uint eventTime);
}
