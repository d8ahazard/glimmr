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

	private static IntPtr _procId;
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
		} catch (CmdArgumentException e) {
			Console.Error.WriteLine(e.Message);
			Environment.ExitCode = -1;
		} catch (Exception e) {
			Console.Error.WriteLine(e.ToString());
			Environment.ExitCode = -1;
		}
	}

	private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
		IntPtr handle, int idObject, int idChild, uint dwEventThread, uint wmsEventTime) {
		SwitchWindow(handle);
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

		var p = Process.Start(processStartInfo);
		ExitAtSame(p);
		SwitchWindow(GetConsoleWindow());
		if (p != null) {
			_procId = p.Handle;
			_winShown = true;
		}

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
		SetWinEventHook(EventSystemMinimizeStart, EventSystemMinimizeStart, _procId,
			ProcDelegate, 0, 0, WinEventOutOfContext);

		tray.MouseDoubleClick += (_, _) => {
			if (p != null) {
				SwitchWindow(p.MainWindowHandle);
			}

			ShowWindow(_procId, !_winShown ? SwHide : SwShow);
		};
		Application.Run();
	}

	private static void OpenData(object sender, EventArgs e) {
		Process.Start("explorer.exe", @"C:\ProgramData\Glimmr");
	}

	private static void ShowUi(object sender, EventArgs e) {
		Process.Start("explorer", "http://localhost");
	}

	private static void CloseApp(object sender, EventArgs e) {
		Application.Exit();
	}

	private static void ExitProcess(Process p) {
		try {
			p.Kill();
		} catch {
			// Ignored
		}
	}

	private static void ExitAtSame(Process p) {
		UnhookWinEvent(p.Handle);
		p.EnableRaisingEvents = true;
		p.Exited += (_, _) => { Environment.Exit(0); };
		AppDomain.CurrentDomain.ProcessExit += (_, _) => { ExitProcess(p); };
	}

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	private static void SwitchWindow(IntPtr hWnd) {
		var success = ShowWindow(hWnd, SwHide);
		if (success) {
			return;
		}

		_winShown = true;
		ShowWindow(hWnd, SwShow);
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetConsoleWindow();

	private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
		IntPtr window, int idObject, int idChild, uint dwEventThread, uint eventTime);
}