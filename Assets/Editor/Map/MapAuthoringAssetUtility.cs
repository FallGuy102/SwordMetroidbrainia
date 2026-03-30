using System.IO;
using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapAuthoringAssetUtility
    {
        public static void PickRoomFolder(MapAuthoringRoot root)
        {
            var currentPath = string.IsNullOrWhiteSpace(root.RoomAssetFolder) ? "Assets" : root.RoomAssetFolder;
            var absoluteStartPath = Path.GetFullPath(currentPath);
            var selectedPath = EditorUtility.OpenFolderPanel("Choose Room Asset Folder", absoluteStartPath, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var projectPath = Path.GetFullPath(Application.dataPath + "/..");
            if (!selectedPath.StartsWith(projectPath))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Please choose a folder inside this Unity project.", "OK");
                return;
            }

            var relativePath = selectedPath.Replace(projectPath, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relativePath = relativePath.Replace('\\', '/');

            Undo.RecordObject(root, "Change Room Asset Folder");
            root.RoomAssetFolder = relativePath;
            EditorUtility.SetDirty(root);
        }

        public static void OpenRoomFolder(MapAuthoringRoot root)
        {
            EnsureFolderExists(root.RoomAssetFolder);
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(root.RoomAssetFolder);
            if (folderAsset == null)
            {
                return;
            }

            Selection.activeObject = folderAsset;
            EditorGUIUtility.PingObject(folderAsset);
        }

        public static MapRoomDefinition CreateRoomAsset(MapAuthoringRoot root, string roomName)
        {
            var sanitizedName = string.IsNullOrWhiteSpace(roomName) ? "NewRoom" : roomName.Trim();
            EnsureFolderExists(root.RoomAssetFolder);

            var requestedAssetPath = $"{root.RoomAssetFolder}/{sanitizedName}.asset";
            var existingRoom = AssetDatabase.LoadAssetAtPath<MapRoomDefinition>(requestedAssetPath);
            if (existingRoom != null)
            {
                return existingRoom;
            }

            var assetPath = AssetDatabase.LoadMainAssetAtPath(requestedAssetPath) == null
                ? requestedAssetPath
                : AssetDatabase.GenerateUniqueAssetPath(requestedAssetPath);
            var room = ScriptableObject.CreateInstance<MapRoomDefinition>();
            AssetDatabase.CreateAsset(room, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return room;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalizedPath = folderPath.Replace('\\', '/');
            var parts = normalizedPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
