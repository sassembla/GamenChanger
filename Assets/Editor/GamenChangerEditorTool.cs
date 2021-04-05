using System.Collections.Generic;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class GamenChangerEditorTool
{
    [MenuItem("Window/GamenChanger/Update UnityPackage")]
    public static void UnityPackage()
    {
        var assetPaths = new List<string>();

        var frameworkPath = "Assets/GamenChanger";
        CollectPathRecursive(frameworkPath, assetPaths);

        AssetDatabase.ExportPackage(assetPaths.ToArray(), "GamenChanger.unitypackage", ExportPackageOptions.IncludeDependencies);
    }

    private static void CollectPathRecursive(string path, List<string> collectedPaths)
    {
        var filePaths = Directory.GetFiles(path);
        foreach (var filePath in filePaths)
        {
            collectedPaths.Add(filePath);
        }

        var modulePaths = Directory.GetDirectories(path);
        foreach (var folderPath in modulePaths)
        {
            CollectPathRecursive(folderPath, collectedPaths);
        }
    }

    static GamenChangerEditorTool()
    {
        // create unitypackage if compiled.
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityPackage();
        }
    }
}