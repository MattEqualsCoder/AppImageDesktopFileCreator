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
        #!/usr/bin/env bash

        rm -f "%DesktopFilePath%"
        rm -f "%AppPath%"
        rm -f "%UninstallFilePath%"
        """;

    internal const string MimeTypeFile =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <mime-info xmlns='http://www.freedesktop.org/standards/shared-mime-info'>
          <mime-type type="%MimeType%">
            <comment>%Description%</comment>
            <glob pattern="%GlobPattern%"/>
          </mime-type>
        </mime-info>
        """;
}