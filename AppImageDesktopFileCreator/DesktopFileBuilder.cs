using System.Reflection;
using System.Runtime.Versioning;

namespace AppImageDesktopFileCreator;

/// <summary>
/// Build implementation of AppImageDesktopFileCreator to generate a .desktop
/// file for an AppImage file
/// </summary>
/// <param name="appId">A unique reverse DNS identifier for the app</param>
/// <param name="appName">A friendly display name for the app</param>
[SupportedOSPlatform("linux")]
public class DesktopFileBuilder(string appId, string appName)
{
    private string? _windowClass;
    private string? _category;
    private string? _description;
    private readonly List<DesktopIcon> _icons = [];
    private readonly List<CustomAction> _customActions = [];
    private bool _addUninstallAction;
    private readonly List<string> _additionalUninstallPaths = [];
    private CustomMimeTypeInfo? _mimeTypeInfo;

    /// <summary>
    /// Adds a window class to help desktop environments identify the window
    /// </summary>
    public DesktopFileBuilder AddWindowClass(string windowClass)
    {
        _windowClass = windowClass;
        return this;
    }
    
    /// <summary>
    /// Specifies the category for the desktop environment to organize the entry
    /// in the menu
    /// </summary>
    public DesktopFileBuilder AddCategory(string category)
    {
        _category = category;
        return this;
    }
    
    /// <summary>
    /// Specifies the long form description for the entry in the menu
    /// </summary>
    public DesktopFileBuilder AddDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Adds an icon from a stream to use to display the entry in the menu
    /// </summary>
    /// <param name="extension">The file extension of the icon</param>
    /// <param name="stream">The stream to use to copy the icon</param>
    /// <param name="size">
    /// The size of the image in pixels for placing in the correct location.
    /// Using the default DesktopIcon.DynamicSize is for svg files.
    /// </param>
    public DesktopFileBuilder AddIcon(string extension, Stream stream, int size = DesktopIcon.DynamicSize)
    {
        _icons.Add(new DesktopIcon
        {
            Extension = extension,
            Size = size,
            Stream = stream
        });
        return this;
    }

    /// <summary>
    /// Adds an icon from an embedded resource to use to display the entry in the menu
    /// </summary>
    /// <param name="assembly">The calling assembly to use for getting the embedded resource from</param>
    /// <param name="embeddedResourceName">The resource name to load for the icon</param>
    /// <param name="size">
    /// The size of the image in pixels for placing in the correct location.
    /// Using the default DesktopIcon.DynamicSize is for svg files.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when the embedded resource is invalid</exception>
    public DesktopFileBuilder AddIcon(Assembly assembly, string embeddedResourceName,
        int size = DesktopIcon.DynamicSize)
    {
        var stream = assembly.GetManifestResourceStream(embeddedResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Invalid assembly resource {embeddedResourceName}");
        }
        _icons.Add(new DesktopIcon
        {
            Extension = Path.GetExtension(embeddedResourceName),
            Size = size,
            Stream = stream
        });
        return this;
    }

    /// <summary>
    /// Add an action to the desktop for quick list functionality to perform a specific action.
    /// </summary>
    /// <param name="code">Unique code for the quick list entry</param>
    /// <param name="name">The display name of the quick list entry</param>
    /// <param name="arguments">The command line arguments to pass to the application.</param>
    /// <param name="icon">The icon to display</param>
    public DesktopFileBuilder AddCustomArgumentsAction(string code, string name, string arguments, string? icon = null)
    {
        _customActions.Add(new CustomAction
        {
            Code = code,
            Name = name,
            Command = $"\"%EscapedAppPath%\" {arguments}",
            Icon = icon
        });
        return this;
    }
    
    /// <summary>
    /// Add an action to the desktop for quick list functionality to perform a specific action.
    /// </summary>
    /// <param name="code">Unique code for the quick list entry</param>
    /// <param name="name">The display name of the quick list entry</param>
    /// <param name="command">The full path and arguments to use for the action</param>
    /// <param name="icon">The icon to display</param>\
    public DesktopFileBuilder AddCustomCommandAction(string code, string name, string command, string? icon = null)
    {
        _customActions.Add(new CustomAction
        {
            Code = code,
            Name = name,
            Command = command,
            Icon = icon
        });
        return this;
    }

    /// <summary>
    /// Adds an uninstall bash script and action to the jump list to remove the AppImage, Desktop, and Icon files
    /// </summary>
    public DesktopFileBuilder AddUninstallAction()
    {
        _addUninstallAction = true;
        return this;
    }

    /// <summary>
    /// Adds an uninstall bash script and action to the jump list to remove the AppImage, Desktop, and Icon files
    /// </summary>
    /// <param name="additionalUninstallPaths">Additional files or folders to remove when uninstalling</param>
    public DesktopFileBuilder AddUninstallAction(params string[] additionalUninstallPaths)
    {
        _addUninstallAction = true;
        _additionalUninstallPaths.AddRange(additionalUninstallPaths);
        return this;
    }

    public DesktopFileBuilder WithMimeType(string mimeType, string description, string globPattern, bool autoAssociate)
    {
        _mimeTypeInfo = new CustomMimeTypeInfo
        {
            MimeType = mimeType,
            Description = description,
            GlobPattern = globPattern,
            AutoAssociate = autoAssociate
        };
        return this;
    }
    
    /// <summary>
    /// Build the desktop file
    /// </summary>
    /// <returns>The response for if it was successful or not</returns>
    public CreateDesktopFileResponse Build()
    {
        return DesktopFileCreator.CreateDesktopFile(new CreateDesktopFileRequest
        {
            AppId = appId,
            AppName = appName,
            WindowClass = _windowClass ?? appName,
            AppDescription = _description ?? string.Empty,
            DesktopFileCategory = _category ?? DesktopFileCategories.Utility,
            Icons = _icons,
            CustomActions = _customActions,
            AddUninstallAction = _addUninstallAction,
            AdditionalUninstallPaths = _additionalUninstallPaths,
            CustomMimeTypeInfo = _mimeTypeInfo,
        });
    }
}