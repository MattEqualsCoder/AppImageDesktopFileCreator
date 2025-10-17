// See https://aka.ms/new-console-template for more information

using AppImageDesktopFileCreator;

var file = args.FirstOrDefault() ?? "/home/matt/Downloads/MSURandomizer_3.2.0-rc.1/MSURandomizer.x86_64.AppImage";

// if (string.IsNullOrEmpty(file) || !File.Exists(file))
// {
//     Console.WriteLine("File not found");
//     return;
// }

Environment.SetEnvironmentVariable("APPIMAGE", file);
var exists = DesktopFileCreator.DoesDesktopFileExist("org.mattequalscoder.msurandomizer");

var response = new DesktopFileBuilder("org.mattequalscoder.msurandomizer", "MSU Randomizer")
    .WithDebugAppImage(file)
    .AddUninstallAction()
    .Build();

foreach (var fileToDelete in response.AddedFiles ?? [])
{
    if (File.Exists(fileToDelete))
    {
        File.Delete(fileToDelete);
    }
}

