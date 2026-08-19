using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PoorSmith.Editor
{
    /// <summary>
    /// Design/notion/*.json 을 읽어들이는 공통 부분.
    /// 이 JSON들은 노션에서 추출한 원본이므로 손으로 고치지 않는다.
    /// </summary>
    internal static class NotionSourceFiles
    {
        const string SourceFolder = "Design/notion";

        internal static string PathOf(string fileName) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceFolder, fileName));

        /// <summary>읽기에 실패하면 사용자에게 이유를 보여주고 null을 반환한다.</summary>
        internal static T Read<T>(string fileName) where T : class
        {
            var path = PathOf(fileName);

            if (!File.Exists(path))
            {
                Fail($"원본 파일을 찾을 수 없습니다.\n\n{SourceFolder}/{fileName}\n\n" +
                     "저장소를 최신으로 받았는지 확인해주세요.");
                return null;
            }

            try
            {
                var parsed = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (parsed == null)
                    Fail($"{fileName} 을 해석하지 못했습니다. JSON 형식이 깨졌을 수 있습니다.");
                return parsed;
            }
            catch (Exception e)
            {
                Fail($"{fileName} 을 읽는 중 오류가 발생했습니다.\n\n{e.Message}");
                return null;
            }
        }

        static void Fail(string message)
        {
            Debug.LogError($"[노션 임포터] {message}");
            EditorUtility.DisplayDialog("노션 데이터 불러오기 실패", message, "확인");
        }

        /// <summary>Assets 아래 경로를 한 단계씩 만들어 둔다.</summary>
        internal static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            var parts = assetFolderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>없으면 만들고, 있으면 그대로 쓴다. 기존 참조를 유지하기 위해 지우고 새로 만들지 않는다.</summary>
        internal static T LoadOrCreate<T>(string assetPath, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            created = asset == null;

            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            return asset;
        }
    }
}
