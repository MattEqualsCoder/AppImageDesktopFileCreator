namespace AppImageDesktopFileCreator;

public class CreateDesktopFileRequest
{
    public required string AppId { get; init; }
    public required string AppName { get; init; }
    public required string AppDescription { get; init; }
    public required string WindowClass { get; init; }
    public required string DesktopFileCategory { get; init; }
    public required List<DesktopIcon> Icons { get; init; }
    public List<CustomAction>? CustomActions { get; set; }
    public bool AddUninstallAction { get; set; }
    public List<string>? AdditionalUninstallPaths { get; set; }
    public CustomMimeTypeInfo? CustomMimeTypeInfo { get; set; }
}

public class CustomAction
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Command { get; init; }
    public string? Icon { get; init; }
}

public class DesktopIcon
{
    public const int DynamicSize = 0;
    public required int Size { get; init; }
    public required Stream Stream { get; init; }
    public required string Extension { get; init; }
}

public class CustomMimeTypeInfo
{
    public required string MimeType { get; init; }
    public required string Description { get; init; }
    public required string GlobPattern { get; init; }
    public bool AutoAssociate { get; init; }
}