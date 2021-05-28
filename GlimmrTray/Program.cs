using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GlimmrTray {
  class Program {
    private const string kHelpCmdString = @"Usage: GlimmrTray.exe [-p filePath]
Arguments 
-p              : the application or document to start

Options
-a              : sets the set of command-line arguments to use when starting the application 
-d              : sets the working directory for the process to be started
-i              : sets the current icon of tray
-t              : sets the ToolTip text of tray
-h              : show the help message and exit   
";
    
    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
      IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr
        hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess,
      uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    const uint EVENT_OBJECT_WM_SIZE = 0x0005;
    private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    const uint WINEVENT_OUTOFCONTEXT = 0;
    
    private static IntPtr _procId;
    private static bool _winShown;

    // Need to ensure delegate is not collected while we're using it,
    // storing it in a class field is simplest way to do this.
    static WinEventDelegate procDelegate = WinEventProc;

    static void Main(string[] args) {
      
      if (true) {
        try {
          var cmds = Utils.GetCommondLines(args);
          if (cmds.ContainsKey("-h")) {
            ShowHelpInfo();
            return;
          }

          var dir = AppDomain.CurrentDomain.BaseDirectory;
          var path = Path.Combine(dir, "Glimmr.exe");
          var arguments = cmds.GetArgument("-a", true);
          var baseDirectory = dir;
          var icon = cmds.GetArgument("-i", true);
          var tip = cmds.GetArgument("-t", true);
          Run(path, arguments, icon, baseDirectory, tip);
          
        } catch (CmdArgumentException e) {
          Console.Error.WriteLine(e.Message);
          ShowHelpInfo();
          Environment.ExitCode = -1;
        } catch (Exception e) {
          Console.Error.WriteLine(e.ToString());
          Environment.ExitCode = -1;
        }
      } else {
        ShowHelpInfo();
        Environment.ExitCode = -1;
      }
    }
    
    static void WinEventProc(IntPtr hWinEventHook, uint eventType,
      IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
      SwitchWindow(hwnd);
      _winShown = false;
    }

    private static void ShowHelpInfo() {
      Console.Error.WriteLine(kHelpCmdString);
    }

    private static void Run(string path, string arguments, string icon, string baseDirectory, string tip) {
      Icon trayIcon;
      if (!string.IsNullOrWhiteSpace(icon)) {
        trayIcon = new Icon(icon);
      } else {
        trayIcon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
      }

      var processStartInfo = new ProcessStartInfo {
        FileName = path,
        ErrorDialog = true
      };
      if (!string.IsNullOrWhiteSpace(arguments)) {
        processStartInfo.Arguments = arguments;
      }
      if (!string.IsNullOrWhiteSpace(baseDirectory)) {
        processStartInfo.WorkingDirectory = baseDirectory;
      }

      var p = Process.Start(processStartInfo);
      ExitAtSame(p);
      SwitchWindow(GetConsoleWindow());
      if (p != null) {
        _procId = p.Handle;
        _winShown = true;
      }

      var trapText = !string.IsNullOrWhiteSpace(tip) ? tip : GetTrayText(p);
      var contextMenu = new ContextMenu();
      var menuItem0 = new MenuItem {Index = 0, Text = "Show &UI"};
      var menuItem1 = new MenuItem {Index = 1, Text = "Open &Data Folder"};
      var menuItem2 = new MenuItem {Index = 2, Text = "E&xit"};
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
      var hhook = SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZESTART, _procId,
        procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

      tray.MouseDoubleClick += (s, e) => {
        SwitchWindow(p.MainWindowHandle);
        if (!_winShown) {
          ShowWindow(_procId, SW_HIDE);
        } else {
          ShowWindow(_procId, SW_SHOW);
        }
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
      Thread.Sleep(500);  // make MainWindowTitle not empty 
      var title = p.MainWindowTitle; ;
      if (title.Length > kMaxCount) {
        title = title.Substring(title.Length - kMaxCount);
      }
      return title;
    }

    private static void ExitProcess(Process p) {
      try {
        p.Kill();
      } catch {
      }
    }

    private static void ExitAtSame(Process p) {
      p.EnableRaisingEvents = true;
      p.Exited += (s, e) => {
        Environment.Exit(0);
      };
      AppDomain.CurrentDomain.ProcessExit += (s, e) => {
        ExitProcess(p);
      };
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private static void SwitchWindow(IntPtr hWnd) {
      var success = ShowWindow(hWnd, SW_HIDE);
      if (!success) {
        _winShown = true;
        ShowWindow(hWnd, SW_SHOW);
      }
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();
  }
}
