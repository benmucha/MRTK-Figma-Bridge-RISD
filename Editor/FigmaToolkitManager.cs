// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using ButtonConfigHelper = Microsoft.MixedReality.Toolkit.UI.ButtonConfigHelper;

namespace Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter
{
    /// <summary>
    /// Main class that interfaces with the Figma API and rebuilds the Figma pages
    /// from MRTK prefabs.
    /// </summary>
    public class FigmaToolkitManager
    {
        private HttpClient client = null;
        public FigmaSettings settings;
        public FigmaToolkitData data;
        public FigmaToolkitManager(FigmaSettings _settings, FigmaToolkitData _data)
        {
            settings = _settings;
            settings.DefaultCustomMap = Resources.Load<FigmaToolkitCustomMap>("Custom Maps/DefaultMap");
            data = _data;
        }

        public async void RefreshFile(string fileID)
        {
            await GetFile(fileID);
            Debug.Log($"File: {fileID} Refreshed");
        }

        public async Task<string> GetFile(string figmaFileKey)
        {
            if (string.IsNullOrEmpty(settings.FigmaToken))
            {
                Debug.LogError("Figma Token missing");
                return null;
            }

            Debug.Log($"Getting {figmaFileKey} from REST API");
            if (client == null)
            {
                client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-Figma-Token", settings.FigmaToken);
            }
            try
            {
                string responseBody = await client.GetStringAsync($"{FigmaSettings.FigmaBaseURL}/files/{figmaFileKey}");

                responseBody = Uri.UnescapeDataString(responseBody);

                string directory = $"{FigmaSettings.FigmaBasePath}/FigmaFiles";
                Directory.CreateDirectory($"{FigmaSettings.FigmaBasePath}/FigmaFiles");
                System.IO.File.WriteAllText($"{directory}/{figmaFileKey}.json", responseBody);
                AssetDatabase.Refresh();
                Debug.Log($"File:{figmaFileKey} Retrieved");
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"\nException Caught!\nMessage :{e.Message}");
            }

            return null;
        }

        public FileResponse BuildFigmaResponse(string jsonResponse)
        {
            try
            {
                FileResponse response = FileResponse.FromJson(jsonResponse);
                return response;
            }
            catch (Exception e)
            {
                Debug.Log($"Error Building Document: {e.Message}");
                throw;
            }
        }

        public RootNode GetDocument(FigmaFile figmaFile)
        {
            FileResponse response = BuildFigmaResponse(figmaFile.textAsset.text);
            figmaFile.name = response.name;

            return response.document;
        }

        public void GetLocalFiles()
        {
            if (data.files == null)
            {
                data.files = new List<FigmaFile>();
            }

            TextAsset[] files = Resources.LoadAll<TextAsset>("FigmaFiles");
            bool filesChanged = false;
            foreach (TextAsset t in files)
            {
                if (data.files.Exists(x => x.fileID == t.name) == false)
                {
                    FigmaFile file = new FigmaFile { fileID = t.name, textAsset = t };
                    data.files.Add(file);
                    RootNode doc = GetDocument(file);
                    filesChanged = true;
                }
            }
            if (filesChanged)
            {
                EditorUtility.SetDirty(FigmaToolkitData.EditorGetOrCreateData());
            }

        }

        public void DeleteLocalFile(string fileID)
        {
            data.files.Remove(data.files.Find(x => x.fileID == fileID));
            FileUtil.DeleteFileOrDirectory($"{FigmaSettings.FigmaBasePath}/FigmaFiles/{fileID}.meta");
            FileUtil.DeleteFileOrDirectory($"{FigmaSettings.FigmaBasePath}/FigmaFiles/{fileID}.json");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void BuildDocument(string documentName, List<Node> nodes)
        {
            GameObject documentRoot = GameObject.Find(documentName);
            if (documentRoot == null)
            {
                documentRoot = new GameObject();
                documentRoot.name = documentName;
            }

            // make the game object hierarchy
            InitImport();

            foreach (Node item in nodes)
            {
                Build(item, documentRoot.transform);
            }
        }

        private Transform framesFolderTransform;
        private Vector2 frameSize;
        private Vector2 frameSizeCenterOffset;
        private void InitImport()
        {
            FramesFolder figmaImporterManager = UnityEngine.Object.FindObjectOfType<FramesFolder>();
            framesFolderTransform = figmaImporterManager.transform;
            frameSize = figmaImporterManager.config.frameSize;
            frameSizeCenterOffset = new Vector2(-frameSize.X / 2, frameSize.Y / 2);
            ClearForReimport();
        }
        private void ClearForReimport()
        {
            // Can't do a for each loop, because then you get the classic problem of deletion during iteration skipping items in the loop.
            while (framesFolderTransform.childCount != 0)
                UnityEngine.Object.DestroyImmediate(framesFolderTransform.GetChild(0).gameObject);
        }

        private void Build(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document, Transform parent, Frame containingFrame = null)
        {
            GameObject go = null;

            switch (document.type)
            {
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Canvas:
                    go = BuildCanvas(document);
                    break;
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Frame:
                    go = BuildFrame(document);
                    break;
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Group:
                    go = BuildGroup(document);
                    break;
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Instance:
                    go = BuildInstance(document, parent, containingFrame);
                    break;
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Rectangle:
                    go = BuildRectange(document);
                    break;
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Text:
                    go = BuildText(document);
                    break;
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Boolean:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Component:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.ComponentSet:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Document:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Ellipse:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Line:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.RegularPolygon:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Slice:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Star:
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Vector:
                default:
                    //Debug.Log($"{document.type} named {document.name} was not built");
                    break;
            }

            if (!go)
            {
                go = new GameObject();
                go.name = document.name;
            }

            if (containingFrame == null && document.type == NodeType.Frame)
            {
                go.transform.SetParent(framesFolderTransform, true);

                Vector2 thisFrameSize = new Vector2(document.absoluteBoundingBox.width, document.absoluteBoundingBox.height);
                if (!frameSize.Equals(thisFrameSize))
                {
                    Debug.LogWarning($"Figma Error: Figma frame \"{document.name}\" of size {thisFrameSize} does not match the expected frame size {frameSize}. Please fix this issue by either updating the expected frame size in the config or by fixing the frame in the Figma file.");
                }

                containingFrame = go.AddComponent<Frame>();
                containingFrame.Init(document);
                go.transform.localPosition = Vector3.zero;
                if (document.name == "SOFTWARE TEST")
                {
                    Debug.Log("FOUND IT");
                    go.SetActive(true);
                    var backgroundPrefab = AssetDatabase.LoadAssetAtPath("Packages/com.risd.figmabridge/Player/DebugBackground.prefab", typeof(UnityEngine.Object));
                    GameObject background = (GameObject)UnityEngine.Object.Instantiate(backgroundPrefab, containingFrame.transform);
                    background.transform.localPosition = new Vector3(0, 0, 0.01f);
                    background.transform.localScale = ((UnityEngine.Vector2)frameSize) * FigmaSettings.PositionScale;
                }
                else
                {
                    go.SetActive(false);
                }
            }
            else
            {
                go.transform.SetParent(parent, true);
            }

            DebugComponent debugComponent = go.AddComponent<DebugComponent>();
            debugComponent.Init(document, FigmaSettings.PositionScale, containingFrame);

            if (document.children != null && document.type != Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Instance)
            {
                if (document.type != Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.ComponentSet)
                {
                    foreach (Node item in document.children)
                    {
                        Build(item, go?.transform, containingFrame);
                    }
                }
            }

            // Prevent top-level "Page N" from being turned off
            if (document.absoluteBoundingBox == null)
            {
                go.SetActive(true);
            }

            if (!document.visible)
            {
                //go.SetActive(false);
            }
            //go.SetActive(!document.visible);
            go.name += $" [{document.type}]"; // Add node type to GameObject name.
        }

        private GameObject BuildBase(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document)
        {
            GameObject go = new GameObject(document.name);
            return go;
        }

        private GameObject BuildCanvas(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document)
        {
            GameObject go = BuildBase(document);
            return go;
        }

        private GameObject BuildFrame(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document)
        {
            GameObject go = BuildBase(document);
            SetPosition(document, go);
            return go;
        }

        private GameObject BuildGroup(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document)
        {
            GameObject go = BuildBase(document);
            return go;
        }

        private GameObject BuildRectange(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document)
        {
            GameObject go = BuildBase(document);
            return go;
        }
        private GameObject BuildText(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document, FigmaToolkitCustomMap customMap = null)
        {
            if (customMap == null)
            {
                customMap = settings.DefaultCustomMap;
            }
            // Create TextMeshPro object
            // Assign text from TextNode
            // Assign fontsize from TextNode
            // Assign font from TextNode

            GameObject go = new GameObject(document.name);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = document.characters;
            tmp.fontSize = document.style.FontSize;
            tmp.font = customMap.defaultFont;

            // TextAlignHorizontal { Center, Justified, Left, Right };
            // Default == Left
            if (document.style.TextAlignHorizontal == Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.TextAlignHorizontal.Center)
            {
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
            }
            else if (document.style.TextAlignHorizontal == Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.TextAlignHorizontal.Justified)
            {
                tmp.alignment = TMPro.TextAlignmentOptions.Justified;
            }
            else if (document.style.TextAlignHorizontal == Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.TextAlignHorizontal.Right)
            {
                tmp.alignment = TMPro.TextAlignmentOptions.Right;
            }



            // Applying scale
            go.transform.localScale = new Vector3(FigmaSettings.PositionScale * 10.0f, FigmaSettings.PositionScale * 10.0f, FigmaSettings.PositionScale * 10.0f);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new UnityEngine.Vector2(document.absoluteBoundingBox.width, document.absoluteBoundingBox.height) * 0.10f;

            // Positioning
            rect.position = document.absoluteBoundingBox.Position * FigmaSettings.PositionScale;

            Vector3[] v = new Vector3[4];
            rect.GetWorldCorners(v);
            rect.Translate(rect.position - v[1]);

            return go;
        }

        private GameObject BuildInstance(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node node, Transform parent, Frame frame, FigmaToolkitCustomMap customMap = null)
        {
            if (customMap == null)
            {
                customMap = settings.DefaultCustomMap;
            }

            CustomMapItem mapItem;
            GameObject go;

            string componentName = node.name.Contains("/") ? node.name.Split('/')[0] : node.name;
            if (customMap.componentMap.TryGetValue(componentName, out mapItem))
            {
                if (mapItem.Prefab != null)
                {
                    go = UnityEngine.Object.Instantiate(mapItem.Prefab, Vector3.zero, mapItem.Prefab.transform.rotation, parent);
                    Vector3 frameOffset = frame != null ? frame.absolutePos : Vector3.zero;
                    Vector2 instanceSizeOffset = new Vector2(node.absoluteBoundingBox.width/2, -node.absoluteBoundingBox.height/2);
                    go.transform.localPosition = (node.absoluteBoundingBox.Position - frameOffset + instanceSizeOffset + frameSizeCenterOffset) * FigmaSettings.PositionScale;

                    // Post-Process.
                    ///PostProcess(node, mapItem, go);

                    //go.transform.Translate(go.transform.position - GetTopLeft(go));

                    // Applying offset.
                    ///go.transform.Translate(mapItem.offset);
                    return go;
                }
                else
                {
                    Debug.Log($"{node.name} not found in prefab Library");
                    go = BuildBase(node);
                    SetPosition(node, go);
                    return go;
                }
            }
            else
            {
                Debug.Log($"{componentName} (original name: {node.name}) not found in prefab Library");
                go = BuildBase(node);
                SetPosition(node, go);
                return go;
            }
        }

        private void PostProcess(Node node, CustomMapItem mapItem, GameObject go)
        {
            switch (mapItem.ProcessType)
            {
                case PostProcessType.Default:
                    break;
                case PostProcessType.Button:
                    ButtonConfigHelper buttonConfig = go.GetComponent<Microsoft.MixedReality.Toolkit.UI.ButtonConfigHelper>();
                    if (buttonConfig != null)
                    {
                        buttonConfig.MainLabelText = GetText(node);
                    }
                    break;
                case PostProcessType.ButtonCollection:
                    // Get buttons in prefab
                    ButtonConfigHelper[] buttonConfigHelpers = go.transform.Find("ButtonCollection").GetComponentsInChildren<ButtonConfigHelper>();

                    //Get buttons in node children
                    Node[] buttonNodes = Array.FindAll(node.children, x => x.name == "Buttons");

                    //Get text in each button
                    List<string> nodeTexts = new List<string>();
                    if (buttonNodes != null)
                    {
                        foreach (Node item in buttonNodes)
                        {
                            foreach (Node child in item.children)
                            {
                                nodeTexts.Add(GetText(child));
                            }
                        }
                    }
                    //Assign appropriate text to button
                    for (int i = 0; i < buttonConfigHelpers.Length; i++)
                    {
                        buttonConfigHelpers[i].MainLabelText = nodeTexts[i];

                    }
                    break;
                case PostProcessType.Backplate:
                    go.transform.localScale = new Vector3(node.absoluteBoundingBox.width * FigmaSettings.PositionScale, node.absoluteBoundingBox.height * FigmaSettings.PositionScale, go.transform.localScale.z);
                    break;
                case PostProcessType.Slider:
                    // Get default size. Hardcoding for now.
                    float defaultWidth = 764f;
                    // Get current size.
                    float currentWidth = node.absoluteBoundingBox.width;
                    // Getting scaling factor.
                    // Using x for uniform scaling.
                    float scaleFactor = currentWidth / defaultWidth;
                    // Applying scale.
                    go.transform.localScale *= scaleFactor;
                    break;
                default:
                    break;
            }
        }

        private void SetPosition(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.Node document, GameObject go)
        {
            if (document.absoluteBoundingBox != null)
            {
                go.transform.localPosition = document.absoluteBoundingBox.Position * FigmaSettings.PositionScale;
            }
        }

        private Vector3 GetTopLeft(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            // Add all the renderer bounds in the hierarchy 
            foreach (Renderer r in renderers) { bounds.Encapsulate(r.bounds); }
            return new Vector3(bounds.min.x, bounds.max.y);
        }

        private string GetText(Node node)
        {
            if (node == null)
            {
                return null;
            }
            if (node.type == NodeType.Text)
            {
                return node.characters;
            }
            if (node.children != null)
            {
                foreach (Node child in node.children)
                {
                    string found = GetText(child);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
    }
}