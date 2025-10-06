using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Model.Formatting;

namespace AppImageDesktopFileCreator;

[SupportedOSPlatform("linux")]
public static class DesktopFileCreator
{
    public static bool CheckIfDesktopFileExists(string appId)
    {
        var appImageFilePath = Environment.GetEnvironmentVariable("APPIMAGE");
        if (string.IsNullOrEmpty(appImageFilePath) || !File.Exists(appImageFilePath))
        {
            return true;
        }
        
        var desktopFilePath = GetDesktopFileName(GetDesktopFolder(), appId);
        if (!File.Exists(desktopFilePath))
        {
            return false;
        }
        
        var desktopFileContents = File.ReadAllText(desktopFilePath);
        return desktopFileContents.Contains(GetEscapedPathForDesktop(appImageFilePath));
    }
    
    public static CreateDesktopFileResponse CreateDesktopFile(CreateDesktopFileRequest request)
    {
        var appImageFilePath = Environment.GetEnvironmentVariable("APPIMAGE");

        if (string.IsNullOrEmpty(appImageFilePath) || !File.Exists(appImageFilePath))
        {
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = "APPIMAGE missing from environment or the file is not found"
            };
        }

        var desktopFolderPath = GetDesktopFolder();

        try
        {
            if (!Directory.Exists(desktopFolderPath))
            {
                Directory.CreateDirectory(desktopFolderPath);
            }
        }
        catch (Exception e)
        {
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the folder to place the desktop file in: {e.Message}"
            };
        }
        
        var pathData = new PathData
        {
            AppImagePath = appImageFilePath,
            AppImageFolder = Path.GetDirectoryName(appImageFilePath) ?? "",
            DesktopFilePath = GetDesktopFileName(desktopFolderPath, request.AppId),
        };

        try
        {
            CreateIcons(request, pathData);
        }
        catch (Exception e)
        {
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the icon file(s): {e.Message}"
            };
        }

        try
        {
            if (request.AddUninstallAction)
            {
                CreateUninstallFile(request, pathData);
            }
        }
        catch (Exception e)
        {
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the uninstall file: {e.Message}"
            };
        }

        try
        {
            CreateDesktopFile(request, pathData);
        }
        catch (Exception e)
        {
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = $"Failed creating the desktop file: {e.Message}"
            };
        }

        var mimeTypeSuccessful = false;
        string? mimeTypeError = null;
        
        try
        {
            if (request.CustomMimeTypeInfo != null)
            {
                mimeTypeSuccessful = CreateMimeTypeFiles(request, pathData, out mimeTypeError);
            }
        }
        catch (Exception e)
        {
            return new CreateDesktopFileResponse
            {
                Success = true,
                MimeTypeSuccessful = false,
                MimeTypeError = $"Failed creating mime type file: {e.Message}"
            };
        }
        
        return new CreateDesktopFileResponse
        {
            Success = true,
            MimeTypeSuccessful = mimeTypeSuccessful,
            MimeTypeError = mimeTypeError
        };
    }
    
    private static void CreateIcons(CreateDesktopFileRequest request, PathData pathData)
    {
        var iconFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".icons");

        if (!Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }

        string? nonSizedPngIconPath = null;
        List<string> allPaths = [];
        
        foreach (var icon in request.Icons)
        {
            var folderPath = icon.Size > 0 ? CreateIconDirectory(iconFolder, icon.Size) : iconFolder;
            var extension = icon.Extension.StartsWith('.') ? icon.Extension.ToLower() : "." + icon.Extension.ToLower();
            var iconPath = Path.Combine(folderPath, $"{request.AppId}{extension}");
            using var fileStream = new FileStream(iconPath, FileMode.Create);
            for (var i = 0; i < icon.Stream.Length; i++)
            {
                fileStream.WriteByte((byte)icon.Stream.ReadByte());
            }
            fileStream.Close();
            allPaths.Add(iconPath);

            if (icon.Size == 0 || extension != ".png")
            {
                nonSizedPngIconPath = iconPath;
            }
        }

        pathData.IconPaths = allPaths;
        pathData.SelectedIcon = nonSizedPngIconPath ?? request.AppId;
    }

    private static void CreateUninstallFile(CreateDesktopFileRequest request, PathData pathData)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "app-image-uninstalls");

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        pathData.UninstallFilePath = Path.Combine(folder, $"{request.AppId}.sh");
        
        var uninstallFileText = ApplyReplacements(Templates.UninstallFile, request, pathData);

        var paths = (request.AdditionalUninstallPaths ?? []).Concat(pathData.IconPaths ?? []);
        foreach (var path in paths)
        {
            if (Path.HasExtension(path))
            {
                uninstallFileText += Environment.NewLine + $"rm -f {path}";
            }
            else
            {
                uninstallFileText += Environment.NewLine + $"rm -rf {path}";
            }
        }

        request.CustomActions ??= [];
        request.CustomActions.Add(new CustomAction
        {
            Code = "remove",
            Name = $"Uninstall {request.AppName}",
            Command = pathData.UninstallFilePath,
            Icon = "edit-delete-symbolic"
        });

        uninstallFileText = uninstallFileText.Replace("\r\n", "\n");
        
        File.WriteAllText(pathData.UninstallFilePath, uninstallFileText + Environment.NewLine);
        File.SetUnixFileMode(pathData.UninstallFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
    }

    private static void CreateDesktopFile(CreateDesktopFileRequest request, PathData pathData)
    {
        var desktopFileText = ApplyReplacements(Templates.DesktopFile, request, pathData);

        var actionNames = new List<string>();
        var actionText = new StringBuilder();
        
        foreach (var customAction in request.CustomActions ?? [])
        {
            actionNames.Add(customAction.Code);
            actionText.AppendLine();
            actionText.AppendLine($"[Desktop Action {customAction.Code}]");
            actionText.AppendLine($"Name={customAction.Name}");
            actionText.AppendLine($"Exec={ApplyReplacements(customAction.Command, request, pathData)}");

            if (!string.IsNullOrEmpty(customAction.Icon))
            {
                actionText.AppendLine($"Icon={customAction.Icon}");
            }
        }

        if (request.CustomMimeTypeInfo != null)
        {
            desktopFileText += Environment.NewLine + $"MimeType={request.CustomMimeTypeInfo.MimeType}";
        }

        if (actionNames.Count > 0)
        {
            var actionCodeList = string.Join(";", actionNames);
            desktopFileText += Environment.NewLine + $"Actions={actionCodeList}" + Environment.NewLine + actionText;
        }
        
        desktopFileText = desktopFileText.Replace("\r\n", "\n");
        
        File.WriteAllText(pathData.DesktopFilePath, desktopFileText + Environment.NewLine);
    }

    private static bool CreateMimeTypeFiles(CreateDesktopFileRequest request, PathData pathData, out string? error)
    {
        var mimeType = request.CustomMimeTypeInfo?.MimeType;
        var description = request.CustomMimeTypeInfo?.Description;
        var globPattern = request.CustomMimeTypeInfo?.GlobPattern;

        if (string.IsNullOrEmpty(mimeType) || !mimeType.Contains('/') || mimeType.Split("/").Length != 2)
        {
            throw new InvalidOperationException("Invalid mime type");
        }

        if (string.IsNullOrEmpty(globPattern) || !globPattern.StartsWith("*."))
        {
            throw new InvalidOperationException("Invalid glob pattern");
        }
        
        var mimeFolder = GetMimePackagesFolder();
        if (!Directory.Exists(mimeFolder))
        {
            Directory.CreateDirectory(mimeFolder);
        }

        var mimePath = GetMimeFilePath(mimeFolder, mimeType);
        if (File.Exists(mimePath))
        {
            File.Delete(mimePath);
        }

        var mimeDetails = Templates.MimeTypeFile;
        mimeDetails = mimeDetails.Replace("%MimeType%", mimeType);
        mimeDetails = mimeDetails.Replace("%Description%", description);
        mimeDetails = mimeDetails.Replace("%GlobPattern%", globPattern);
        mimeDetails = mimeDetails.Replace("\r\n", "\n");
        File.WriteAllText(mimePath, mimeDetails);

        var mimeListPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mimeapps.list");

        var parser = new FileIniDataParser();
        var data = new IniData();
        if (File.Exists(mimeListPath))
        {
            data = parser.ReadFile(mimeListPath);
        }

        if (!data.Sections.ContainsSection("Default Applications"))
        {
            data.Sections.AddSection("Default Applications");
        }
        data["Default Applications"][mimeType] = Path.GetFileName(pathData.DesktopFilePath);

        if (request.CustomMimeTypeInfo?.AutoAssociate == true)
        {
            if (!data.Sections.ContainsSection("Added Associations"))
            {
                data.Sections.AddSection("Added Associations");
            }
            data["Added Associations"][mimeType] = Path.GetFileName(pathData.DesktopFilePath);
        }

        var formatter = new DefaultIniDataFormatter
        {
            Configuration =
            {
                AssigmentSpacer = ""
            }
        };
        var iniString = data.ToString(formatter) ?? "";
        File.WriteAllText(mimeListPath, iniString);

        if (!UpdateMimeDatabase())
        {
            error = "Error updating mime database";
        }
        else if (!UpdateDesktopDatabase())
        {
            error = "Error updating desktop database";
        }
        else
        {
            error = "";
        }

        return string.IsNullOrEmpty(error);
    }

    private static bool UpdateMimeDatabase()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "update-mime-database",
            Arguments = GetMimeFolder(),
            RedirectStandardOutput = false,
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        try
        {
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"Console App exited with code: {process.ExitCode}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting or running console app: {ex.Message}");
            return false;
        }
    }
    
    private static bool UpdateDesktopDatabase()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "update-desktop-database",
            RedirectStandardOutput = false,
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        try
        {
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"Console App exited with code: {process.ExitCode}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting or running console app: {ex.Message}");
            return false;
        }
    }

    private static string ApplyReplacements(string toUpdate, CreateDesktopFileRequest request, PathData pathData)
    {
        toUpdate = toUpdate.Replace("%AppName%", request.AppName);
        toUpdate = toUpdate.Replace("%AppClass%", request.WindowClass);
        toUpdate = toUpdate.Replace("%AppName%", request.AppName);
        toUpdate = toUpdate.Replace("%AppDescription%", request.AppDescription);
        toUpdate = toUpdate.Replace("%AppPath%", pathData.AppImagePath);
        toUpdate = toUpdate.Replace("%EscapedAppPath%", GetEscapedPathForDesktop(pathData.AppImagePath));
        toUpdate = toUpdate.Replace("%Category%", request.DesktopFileCategory);
        toUpdate = toUpdate.Replace("%IconPath%", pathData.SelectedIcon);
        toUpdate = toUpdate.Replace("%FolderPath%", pathData.AppImageFolder);
        toUpdate = toUpdate.Replace("%DesktopFilePath%", pathData.DesktopFilePath);
        toUpdate = toUpdate.Replace("%UninstallFilePath%", pathData.UninstallFilePath);
        return toUpdate;
    }

    private static string CreateIconDirectory(string iconFolder, int size)
    {
        iconFolder = Path.Combine(iconFolder, "hicolor");
        if (!Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }
        
        iconFolder = Path.Combine(iconFolder, $"{size}x{size}");
        if (!Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }
        
        iconFolder = Path.Combine(iconFolder, "apps");
        if (!Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }

        return iconFolder;
    }

    private static string GetDesktopFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
    }

    private static string GetDesktopFileName(string desktopFolder, string appId)
    {
        return Path.Combine(desktopFolder, $"{appId}.desktop");
    }

    private static string GetEscapedPathForDesktop(string path)
    {
        return path.Replace(" ", "\\s");
    }

    private static string GetMimeFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mime");
    }
    
    private static string GetMimePackagesFolder()
    {
        return Path.Combine(GetMimeFolder(), "packages");
    }

    private static string GetMimeFilePath(string mimeFolder, string mimeType)
    {
        return Path.Combine(mimeFolder, mimeType.Split("/")[1] + ".xml");
    }
}

internal class PathData
{
    public required string AppImagePath { get; init; }
    public required string AppImageFolder { get; init; }
    public required string DesktopFilePath { get; init; }
    public string? SelectedIcon { get; set; }
    public List<string>? IconPaths { get; set; }
    public string? UninstallFilePath { get; set; }
}