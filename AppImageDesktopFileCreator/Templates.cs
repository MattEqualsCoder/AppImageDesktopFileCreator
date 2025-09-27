namespace AppImageDesktopFileCreator;

internal static class Templates
{
    internal const string DesktopFile = 
        """
        [Desktop Entry]
        Type=Application
        Name=%AppName%
        StartupWMClass=%AppClass%
        Comment=%AppDescription%
        Exec="%EscapedAppPath%"
        NoDisplay=false
        Terminal=false
        Categories=%Category%;
        MimeType=
        Keywords=
        Icon=%IconPath%
        Path=%FolderPath%
        """;

    internal const string UninstallFile = 
        """
        #!/bin/bash

        rm -f "%DesktopFilePath%"
        rm -f "%AppPath%"
        rm -f "%UninstallFilePath%"
        """;
}