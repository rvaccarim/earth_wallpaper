using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Serilog;
using Microsoft.Extensions.Configuration;
using Serilog.Formatting.Json;

namespace earth_wallpaper
{
    internal partial class Program
    {
        private static readonly string currDir = System.IO.Directory.GetCurrentDirectory();
        private static readonly string downloadedFilePath = currDir + @"\earth_original.jpg";
        private static readonly string wallpaperFilePath = currDir + @"\earth_wallpaper.jpg";
        private static readonly string configFilePath = currDir + @"\earth_wallpaper.config.json";
        private static readonly string logFilePath = currDir + @"\earth_wallpaper.log";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new JsonFormatter())
                .WriteTo.File(new JsonFormatter(), logFilePath)
                .CreateLogger();

            try
            {
                var wallpaperConfig = ReadConfig();

                if (wallpaperConfig != null)
                {
                    if (await DownloadFile("https://cdn.star.nesdis.noaa.gov/GOES16/ABI/FD/GEOCOLOR/1808x1808.jpg", downloadedFilePath))
                    {
                        if (ResizeImage(downloadedFilePath,
                            wallpaperFilePath,
                            wallpaperConfig.ScaleToWidth,
                            wallpaperConfig.ScaleToHeight,
                            wallpaperConfig.WallpaperWidth,
                            wallpaperConfig.WallpaperHeight))
                        {
                            SetWallpaper(wallpaperFilePath);
                        }
                    }
                }
            }
            finally
            {
                Log.Information("===========================================================================================================");
                Log.CloseAndFlush();
            }
        }


        private static WallpaperConfig? ReadConfig()
        {
            try
            {
                Log.Information($"Reading configuration file");
                
                var config = new ConfigurationBuilder()
                                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                .AddJsonFile(configFilePath).Build();

                var section = config.GetSection("WallpaperConfig");
                return section.Get<WallpaperConfig>();

            }
            catch (Exception ex)
            {
                Log.Error($"Error reading configuration file: {ex.Message}");
                return null;
            }
        }


        private static async Task<bool> DownloadFile(string url, string outputPath)
        {
            try
            {
                Log.Information($"Downloading Image: {url}");

                HttpClient _httpClient = new();
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(url);
                File.WriteAllBytes(outputPath, fileBytes);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error downloading image: {ex.Message}");
                return false;
            }

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        private static bool ResizeImage(string inputImagePath, string outputImagePath, int resizedWidth, int resizedHeight, int canvasWidth, int canvasHeight)
        {
            try
            {
                Log.Information($"Resizing Image: {inputImagePath}");

                using Bitmap originalImage = new(inputImagePath);

                // Resize the original image
                Bitmap resizedImage = new(resizedWidth, resizedHeight);

                using (Graphics g = Graphics.FromImage(resizedImage))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(originalImage, 0, 0, resizedWidth, resizedHeight);
                }

                // Create a new black image
                Bitmap canvas = new(canvasWidth, canvasHeight, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.FillRectangle(Brushes.Black, 0, 0, canvasWidth, canvasHeight);

                    // Get coordinates to paste the resized image in the center
                    int x = (canvasWidth - resizedWidth) / 2;
                    int y = (canvasHeight - resizedHeight) / 2;

                    // Paste the resized image
                    g.DrawImage(resizedImage, x, y, resizedWidth, resizedHeight);
                }

                canvas.Save(outputImagePath, ImageFormat.Jpeg);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error resizing image: {ex.Message}");
                return false;
            }
        }


        private static void SetWallpaper(string imagePath)
        {
            try
            {
                Log.Information($"Setting wallpaper to: {imagePath}");

                if (!File.Exists(imagePath))
                {
                    Log.Error("Error: Image path not found");
                    return;
                }

                // SystemParametersInfo fails if not given a full path
                bool res = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                if (!res)
                {
                    Log.Error($"Error setting wallpaper: {res}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error setting wallpaper: {ex.Message}");
            }

        }
    }
}


