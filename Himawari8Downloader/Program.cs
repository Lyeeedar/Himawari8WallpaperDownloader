using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Himawari8Downloader
{
	class Program
	{
		public sealed class Wallpaper
		{
			Wallpaper() { }

			const int SPI_SETDESKWALLPAPER = 20;
			const int SPIF_UPDATEINIFILE = 0x01;
			const int SPIF_SENDWININICHANGE = 0x02;

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

			public enum Style : int
			{
				Tiled,
				Centered,
				Stretched
			}

			public static void Set(Uri uri, Style style)
			{
				System.IO.Stream s = new System.Net.WebClient().OpenRead(uri.ToString());

				System.Drawing.Image img = System.Drawing.Image.FromStream(s);
				string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.bmp");
				img.Save(tempPath, System.Drawing.Imaging.ImageFormat.Bmp);

				RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
				if (style == Style.Stretched)
				{
					key.SetValue(@"WallpaperStyle", 2.ToString());
					key.SetValue(@"TileWallpaper", 0.ToString());
				}

				if (style == Style.Centered)
				{
					key.SetValue(@"WallpaperStyle", 1.ToString());
					key.SetValue(@"TileWallpaper", 0.ToString());
				}

				if (style == Style.Tiled)
				{
					key.SetValue(@"WallpaperStyle", 1.ToString());
					key.SetValue(@"TileWallpaper", 1.ToString());
				}

				SystemParametersInfo(SPI_SETDESKWALLPAPER,
					0,
					tempPath,
					SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
			}
		}

		public class ImageInfo
		{
			public DateTime Date { get; set; }
			public string File { get; set; }
		}

		public class ImageSettings
		{
			public int Width { get; set; }
			public string Level { get; set; }
			public int NumBlocks { get; set; }
			public string TimeString { get; set; }
		}

		private static ImageSettings GetLatestImageInfo()
		{
			ImageSettings settings = null;
			try
			{
				System.Diagnostics.Debug.WriteLine("Downloading json");

				using (var wc = new WebClient())
				{
					var json = wc.DownloadString("http://himawari8-dl.nict.go.jp/himawari8/img/D531106/latest.json?" + Guid.NewGuid());
					var info = JsonConvert.DeserializeObject<ImageInfo>(json);
					settings = new ImageSettings
					{
						Width = 550,
						Level = "4d",
						NumBlocks = 4,
						TimeString = info.Date.ToString("yyyy/MM/dd/HHmmss", CultureInfo.InvariantCulture)
					};
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("Error: " + ex.Message);
			}

			return settings;
		}

		private static Bitmap AssembleImageFrom(ImageSettings imageInfo)
		{
			var url = $"http://himawari8-dl.nict.go.jp/himawari8/img/D531106/{imageInfo.Level}/{imageInfo.Width}/{imageInfo.TimeString}";

			var finalImage = new Bitmap(imageInfo.Width * imageInfo.NumBlocks, imageInfo.Width * imageInfo.NumBlocks + 100);

			var canvas = Graphics.FromImage(finalImage);
			canvas.Clear(Color.Black);

			try
			{
				Parallel.For(0, imageInfo.NumBlocks, (x) => 
				{
					Parallel.For(0, imageInfo.NumBlocks, (y) =>
					{
						System.Diagnostics.Debug.WriteLine("Downloading tile " + x + "," + y);

						var cUrl = $"{url}_{x}_{y}.png";

						System.Diagnostics.Debug.WriteLine("Url: " + cUrl);

						var request = WebRequest.Create(cUrl);
						var response = (HttpWebResponse)request.GetResponse();
						if (response.StatusCode == HttpStatusCode.OK)
						{
							using (var imagePart = Image.FromStream(response.GetResponseStream()))
							{
								lock (canvas)
								{
									canvas.DrawImage(imagePart, x * imageInfo.Width, y * imageInfo.Width, imageInfo.Width, imageInfo.Width);
								}
							}

							System.Diagnostics.Debug.WriteLine("Completed tile " + x + "," + y);
						}

						response.Close();
					});
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("Error: " + ex.Message);
			}

			return finalImage;
		}

		private static string SaveImage(Bitmap finalImage)
		{
			var eParams = new EncoderParameters(1)
			{
				Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L) }
			};

			var jpegCodecInfo = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");
			var pathName = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + @"\Earth\latest.jpg";

			try
			{
				if (!Directory.Exists(Path.GetDirectoryName(pathName)))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(pathName));
				}

				if (jpegCodecInfo != null) finalImage.Save(pathName, jpegCodecInfo, eParams);
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				finalImage.Dispose();
			}

			return pathName;
		}

		public static void DownloadImage()
		{
			DownloadStarted?.Invoke(null, null);

			try
			{
				var start = DateTime.Now;

				var imageInfo = GetLatestImageInfo();
				if (imageInfo != null)
				{
					var image = AssembleImageFrom(imageInfo);
					var imageFile = SaveImage(image);

					Wallpaper.Set(new Uri(imageFile), Wallpaper.Style.Centered);
				}

				var end = DateTime.Now;

				var duration = (end - start).TotalMilliseconds;

				System.Diagnostics.Debug.WriteLine("Completed in " + duration + "ms");
			}
			catch (Exception)
			{

			}

			DownloadCompleted?.Invoke(null, null);
		}

		private static System.Timers.Timer timer;
		private static string lastDownloaded;

		public static event EventHandler DownloadStarted;
		public static event EventHandler DownloadCompleted;

		public static void Start()
		{
			DownloadImage();
			lastDownloaded = GetLatestImageInfo().TimeString;

			timer = new System.Timers.Timer();
			timer.Interval = 1000 * 60 * 10; // 10 mins
			timer.AutoReset = true;
			timer.Elapsed += (e, args) =>
			{
				var info = GetLatestImageInfo();
				if (info.TimeString != lastDownloaded)
				{
					DownloadImage();
					lastDownloaded = info.TimeString;
				}
			};

			timer.Start();
		}

		public static void Stop()
		{
			timer?.Stop();
		}
	}
}
