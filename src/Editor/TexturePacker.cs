#if UNITY_EDITOR

namespace Woolction.TexturePacker
{
    using UnityEngine;
    using UnityEditor;
    using System.IO;
    using System.Collections.Generic;

    enum TargetChannel { R, G, B, A }
    public class TexturePacker : EditorWindow
    {
        enum ChannelSource { RGB, Red, Green, Blue, Alpha, Mask, Color }
        enum CreateType { Default, InverseRNS }

        class Slot
        {
            public bool enabled = true;
            public Texture2D texture = null;
            public TargetChannel target = TargetChannel.R;
            public ChannelSource source = ChannelSource.Red;
            public CreateType createType = CreateType.Default;
            public Color color;
            public string label = "Slot";
        }

        struct TextureSettings
        {
            public TextureImporter textureImporter;

            //settings
            public int maxTextureSize;
            public TextureImporterAlphaSource alphaSource;
            public TextureImporterCompression compression;
            public TextureImporterType textureType;   
        }

        private Slot[] slots = new Slot[4];
        private List<TextureSettings> textures = new();
        private string outputFolderRelative = "Assets";
        private string outputFilename = "PackedTexture.png";
        private int outputSize = 2048;
        private bool forceReadableTextures = true;
        private bool overwriteExisting = true;
        private bool autoFormating = true;
        private bool sRGB = false;
        private TextureFormat format = TextureFormat.RGBA32;

        [MenuItem("Tools/Texture Packer")]
        public static void OpenWindow()
        {
            var w = GetWindow<TexturePacker>("Texture Packer");
            w.minSize = new Vector2(520, 480);
            w.maxSize = new Vector2(520, 480);
        }

        void OnEnable()
        {
            for (int i = 0; i < 4; i++)
            {
                if (slots[i] == null)
                    slots[i] = new Slot() { label = $"Slot {i+1}", target = (TargetChannel)i };
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Packed Texture Packer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            for (int i = 0; i < 4; i++)
            {
                var s = slots[i];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                s.enabled = EditorGUILayout.Toggle(s.enabled, GUILayout.Width(18));
                s.label = EditorGUILayout.TextField(s.label, GUILayout.Width(120));
                s.texture = (Texture2D)EditorGUILayout.ObjectField(s.texture, typeof(Texture2D), false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Target", GUILayout.Width(50));
                s.target = (TargetChannel)EditorGUILayout.EnumPopup(s.target, GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                GUILayout.Label("Source", GUILayout.Width(50));
                if (s.source == ChannelSource.Color)
                {
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(60));
                    s.color = EditorGUI.ColorField(rect, GUIContent.none, s.color, false, false, false);
                }

                s.source = (ChannelSource)EditorGUILayout.EnumPopup(s.source, GUILayout.Width(100));

                GUILayout.Label("Type", GUILayout.Width(40));
                s.createType = (CreateType)EditorGUILayout.EnumPopup(s.createType, GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output folder (inside Assets):", GUILayout.Width(200));
            outputFolderRelative = EditorGUILayout.TextField(outputFolderRelative);

            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string abs = EditorUtility.OpenFolderPanel("Select output folder (choose a folder inside your project Assets)", Application.dataPath, "");

                if (!string.IsNullOrEmpty(abs))
                {
                    if (abs.StartsWith(Application.dataPath))
                    {
                        outputFolderRelative = "Assets" + abs.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid folder", "Please select a folder inside this Unity project's Assets folder.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            outputFilename = EditorGUILayout.TextField("Output filename", outputFilename);
            EditorGUILayout.BeginHorizontal();
            outputSize = EditorGUILayout.IntField("Size", outputSize);
            EditorGUILayout.EndHorizontal();

            format = (TextureFormat)EditorGUILayout.EnumPopup("Texture write format", format, GUILayout.Width(275));

            overwriteExisting = EditorGUILayout.Toggle("Overwrite existing file", overwriteExisting);
            autoFormating = EditorGUILayout.Toggle("Auto Texture Formating", autoFormating);
            sRGB = EditorGUILayout.Toggle("Enabling sRGB", sRGB);
        

            EditorGUILayout.Space();

            if (GUILayout.Button("Pack and Save", GUILayout.Height(36)))
            {
                PackAndSave();
            }
        }

        void PackAndSave()
        {
            if (string.IsNullOrEmpty(outputFolderRelative) || !outputFolderRelative.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Output folder error", "Please set an output folder inside the project's Assets folder.", "OK");
                return;
            }
            if (string.IsNullOrEmpty(outputFilename))
            {
                EditorUtility.DisplayDialog("Filename error", "Please set a valid output filename (e.g. Packed.png).", "OK");
                return;
            }

            string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), outputFolderRelative, outputFilename);
            string assetRelativePath = Path.Combine(outputFolderRelative, outputFilename).Replace("\\","/");

            if (File.Exists(fullPath) && !overwriteExisting)
            {
                if (!EditorUtility.DisplayDialog("File exists", "File already exists. Overwrite?", "Yes", "No"))
                    return;
            }

            for (int i = 0; i < 4; i++)
            {
                var t = slots[i].texture;

                if (t == null || !slots[i].enabled)
                    continue;
                
                if (forceReadableTextures)
                {
                    string texPath = AssetDatabase.GetAssetPath(t);

                    if (!string.IsNullOrEmpty(texPath))
                    {
                        var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;

                        if (importer != null)
                        {
                            if (!importer.isReadable)
                            {
                                importer.isReadable = true;
                            }

                            textures.Add(new TextureSettings()
                            {
                                textureImporter = importer,

                                maxTextureSize = importer.maxTextureSize,
                                compression = importer.textureCompression,
                                alphaSource = importer.alphaSource,
                                textureType = importer.textureType
                            });

                            importer.maxTextureSize = outputSize; 

                            importer.textureCompression = TextureImporterCompression.Uncompressed;
                            importer.alphaSource = TextureImporterAlphaSource.FromInput;
                            importer.textureType = TextureImporterType.Default;

                            importer.SaveAndReimport();
                        }
                    }
                }
            }

            Texture2D outTex = new Texture2D(outputSize, outputSize, format, false);

            for (int y = 0; y < outputSize; y++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    Color outC = new Color(0,0,0,1);
                    float u = (outputSize > 1) ? (float)x / (outputSize - 1) : 0f;
                    float v = (outputSize > 1) ? (float)y / (outputSize - 1) : 0f;

                    for (int sIndex = 0; sIndex < 4; sIndex++)
                    {
                        var s = slots[sIndex];

                        if (!s.enabled || s.texture == null) 
                            continue;

                        Texture2D tex = s.texture;

                        Color sample = SampleTextureNormalized(tex, u, v);

                        float val = 0;
                        Color solidColor = new();

                        switch (s.source)
                        {
                            case ChannelSource.RGB:
                                val = Mathf.Max(sample.r + sample.g + sample.b);
                                break;
                            case ChannelSource.Red:
                                val = sample.r;
                                break;
                            case ChannelSource.Green:
                                val = sample.g;
                                break;
                            case ChannelSource.Blue:
                                val = sample.b;
                                break;
                            case ChannelSource.Alpha:
                                val = sample.a;
                                break;
                            case ChannelSource.Mask:
                                int r = (int)(sample.r * 255f);
                                int g = (int)(sample.g * 255f);
                                int b = (int)(sample.b * 255f);

                                int packed = (r << 16) | (g << 8) | b;
                                val = packed / 16777215f;
                                break;
                            case ChannelSource.Color:
                                solidColor = s.color;
                                break;
                        }

                        if (s.createType == CreateType.InverseRNS)
                        {
                            val = 1 - val;
                            solidColor = new(1 - s.color.r, 1 - s.color.g, 1 - s.color.b, 1 - s.color.a);
                        }

                        if (s.source == ChannelSource.Color)
                        {
                            switch (s.target)
                            {
                                case TargetChannel.R:
                                    outC.r = solidColor.r;
                                    break;
                                case TargetChannel.G:
                                    outC.g = solidColor.g;
                                    break;
                                case TargetChannel.B:
                                    outC.b = solidColor.b;
                                    break;
                                case TargetChannel.A:
                                    outC.a = solidColor.a;
                                    break;
                            }
                        }
                        else
                        {
                            switch (s.target)
                            {
                                case TargetChannel.R:
                                    outC.r = val;
                                    break;
                                case TargetChannel.G:
                                    outC.g = val;
                                    break;
                                case TargetChannel.B:
                                    outC.b = val;
                                    break;
                                case TargetChannel.A:
                                    outC.a = val;
                                    break;
                            }   
                        }
                    }

                    outTex.SetPixel(x, y, outC);
                }
            }

            outTex.Apply();

            byte[] png = outTex.EncodeToPNG();
            try
            {
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(fullPath, png);
                AssetDatabase.Refresh();

                var imp = AssetImporter.GetAtPath(assetRelativePath) as TextureImporter;

                if (imp != null)
                {
                    imp.textureType = TextureImporterType.Default;
                    imp.isReadable = false;

                    if (autoFormating)
                    {
                        byte activeSlot = 0;

                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (slots[i].enabled)
                            {
                                activeSlot++;
                            }
                        }

                        TextureImporterFormat format = TextureImporterFormat.Automatic;

                        switch (activeSlot)
                        {
                            case 1: format = TextureImporterFormat.BC4; break;
                            case 2: format = TextureImporterFormat.BC5; break;
                            case 3: format = TextureImporterFormat.BC6H; break;
                            case 4: format = TextureImporterFormat.BC7; break;
                        }

                        imp.SetPlatformTextureSettings("Standalone", imp.maxTextureSize, format, (int)TextureCompressionQuality.Normal, false);
                    }
                    else
                    {
                        imp.textureCompression = TextureImporterCompression.Uncompressed;
                    }

                    imp.sRGBTexture = sRGB;
                    imp.SaveAndReimport();
                }

                for (int i = 0; i < textures.Count; i++)
                {   
                    TextureImporter textureImporter = textures[i].textureImporter;

                    textureImporter.isReadable = true;

                    textureImporter.maxTextureSize = textures[i].maxTextureSize;
                    textureImporter.textureCompression = textures[i].compression;
                    textureImporter.textureType = textures[i].textureType;
                    textureImporter.alphaSource = textures[i].alphaSource;

                    textureImporter.SaveAndReimport();
                }

                textures.Clear();

                EditorUtility.DisplayDialog("Success", "Packed texture saved to:\n" + assetRelativePath, "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to save packed texture: " + ex.Message);
                EditorUtility.DisplayDialog("Save error", "Failed to write file: " + ex.Message, "OK");
            }
        }

        Color SampleTextureNormalized(Texture2D tex, float u, float v)
        {   
            if (tex == null) return Color.black;

            try
            {
                return tex.GetPixelBilinear(u, v);
            }
            catch
            {
                return Color.black;
            }
        }

    }   
}

#endif