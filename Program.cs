namespace SKUUpdater;


using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

static class Constants
{
    public const string SKU_URL = "https://api.github.com/repos/Duugu/Sku/releases/latest";
    public const string SKU_UPDATER_URL = "https://api.github.com/repos/cyrmax/sku-updater/releases/latest";
}


record SkuInfo(Version Version, string Link);

class SKUUpdater
{
    private static DateTime titleUpdateTimestamp = DateTime.UtcNow;
    private static HttpClient netClient = new HttpClient();
    private static JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, TypeInfoResolver = SourceGenerationContext.Default };
    static void updateTitle(float downloadedSize, float totalSize)
    {
        if ((DateTime.UtcNow - titleUpdateTimestamp).TotalMilliseconds < 500)
            return;
        Console.Title = $"{downloadedSize / (1024 * 1024):F2} MB of {totalSize / (1024 * 1024):F2} MB, {downloadedSize / totalSize * 100:F2}%. Sku Updater";
        titleUpdateTimestamp = DateTime.UtcNow;
    }

    static void confirmedExit(int code)
    {
        Console.WriteLine("Press enter to exit the program.");
        Console.ReadLine();
        Environment.Exit(code);
    }

    static async Task<bool> selfUpdate()
    {
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        netClient.DefaultRequestHeaders.Add("User-Agent", "SKUUpdater");
        var releaseResponse = await netClient.GetAsync(Constants.SKU_UPDATER_URL);
        if (!releaseResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("Unable to download latest Sku Updater version.");
            Console.WriteLine($"Error http response {releaseResponse.ReasonPhrase}");
            return false;
        }
        var jsonString = await releaseResponse.Content.ReadAsStringAsync();
        if (jsonString is null)
        {
            Console.WriteLine("Unable to download latest Sku Updater version.");
            return false;
        }
        var release = JsonSerializer.Deserialize(jsonString, SourceGenerationContext.Default.ReleaseInfo);
        if (release is null)
        {
            Console.WriteLine("Unable to download latest Sku Updater version.");
            Console.WriteLine("release is null");
            return false;
        }
        var latestVersion = new Version(release.TagName);
        if (currentVersion >= latestVersion)
        {
            return false;
        }
        else
        {
            Console.WriteLine($"SKU Updater update available: {latestVersion}. Downloading...");
            string? url = release.Assets.Find(a => a.Name == "sku-updater.zip")?.BrowserDownloadUrl;
            if (url is null)
            {
                Console.WriteLine("Unable to download latest Sku Updater version.");
                return false;
            }
            var localFilename = url.Split("/").Last() + ".tmp";
            var response = await netClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Unable to download latest Sku Updater version.");
                return false;
            }
            var totalSize = response.Content.Headers.ContentLength ?? -1;
            var downloadedSize = 0L;
            using var file = File.Create(localFilename);
            using var stream = await response.Content.ReadAsStreamAsync();
            byte[] buffer = new byte[16384];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await file.WriteAsync(buffer, 0, bytesRead);
                downloadedSize += bytesRead;
                updateTitle(downloadedSize, totalSize);
            }
            using var restartFile = new StreamWriter("sku-updater-restart.bat");
            await restartFile.WriteLineAsync(@"@echo off
            ping -n 5 localhost > nul
            del sku-updater.exe
            copy sku-updater.exe.tmp sku-updater.exe
            start sku-updater.exe
            ");
            return true;
        }
    }

    static string? findWowc()
    {
        var value = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft")?.GetValue("InstallPath")?.ToString();
        if (value is null) return null;
        return Path.Combine(Directory.GetParent(value)!.FullName, "_classic_");
    }

    static Version? getSkuVersion(string skuPath)
    {
        string changeLogPath = Path.Combine(skuPath, "changelog.md");
        Regex regex = new Regex(@"^\# Sku \(((\d+\.\d+)|(\d+))\)");
        using StreamReader changeLogFile = new StreamReader(changeLogPath);
        string? line;
        while ((line = changeLogFile.ReadLine()) != null)
        {
            Match match = regex.Match(line);
            if (match.Success)
            {
                return new Version(match.Groups[1].Value);
            }
        }
        return null;
    }

    static async Task<SkuInfo?> fetchSkuVersion()
    {
        Regex regex = new Regex(@"^r([\d\.]+)$");
        var response = await netClient.GetAsync(Constants.SKU_URL);
        if (!response.IsSuccessStatusCode) return null;
        var jsonString = await response.Content.ReadAsStringAsync();
        if (jsonString is null) return null;
        var releaseInfo = JsonSerializer.Deserialize(jsonString, SourceGenerationContext.Default.ReleaseInfo);
        if (releaseInfo is null) return null;
        Match versionMatch = regex.Match(releaseInfo.TagName);
        if (!versionMatch.Success) return null;
        Version version = new Version(versionMatch.Groups[1].Value);
        string? link = releaseInfo.Assets.Find(a => a.Name.EndsWith(".zip"))?.BrowserDownloadUrl;
        if (link is null) return null;
        return new SkuInfo(version, link);
    }

    static async Task<bool> updateSku(SkuInfo skuInfo, string skupath)
    {
        string url = skuInfo.Link;
        string localFileName = url.Split("/").Last();
        var response = await netClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode) return false;
        using (var localFile = File.Create(localFileName))
        using (var stream = await response.Content.ReadAsStreamAsync())
        {
            long totalSize = response.Content.Headers.ContentLength ?? -1;
            long downloadedSize = 0;
            int bytesRead = 0;
            byte[] buffer = new byte[16384];
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await localFile.WriteAsync(buffer, 0, bytesRead);
                downloadedSize += bytesRead;
                updateTitle(downloadedSize, totalSize);
            }
        }
        Console.WriteLine("Installing...");
        Console.Title = "Installing Sku. Sku Updater";
        ZipFile.ExtractToDirectory(localFileName, Directory.GetParent(skupath)!.FullName);
        Console.WriteLine("Cleaning...");
        Console.Title = "Cleaning. Sku Updater";
        File.Delete(localFileName);
        return true;
    }

    public static async Task<int> Main(string[] args)
    {
        System.AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((object sender, UnhandledExceptionEventArgs e) =>
        {
            Console.WriteLine(" An error occured");
            Console.WriteLine(e);
        });
        var forceOption = new Option<bool>("--force", "Force update even if local version is equal or never than latest available. Mainly used for testing purposes.");
        forceOption.AddAlias("-f");
        var diagnosticOption = new Option<bool>("--diagnostic", "Just save diagnostic information to log file and exit without updating.");
        var noUpdateOption = new Option<bool>("--no-self-update", "Skip SKU Updater self update check.");
        var rootCommand = new RootCommand("Sku Updater. A program to update your Sku for WoW Classic installation.");
        rootCommand.AddOption(forceOption);
        rootCommand.AddOption(diagnosticOption);
        rootCommand.AddOption(noUpdateOption);
        rootCommand.SetHandler(async (force, diagnostic, noUpdate) =>
        {
            await _main(force, diagnostic, noUpdate);
        }, forceOption, diagnosticOption, noUpdateOption);
        return await rootCommand.InvokeAsync(args);
    }

    static async Task _main(bool force, bool diagnostic, bool noUpdate)
    {
        File.Delete("sku-updater-restart.bat");
        File.Delete("sku-updater.exe.tmp");
        if (!noUpdate)
        {
            if (await selfUpdate())
            {
                Process.Start("sku-updater-restart.bat");
                Environment.Exit(0);
            }
        }
        Console.WriteLine("Searching for World of Warcraft Classic installation");
        var wowcPath = findWowc();
        if (wowcPath is null)
        {
            Console.WriteLine("Unable to find World of Warcraft Classic installation");
            confirmedExit(1);
            return;
        }
        Console.WriteLine($"Found WoW Classic at path: {wowcPath}");
        string skuPath = Path.Combine(wowcPath, "Interface", "AddOns", "Sku");
        if (!Path.Exists(skuPath))
        {
            Console.WriteLine("Unable to find Sku. Check your installation.");
            confirmedExit(1);
        }
        Console.WriteLine($"Found Sku at path: {skuPath}");
        var skuVersion = getSkuVersion(skuPath);
        if (skuVersion is null)
        {
            Console.WriteLine("Unable to find Sku version. Check your installation.");
            confirmedExit(1);
        }
        Console.WriteLine($"Current Sku version is {skuVersion}");
        Console.WriteLine("Checking for updates");
        var skuInfo = await fetchSkuVersion();
        if (skuInfo is null)
        {
            Console.WriteLine("Unable to fetch latest Sku version. Check your internet connection.");
            confirmedExit(1);
            return;
        }
        Console.WriteLine($"Latest Sku version is {skuInfo.Version}");
        if (skuInfo.Version <= skuVersion || force)
        {
            Console.WriteLine("Your Sku is up to date");
            confirmedExit(0);
            return;
        }
        Console.WriteLine("Do you want to update to the latest version? y or yes to update, n, no or other letters to exit.");
        string answer = Console.ReadLine()!;
        if (answer != "y" && answer != "yes")
        {
            confirmedExit(0);
            return;
        }
        Console.WriteLine("Updating Sku");
        if (!(await updateSku(skuInfo, skuPath)))
        {
            Console.WriteLine("Unable to update Sku");
            confirmedExit(1);
        }
        Console.WriteLine("Verifying...");
        var newVersion = getSkuVersion(skuPath);
        if (newVersion <= skuVersion)
        {
            Console.WriteLine("Sku not updated. Either you have updated with force flag or update was unsuccessful.");
            confirmedExit(1);
            return;
        }
        Console.WriteLine($"Sku version is now {newVersion}");
        confirmedExit(0);
    }
}
