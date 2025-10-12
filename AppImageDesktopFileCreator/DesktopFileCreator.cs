using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using IniParser;
using IniParser.Model;
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
        
        var mountPath = Environment.GetEnvironmentVariable("APPDIR");
        if (string.IsNullOrEmpty(mountPath) || !Directory.Exists(mountPath))
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
        
        var mountPath = Environment.GetEnvironmentVariable("APPDIR");
        if (string.IsNullOrEmpty(mountPath) || !Directory.Exists(mountPath))
        {
            return new CreateDesktopFileResponse
            {
                Success = false,
                ErrorMessage = "APPDIR missing from environment or the directory is not found"
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
            SanitizedAppImagePath = GetEscapedPathForDesktop(appImageFilePath),
            MountFolder = mountPath,
            DesktopFilePath = Path.Combine(desktopFolderPath, $"{request.AppId}.desktop")
        };

        List<string> addedFiles = [pathData.DesktopFilePath];

        try
        {
            var icons = CreateIcons(pathData);
            addedFiles.AddRange(icons);
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
                addedFiles.Add(CreateUninstallFile(request, pathData));
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
                mimeTypeSuccessful = CreateMimeTypeFiles(request, pathData, addedFiles, out mimeTypeError);
            }
        }
        catch (Exception e)
        {
            return new CreateDesktopFileResponse
            {
                Success = true,
                MimeTypeSuccessful = false,
                MimeTypeError = $"Failed creating mime type file: {e.Message}",
                AddedFiles = addedFiles,
            };
        }
        
        return new CreateDesktopFileResponse
        {
            Success = true,
            MimeTypeSuccessful = mimeTypeSuccessful,
            MimeTypeError = mimeTypeError,
            AddedFiles = addedFiles
        };
    }
    
    private static List<string> CreateIcons(PathData pathData)
    {
        var iconFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".icons");

        if (!Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }

        var hiColorFolder = Path.Combine(iconFolder, "hicolor");
        if (!Directory.Exists(hiColorFolder))
        {
            Directory.CreateDirectory(hiColorFolder);
        }

        var copyFromFolder = Path.Combine(pathData.MountFolder, "usr", "share", "icons", "hicolor");

        List<string> iconPaths = [];
        CopyFilesRecursively(new DirectoryInfo(copyFromFolder), new DirectoryInfo(hiColorFolder), iconPaths);
        pathData.IconPaths = iconPaths;
        return iconPaths;
    }

    private static string CreateUninstallFile(CreateDesktopFileRequest request, PathData pathData)
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

        return pathData.UninstallFilePath;
    }

    private static void CreateDesktopFile(CreateDesktopFileRequest request, PathData pathData)
    {
        var desktopFile = Directory.EnumerateFiles(pathData.MountFolder, "*.desktop", SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (string.IsNullOrEmpty(desktopFile))
        {
            throw new FileNotFoundException("Unable to find desktop file");
        }
        
        var desktopText = File.ReadAllLines(desktopFile);
        var execLine = desktopText.FirstOrDefault(x => x.StartsWith("Exec="));
        
        if (string.IsNullOrEmpty(execLine))
        {
            throw new InvalidOperationException("Unable to find exec line in desktop file");
        }

        execLine = execLine.Split("=", 2)[1];

        var mimeInsertIndex = 0;
        var insertedMimeType = false;
        var insertedActionsType = false;
        
        var stringBuilder = new StringBuilder();
        foreach (var line in desktopText)
        {
            if (line.StartsWith("Actions="))
            {
                var actions = line.Split("=", 2)[1].Split(";");
                if (request.CustomActions?.Count > 0)
                {
                    actions = actions.Concat(request.CustomActions.Select(x => x.Name)).ToArray();
                }
                var actionCodeList = string.Join(";", actions);
                stringBuilder.AppendLine($"Actions={actionCodeList}");
                insertedActionsType = true;
            }
            else if (line.StartsWith("Mime=") && request.CustomMimeTypeInfo != null)
            {
                stringBuilder.AppendLine($"MimeType={request.CustomMimeTypeInfo.MimeType}");
                insertedMimeType = true;
            }
            else
            {
                if (line.StartsWith("Categories"))
                {
                    mimeInsertIndex = stringBuilder.Length;
                }
                stringBuilder.AppendLine(line.Replace(execLine, pathData.SanitizedAppImagePath));
            }
        }

        if (!insertedActionsType && request.CustomActions?.Count > 0)
        {
            var actions = request.CustomActions.Select(x => x.Code);
            var actionCodeList = string.Join(";", actions);
            stringBuilder.AppendLine($"Actions={actionCodeList}");
        }
        
        if (!insertedMimeType && request.CustomMimeTypeInfo != null)
        {
            stringBuilder.Insert(mimeInsertIndex, $"MimeType={request.CustomMimeTypeInfo.MimeType}{Environment.NewLine}");
        }

        foreach (var customAction in request.CustomActions ?? [])
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"[Desktop Action {customAction.Code}]");
            stringBuilder.AppendLine($"Name={customAction.Name}");
            stringBuilder.AppendLine($"Exec={ApplyReplacements(customAction.Command, request, pathData)}");
        }
        
        stringBuilder.AppendLine();
        File.WriteAllText(pathData.DesktopFilePath, stringBuilder.ToString());
    }

    private static bool CreateMimeTypeFiles(CreateDesktopFileRequest request, PathData pathData, List<string> addedFiles, out string? error)
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
        addedFiles.Add(mimePath);

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
        toUpdate = toUpdate.Replace("%AppPath%", pathData.AppImagePath);
        toUpdate = toUpdate.Replace("%EscapedAppPath%", GetEscapedPathForDesktop(pathData.AppImagePath));
        toUpdate = toUpdate.Replace("%FolderPath%", pathData.AppImageFolder);
        toUpdate = toUpdate.Replace("%DesktopFilePath%", pathData.DesktopFilePath);
        toUpdate = toUpdate.Replace("%UninstallFilePath%", pathData.UninstallFilePath);
        return toUpdate;
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
    
    private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target, List<string> outputPaths) 
    {
        foreach (var dir in source.GetDirectories())
        {
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name), outputPaths);
        }
            
        foreach (var file in source.GetFiles())
        {
            var destination = Path.Combine(target.FullName, file.Name);
            file.CopyTo(destination, overwrite: true);
            outputPaths.Add(destination);
        }
    }
}

internal class PathData
{
    public required string AppImagePath { get; init; }
    public required string AppImageFolder { get; init; }
    public required string SanitizedAppImagePath { get; init; }
    public required string MountFolder { get; init; }
    public required string DesktopFilePath { get; init; }
    public List<string>? IconPaths { get; set; }
    public string? UninstallFilePath { get; set; }
}