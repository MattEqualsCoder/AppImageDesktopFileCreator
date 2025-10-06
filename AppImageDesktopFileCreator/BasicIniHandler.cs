namespace AppImageDesktopFileCreator;

public class BasicIniHandler
{
    private Dictionary<string, Dictionary<string, string>> mimeDetails = [];

    public void ReadFile(string path)
    {
        var currentCategory = "";
        
        foreach (string line in File.ReadLines(path))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("["))
            {
                currentCategory = trimmedLine.Substring(1, trimmedLine.Length - 2);
            }
            else if (!string.IsNullOrEmpty(trimmedLine))
            {
                
            }
        }
    }
}