# App Image Desktop File Creator

Nuget Package for creating .desktop files for Linux .AppImage files. Can be used with [PupNet-Deploy](https://github.com/kuiperzone/PupNet-Deploy) for single file .net applications that will automatically add themselves to the menu.

## Basic Usage

```csharp
if (!DesktopFileCreator.CheckIfDesktopFileExists("org.mattequalscoder.example"))
{
    var assembly = Assembly.GetExecutingAssembly();
    return new DesktopFileBuilder("org.mattequalscoder.example", "Example App")
        .AddDescription("Example of .desktop files for AppImages")
        .AddCategory(DesktopFileCategories.Development)
        .AddWindowClass("Example")
        .AddIcon(assembly, "Example.Assets.icon.16.png", 16)
        .AddIcon(assembly, "Example.Assets.icon.32.png", 32)
        .AddIcon(assembly, "Example.Assets.icon.48.png", 48)
        .AddIcon(assembly, "Example.Assets.icon.256.png", 256)
        .AddIcon(assembly, "Example.Assets.icon.svg")
        .AddUninstallAction(Directories.BaseFolder)
        .Build();
}
```

## Directories

The following directories are used:

* `~/.local/share/applications` - This is where the .desktop file is created
* `~/.icons` - This is where svg icon files are placed
* `~/.icons/hicolor/<imagesize>/apps` - This is where sized png files are placed
* `~/.local/share/app-image-uninstalls` - If the uninstall action is requested, this is where the bash file is created