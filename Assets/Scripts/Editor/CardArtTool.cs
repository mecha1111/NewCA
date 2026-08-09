using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossAccel.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CrossAccel.EditorTools
{
    /// <summary>
    /// Assets/Art를 스캔해 CardArtLibrary(Resources)를 만들거나 갱신한다.
    /// 일러스트/풀카드를 새로 추가했을 때 이 메뉴만 다시 돌리면 카드에 자동으로 붙는다.
    /// </summary>
    public static class CardArtTool
    {
        private const string FramePath = "Assets/Art/Frames/card_frame_character.png";
        private const string ScrimPath = "Assets/Art/Frames/bottom_scrim.png";
        private const string FontAssetPath = "Assets/Art/Fonts/KOHINanum_Bold SDF.asset";
        private const string IllustrationFolder = "Assets/Art/Illustrations";
        private const string CardsFolder = "Assets/Art/Cards";
        private const string LibraryPath = "Assets/Resources/CardArtLibrary.asset";

        [MenuItem("CrossAccel/Art/3. Build Card Art Library")]
        public static void BuildLibrary()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            GenerateScrimTexture();

            var frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            if (frame == null) Debug.LogError($"[CardArt] 프레임 스프라이트를 찾지 못함: {FramePath}");

            var scrim = AssetDatabase.LoadAssetAtPath<Sprite>(ScrimPath);
            if (scrim == null) Debug.LogError($"[CardArt] 스크림 스프라이트를 찾지 못함: {ScrimPath}");

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null) Debug.LogError($"[CardArt] TMP 폰트 애셋을 찾지 못함: {FontAssetPath}");

            var illustrationEntries = ScanFolder(IllustrationFolder);
            var cardImageEntries = ScanFolder(CardsFolder);

            var library = AssetDatabase.LoadAssetAtPath<CardArtLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CardArtLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.EditorPopulate(frame, scrim, font, illustrationEntries, cardImageEntries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CardArt] 라이브러리 갱신: 초상 {illustrationEntries.Count}종, 풀카드 {cardImageEntries.Count}종, " +
                      $"프레임={(frame != null ? "O" : "X")}, 스크림={(scrim != null ? "O" : "X")}, 폰트={(font != null ? "O" : "X")}");
        }

        /// <summary>
        /// [무엇] 폴더 안 PNG를 카드 ID(파일명) → Sprite 목록으로 모은다.
        /// [주의] Texture Type이 Sprite가 아니면 LoadAssetAtPath가 null을 반환한다.
        /// </summary>
        private static List<CardArtLibrary.Entry> ScanFolder(string folder)
        {
            var list = new List<CardArtLibrary.Entry>();
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"[CardArt] 폴더 없음: {folder}");
                return list;
            }

            foreach (var path in Directory.GetFiles(folder, "*.png").OrderBy(p => p))
            {
                string assetPath = path.Replace('\\', '/');
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[CardArt] Sprite 로드 실패(Texture Type 확인): {assetPath}");
                    continue;
                }

                list.Add(new CardArtLibrary.Entry
                {
                    CardId = Path.GetFileNameWithoutExtension(path),
                    Illustration = sprite
                });
            }

            return list;
        }

        /// <summary>
        /// 하단 스크림 그라데이션 PNG를 굽는다 (위 투명 → 아래 CardArt.ScrimColor * ScrimMaxAlpha).
        /// 런타임에 Texture2D를 만들어 쓰면 씬에 저장할 때 참조가 끊기므로 실제 에셋으로 만든다.
        /// </summary>
        private static void GenerateScrimTexture()
        {
            const int width = 4;
            const int height = 512;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var color = CardArt.ScrimColor;

            for (int y = 0; y < height; y++)
            {
                float t = 1f - y / (float)(height - 1);
                float p = Mathf.Clamp01(t / CardArt.ScrimRampEnd);
                float eased = p * p * (3f - 2f * p);

                byte alpha = (byte)Mathf.RoundToInt(eased * CardArt.ScrimMaxAlpha * 255f);
                var pixel = new Color32(color.r, color.g, color.b, alpha);

                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, pixel);
            }
            texture.Apply();

            File.WriteAllBytes(ScrimPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(ScrimPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(ScrimPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Debug.Log($"[CardArt] 스크림 그라데이션 생성: {ScrimPath} ({width}x{height}, 최대 알파 {CardArt.ScrimMaxAlpha:0.##})");
        }
    }
}