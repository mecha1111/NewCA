using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// TextMeshPro 준비 도구.
    ///
    /// Unity 6에서는 TMP가 별도 패키지가 아니라 com.unity.ugui에 포함돼 있어 패키지 설치가 필요 없다.
    /// 다만 TMP는 "TMP Essential Resources"(TMP Settings·기본 셰이더/머티리얼)가 프로젝트에 임포트돼
    /// 있어야 동작하므로 그것만 넣어주면 된다.
    ///
    /// 두 단계를 나눈 이유: 에셋 임포트는 완료 후 도메인 리로드가 필요해서, 한 번의 배치 실행 안에서
    /// 임포트하고 곧바로 그 결과를 쓰면 타입이 잡히지 않을 수 있다. 실행을 나누면 확실하다.
    /// </summary>
    public static class TmpSetupTool
    {
        private const string FontPath = "Assets/Art/Fonts/KOHINanum_Bold.ttf";
        private const string FontAssetPath = "Assets/Art/Fonts/KOHINanum_Bold SDF.asset";

        [MenuItem("CrossAccel/Art/1. Import TMP Essential Resources")]
        public static void ImportEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[TMP] Essential Resources가 이미 임포트돼 있음 — 건너뜀");
                return;
            }

            var package = Directory
                .GetDirectories("Library/PackageCache")
                .Where(d => Path.GetFileName(d).StartsWith("com.unity.ugui"))
                .Select(d => Path.Combine(d, "Package Resources", "TMP Essential Resources.unitypackage"))
                .FirstOrDefault(File.Exists);

            if (package == null)
            {
                Debug.LogError("[TMP] TMP Essential Resources.unitypackage를 찾지 못했습니다.");
                return;
            }

            Debug.Log($"[TMP] 임포트: {package}");
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// KOHINanum_Bold.ttf → TMP Font Asset.
        /// 한글은 글리프가 수천 자라 정적 아틀라스로 구우면 폰트 텍스처가 과도하게 커진다.
        /// Dynamic 모드로 만들어 실제로 쓰이는 글자만 런타임에 아틀라스로 채우게 한다.
        /// </summary>
        [MenuItem("CrossAccel/Art/2. Create KOHINanum TMP Font Asset")]
        public static void CreateFontAsset()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError($"[TMP] 폰트를 찾지 못했습니다: {FontPath}");
                return;
            }

            // Dynamic 폰트 애셋은 원본 TTF의 폰트 데이터를 런타임에 읽으므로 반드시 포함시켜야 한다.
            if (AssetImporter.GetAtPath(FontPath) is TrueTypeFontImporter importer && !importer.includeFontData)
            {
                importer.includeFontData = true;
                importer.SaveAndReimport();
                Debug.Log("[TMP] TTF includeFontData=true 로 재임포트");
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[TMP] 폰트 애셋 생성 실패");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);

            if (File.Exists(FontAssetPath)) AssetDatabase.DeleteAsset(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            // 아틀라스 텍스처와 머티리얼은 서브 애셋으로 함께 저장해야 참조가 끊기지 않는다.
            if (fontAsset.atlasTextures != null)
            {
                foreach (var atlas in fontAsset.atlasTextures)
                {
                    atlas.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMP] 폰트 애셋 생성 완료: {FontAssetPath} (Dynamic, SDFAA, 1024x1024)");
        }
    }
}
