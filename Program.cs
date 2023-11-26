using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Net.Http;
using System.IO.Compression;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.CommandLine;
using System.CommandLine.Invocation;

record Version(int Major, int Minor, int Subminor, int Patch) : IComparable<Version>
{
    public static Version? Parse(string input)
    {
        string[] parts = input.Split(".", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 4)
            return null;
        int[] versionParts = new int[4];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out versionParts[i]))
                return null;
        }
        return new Version(versionParts[0], versionParts[1], versionParts[2], versionParts[3]);
    }
    public int CompareTo(Version? other)
    {
        if (other is null) return 1;
        int majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;
        int minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;
        int subminorComparison = Subminor.CompareTo(other.Subminor);
        if (subminorComparison != 0) return subminorComparison;
        return Patch.CompareTo(other.Patch);
    }
    public override string ToString()
    {
        return $"{this.Major}.{this.Minor}.{this.Subminor}.{this.Patch}";
    }
}


static class Constants
{
    public const string SKU_URL = "https://api.github.com/repos/Duugu/Sku/releases/latest";
    public const string SKU_UPDATER_URL = "https://api.github.com/repos/cyrmax/sku-updater/releases/latest";
}


class SKUUpdater
{
    private static DateTime titleUpdateTimestamp = DateTime.UtcNow;
    static void updateTitle(int downloadedSize, int totalSize)
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