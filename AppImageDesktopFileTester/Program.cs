// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using AppImageDesktopFileCreator;

var file = args.FirstOrDefault() ?? "/home/matt/Downloads/SMZ3CasRandomizer_9.9.10-beta.1/SMZ3CasRandomizer.x86_64.AppImage";

if (string.IsNullOrEmpty(file) || !File.Exists(file))
{
    Console.WriteLine("File not found");
    return;
}

var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
if (Directory.Exists(tempPath))
{
    Directory.Delete(tempPath, true);
}
Directory.CreateDirectory(tempPath);
Environment.CurrentDirectory = tempPath;
;
Console.WriteLine("Using temp path:  " + tempPath);

var tempAppImagePath = Path.Combine(tempPath, "test.AppImage");
File.Copy(file, tempAppImagePath);

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = tempAppImagePath,
        Arguments = "--appimage-extract",
        UseShellExecute = true,
        CreateNoWindow = true,
        WorkingDirectory = tempPath,
    }
};

process.Start();
process.WaitForExit();

var extractedPath = Path.Combine(tempPath, "squashfs-root");

Environment.SetEnvironmentVariable("APPIMAGE", tempAppImagePath);
Environment.SetEnvironmentVariable("APPDIR", extractedPath);

var response = new DesktopFileBuilder("org.test.matt", "Matt Test App")
    .AddUninstallAction()
    .Build();

foreach (var fileToDelete in response.AddedFiles ?? [])
{
    if (File.Exists(fileToDelete))
    {
        File.Delete(fileToDelete);
    }
}
Directory.Delete(tempPath, true);

