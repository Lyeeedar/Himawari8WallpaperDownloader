using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Himawari8Downloader
{
	public class TrayApp : Form
	{
		[STAThread]
		public static void Main()
		{
			Application.Run(new TrayApp());
		}

		private NotifyIcon trayIcon;
		private ContextMenu trayMenu;

		private Icon mainIcon;
		private Icon downloadingIcon;

		public TrayApp()
		{
			

			mainIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("Himawari8Downloader.Resources.MainIcon.ico"));
			downloadingIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("Himawari8Downloader.Resources.DownloadIcon.ico"));

			trayIcon = new NotifyIcon();
			trayIcon.Text = "Himawari 8 Wallpaper";
			trayIcon.Icon = new Icon(downloadingIcon, 40, 40);

			BuildContextMenu();

			trayIcon.Visible = true;

			Program.DownloadStarted += (e, args) =>
			{
				trayIcon.Icon = new Icon(downloadingIcon, 40, 40);
				trayIcon.Visible = true;
			};

			Program.DownloadCompleted += (e, args) =>
			{
				trayIcon.Icon = new Icon(mainIcon, 40, 40);
				trayIcon.Visible = true;
			};
		}

		private void BuildContextMenu()
		{
			trayMenu = new ContextMenu();

			var startupItem = new MenuItem("Run on startup");
			startupItem.Checked = IsRegisteredInStartup();
			startupItem.Click += (e, args) =>
			{
				RegisterInStartup(!startupItem.Checked);
				BuildContextMenu();
			};

			trayMenu.MenuItems.Add(startupItem);
			trayMenu.MenuItems.Add("Exit", OnExit);

			trayIcon.ContextMenu = trayMenu;
		}

		private void RegisterInStartup(bool isChecked)
		{
			RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

			if (isChecked)
			{
				registryKey.SetValue("Himawari8Downloader", Application.ExecutablePath);
			}
			else if (IsRegisteredInStartup())
			{
				registryKey.DeleteValue("Himawari8Downloader");
			}
		}

		private bool IsRegisteredInStartup()
		{
			RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

			return (registryKey.GetValue("Himawari8Downloader") as string) == Application.ExecutablePath;
		}

		protected override void OnLoad(EventArgs e)
		{
			Visible = false;
			ShowInTaskbar = false;

			Program.Start();

			base.OnLoad(e);
		}

		private void OnExit(object sender, EventArgs e)
		{
			Program.Stop();

			Application.Exit();
		}

		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing)
			{
				trayIcon.Dispose();
			}

			base.Dispose(isDisposing);
		}
	}
}
