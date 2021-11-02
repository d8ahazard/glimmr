#region

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

#endregion

namespace GlimmrTray {
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
			if (true) {
				try {
					var dir = AppDomain.CurrentDomain.BaseDirectory;
					var path = Path.Combine(dir, "Glimmr.exe");
					var baseDirectory = dir;
					Run(path, baseDirectory);
				} catch (CmdArgumentException e) {
					Console.Error.WriteLine(e.Message);
					Environment.ExitCode = -1;
				} catch (Exception e) {
					Console.Error.WriteLine(e.ToString());
					Environment.ExitCode = -1;
				}
			}
		}

		private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
			IntPtr handle, int idObject, int idChild, uint dwEventThread, uint wmsEventTime) {
			SwitchWindow(handle);
			_winShown = false;
		}


		private static void Run(string path, string baseDirectory) {
			var trayIcon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

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

			var trapText = "Glimmr TV System Tray";
			var contextMenu = new ContextMenu();
			var menuItem0 = new MenuItem { Index = 0, Text = "Show &UI" };
			var menuItem1 = new MenuItem { Index = 1, Text = "Open &Data Folder" };
			var menuItem2 = new MenuItem { Index = 2, Text = "E&xit" };
			menuItem0.Click += ShowUi;
			menuItem1.Click += OpenData;
			menuItem2.Click += CloseApp;
			contextMenu.MenuItems.Add(menuItem0);
			contextMenu.MenuItems.Add(menuItem1);
			contextMenu.MenuItems.Add(menuItem2);
			var tray = new NotifyIcon {
				Icon = trayIcon,
				Text = trapText,
				BalloonTipTitle = trapText,
				BalloonTipText = trapText,
				Visible = true,
				ContextMenu = contextMenu
			};
			SetWinEventHook(EventSystemMinimizeStart, EventSystemMinimizeStart, _procId,
				ProcDelegate, 0, 0, WinEventOutOfContext);

			tray.MouseDoubleClick += (s, e) => {
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
			Process.Start("http://localhost");
		}

		private static void CloseApp(object sender, EventArgs e) {
			Application.Exit();
		}

		private static string GetTrayText(Process p) {
			const int kMaxCount = 63;
			Thread.Sleep(500); // make MainWindowTitle not empty 
			var title = p.MainWindowTitle;
			if (title.Length > kMaxCount) {
				title = title.Substring(title.Length - kMaxCount);
			}

			return title;
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
			p.Exited += (s, e) => { Environment.Exit(0); };
			AppDomain.CurrentDomain.ProcessExit += (s, e) => { ExitProcess(p); };
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
}