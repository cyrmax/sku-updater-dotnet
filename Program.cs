using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Net.Http;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.Metadata;
using Octokit;


static class Constants
{
    public const string SKU_URL = "https://api.github.com/repos/Duugu/Sku/releases/latest";
    public const string SKU_UPDATER_URL = "https://api.github.com/repos/cyrmax/sku-updater/releases/latest";
}


class SKUUpdater
{
    private static DateTime titleUpdateTimestamp = DateTime.UtcNow;
    private static GitHubClient github = new GitHubClient(new ProductHeaderValue("SKUUpdater.net"));
    private static HttpClient netClient = new HttpClient();
    static void updateTitle(long downloadedSize, long totalSize)
    {
        if ((DateTime.UtcNow - titleUpdateTimestamp).TotalMilliseconds < 500)
            return;
        Console.Title = $"{downloadedSize / (1024 * 1024):.2f} MB of {totalSize / (1024 * 1024):.2f} MB, {downloadedSize / totalSize * 100:.2f}%. Sku Updater";
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
        var release = await github.Repository.Release.GetLatest("cyrmax", "SKUUpdater.net");
        var latestVersion = new Version(release.TagName);
        if (currentVersion >= latestVersion)
        {
            return false;
        }
        else
        {
            Console.WriteLine($"SKU Updater update available: {latestVersion}. Downloading...");
            string? url = null;
            foreach (var asset in release.Assets)
            {
                if (asset.Name == "sku-updater.exe")
                {
                    url = asset.BrowserDownloadUrl;
                    break;
                }
            }
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
    public static async Task<int> Main(string[] args)
    {
        var forceOption = new Option<bool>("--force", "Force update even if local version is equal or never than latest available. Mainly used for testing purposes.");
        forceOption.AddAlias("-f");
        var diagnosticOption = new Option<bool>("--diagnostic", "Just save diagnostic information to log file and exit without updating.");
        var noUpdateOption = new Option<bool>("--no-self-update", "Skip SKU Updater self update check.");
        var rootCommand = new RootCommand("Sku Updater. A program to update your Sku for WoW Classic installation.");
        rootCommand.AddOption(forceOption);
        rootCommand.AddOption(diagnosticOption);
        rootCommand.AddOption(noUpdateOption);
        rootCommand.SetHandler((force, diagnostic, noUpdate) =>
        {
            // var updater = new Updater();
            // updater.Update(force, diagnostic, noUpdate);
        }, forceOption, diagnosticOption, noUpdateOption);
        return await rootCommand.InvokeAsync(args);
    }
}