using System.IO;
using System.Text.Json;

namespace CodexQuota;

public sealed class AppSettings
{
    public bool FirstRun { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ClickThrough { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public double Opacity { get; set; } = 0.92;
    public double Brightness { get; set; } = 1.0;
    public double Scale { get; set; } = 1.0;
    public double OverlayWidth { get; set; } = 430;
    public double OverlayHeight { get; set; } = 205;
    public string Foreground { get; set; } = "White";
    public string Background { get; set; } = "#CC202124";
    public double Left { get; set; } = 40;
    public double Top { get; set; } = 40;
    public int MonitorIndex { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexQuota", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
