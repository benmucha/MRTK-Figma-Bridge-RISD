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

        public void BuildDocument(string documentName, List<NodeData> nodes)
        {
            GameObject documentRoot = GameObject.Find(documentName);
            if (documentRoot == null)
            {
                documentRoot = new GameObject();
                documentRoot.name = documentName;
            }

            // make the game object hierarchy
            InitImport();

            foreach (NodeData item in nodes)
            {
                Build(item, new InstantiatedNode(documentRoot));
            }
        }

        private Transform framesFolderTransform;
        private Vector2 frameSize;
        private Vector2 frameSizeCenterOffset;
        private void InitImport()
        {
            FramesFolder figmaImporterManager = UnityEngine.Object.FindObjectOfType<FramesFolder>();
            if (figmaImporterManager == null)
            {
                throw new Exception("Figma Manager doesn't exist");
            }
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

        private class Node
        {
            public NodeData nodeData;

            public Vector3 Position => nodeData.absoluteBoundingBox == null ? Vector3.zero : nodeData.absoluteBoundingBox.Position;
            public float Width => nodeData.absoluteBoundingBox.width;
            public float Height => nodeData.absoluteBoundingBox.height;
            public Vector2 Size => new Vector2(Width, Height);
            public String Name => nodeData.name;
            public NodeType Type => nodeData.type;
            public bool FromToolkitPrefab => (nodeData.type == NodeType.Instance);
            public bool IsUiBackplate => ToolkitPrefabName == "UI Backplate";
            public string ToolkitPrefabName { 
                get
                {
                    return Name.Contains("/") ? Name.Split('/')[0] : Name;
                } 
            }

            public Node(NodeData data)
            {
                nodeData = data;
            }
        }

        private class InstantiatedNode
        {
            public GameObject go;
            public Transform transform => go.transform;
            public Node node;
            public InstantiatedNode parent;
            public Frame containingFrame;

            public float GetZPos()
            {
                if (this.IsParentFrame)
                {
                    if (node.IsUiBackplate)
                        return 0.01f;
                    else
                        return 0;
                }
                else
                    return transform.localPosition.z;
            }

            public bool IsParentFrame => (parent != null) ? parent.IsFrame : true;
            public bool IsFrame => (node != null) ? node.Type == NodeType.Frame : true;

            public InstantiatedNode(GameObject go)
            {
                this.go = go;
            }
            public InstantiatedNode(Node node, InstantiatedNode parent, Frame containingFrame)
            {
                this.node = node;
                this.parent = parent;
                this.containingFrame = containingFrame;
            }
        }

        InstantiatedNode BuildBaseNode(NodeData nodeData, InstantiatedNode parent, Frame containingFrame)
        {
            FigmaToolkitCustomMap customMap = null;
            Node node = new Node(nodeData);
            InstantiatedNode instantiated = new InstantiatedNode(node, parent, containingFrame);
            GameObject go = BuildGameObject(instantiated);
            if (go == null)
                return null;
            go.name = node.Name;
            instantiated.go = go;
            return instantiated;
        }

        private GameObject BuildGameObject(InstantiatedNode instantiated)
        {
            switch (instantiated.node.Type)
            {
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Canvas:
                    return BuildEmpty(instantiated);
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Frame:
                    return BuildEmpty(instantiated);
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Group:
                    return BuildEmpty(instantiated);
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Instance:
                    return BuildInstance(instantiated);
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Rectangle:
                    return BuildEmpty(instantiated);
                case Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Text:
                    return BuildText(instantiated);
                /*
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
                */
                default:
                    Debug.LogWarning($"{instantiated.node.Name} was not built as its type ({instantiated.node.Type}) is unsupported");
                    return null;
            }
        }

        private GameObject BuildEmpty(InstantiatedNode instantiated)
        {
            GameObject go = new GameObject();
            if (instantiated.containingFrame == null && instantiated.node.Type == NodeType.Frame)
            {
                go.transform.SetParent(framesFolderTransform, true);
            }
            else
            {
                go.transform.SetParent(instantiated.parent.transform);
            }
            return go;
        }

        /// <summary>
        /// Factory for <see cref="InstantiatedNode"/>.
        /// </summary>
        private class NodeBuilder
        {

        }

        private void Build(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData node, InstantiatedNode parent, Frame containingFrame = null)
        {
            InstantiatedNode instantiatedNode = BuildBaseNode(node, parent, containingFrame);
            if (instantiatedNode == null)
                return;
            GameObject go = instantiatedNode.go;
            SetPosition(instantiatedNode);

            if (containingFrame == null && node.type == NodeType.Frame)
            {
                //go.transform.SetParent(framesFolderTransform, true);

                Vector2 thisFrameSize = new Vector2(node.absoluteBoundingBox.width, node.absoluteBoundingBox.height);
                if (!frameSize.Equals(thisFrameSize))
                {
                    Debug.LogWarning($"Figma Error: Figma frame \"{node.name}\" of size {thisFrameSize} does not match the expected frame size {frameSize}. Please fix this issue by either updating the expected frame size in the config or by fixing the frame in the Figma file.");
                }

                containingFrame = go.AddComponent<Frame>();
                containingFrame.Init(node);

                //if (document.name == "SOFTWARE TEST")
                if (node.name == "EVA FULL VIEW")
                {
                    Debug.Log("Found the focus frame");
                    go.SetActive(true);
                    /*
                    var backgroundPrefab = AssetDatabase.LoadAssetAtPath("Packages/com.risd.figmabridge/Player/DebugBackground.prefab", typeof(UnityEngine.Object));
                    GameObject background = (GameObject)UnityEngine.Object.Instantiate(backgroundPrefab, containingFrame.transform);
                    background.transform.localPosition = new Vector3(0, 0, 0.01f);
                    background.transform.localScale = ((UnityEngine.Vector2)frameSize) * FigmaSettings.PositionScale;
                    */
                }
                else
                {
                    go.SetActive(false);
                }
            }
            else
            {
                //go.transform.SetParent(parent.transform, true);
            }

            if (node.type == NodeType.Frame)
            {
                go.transform.localPosition = Vector3.zero;
            }

            DebugComponent debugComponent = go.AddComponent<DebugComponent>();
            debugComponent.Init(node, FigmaSettings.PositionScale, containingFrame);

            if (node.children != null && node.type != Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.Instance)
            {
                if (node.type != Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeType.ComponentSet)
                {
                    foreach (NodeData item in node.children)
                    {
                        Build(item, instantiatedNode, containingFrame);
                    }
                }
            }

            // Prevent top-level "Page N" from being turned off
            if (node.absoluteBoundingBox == null)
            {
                go.SetActive(true);
            }

            if (!node.visible)
            {
                //go.SetActive(false);
            }
            //go.SetActive(!document.visible);
            go.name += $" [{node.type}]"; // Add node type to GameObject name.
        }

        private GameObject BuildBase(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData document)
        {
            GameObject go = new GameObject(document.name);
            return go;
        }

        private GameObject BuildCanvas(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData document)
        {
            GameObject go = BuildBase(document);
            return go;
        }

        private GameObject BuildFrame(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData document)
        {
            GameObject go = BuildBase(document);
            SetPosition(document, go);
            return go;
        }

        private GameObject BuildGroup(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData document)
        {
            GameObject go = BuildBase(document);
            return go;
        }

        private GameObject BuildRectange(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData document)
        {
            GameObject go = BuildBase(document);
            return go;
        }
        private GameObject BuildText(InstantiatedNode instantiated, FigmaToolkitCustomMap customMap = null)
        {
            if (customMap == null)
            {
                customMap = settings.DefaultCustomMap;
            }
            NodeData nodeData = instantiated.node.nodeData;

            // Create TextMeshPro object
            // Assign text from TextNode
            // Assign fontsize from TextNode
            // Assign font from TextNode

            GameObject go = BuildBase(nodeData);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = nodeData.characters;
            tmp.fontSize = nodeData.style.FontSize;
            tmp.font = customMap.defaultFont;

            // TextAlignHorizontal { Center, Justified, Left, Right };
            // Default == Left
            if (nodeData.style.TextAlignHorizontal == Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.TextAlignHorizontal.Center)
            {
                tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
            }
            else if (nodeData.style.TextAlignHorizontal == Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.TextAlignHorizontal.Justified)
            {
                tmp.horizontalAlignment = HorizontalAlignmentOptions.Justified;
            }
            else if (nodeData.style.TextAlignHorizontal == Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.TextAlignHorizontal.Right)
            {
                tmp.horizontalAlignment = HorizontalAlignmentOptions.Right;
            }

            if (nodeData.style.TextAlignVertical == TextAlignVertical.Top)
            {
                tmp.verticalAlignment = VerticalAlignmentOptions.Top;
            }
            else if (nodeData.style.TextAlignVertical == TextAlignVertical.Center)
            {
                tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            }
            else if (nodeData.style.TextAlignVertical == TextAlignVertical.Bottom)
            {
                tmp.verticalAlignment = VerticalAlignmentOptions.Bottom;
            }

            // Applying scale
            go.transform.localScale = new Vector3(FigmaSettings.PositionScale * 10.0f, FigmaSettings.PositionScale * 10.0f, FigmaSettings.PositionScale * 10.0f);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new UnityEngine.Vector2(nodeData.absoluteBoundingBox.width, nodeData.absoluteBoundingBox.height) * 0.10f;
            tmp.enableWordWrapping = false; // If the scale clips text then just let it overflow to look normal.

            // Positioning
            //rect.position = document.absoluteBoundingBox.Position * FigmaSettings.PositionScale;

            Vector3[] v = new Vector3[4];
            rect.GetWorldCorners(v);
            rect.Translate(rect.position - v[1]);

            go.transform.SetParent(instantiated.parent.transform);
            return go;
        }

        private void SetPosition(InstantiatedNode instantiatedNode)
        {
            if (instantiatedNode.node.nodeData.absoluteBoundingBox == null)
                return;
            Vector3 frameOffset = instantiatedNode.containingFrame != null ? instantiatedNode.containingFrame.absolutePos : Vector3.zero;
            Vector2 instanceSizeOffset = new Vector2(instantiatedNode.node.Width / 2, -instantiatedNode.node.Height / 2);
            Vector2 position2d = (instantiatedNode.node.Position - frameOffset + instanceSizeOffset + frameSizeCenterOffset) * FigmaSettings.PositionScale;
            instantiatedNode.transform.localPosition = new Vector3(position2d.X, position2d.Y, instantiatedNode.GetZPos());
        }
        
        private GameObject BuildInstance(InstantiatedNode instantiated, FigmaToolkitCustomMap customMap = null)
        {
            if (customMap == null)
            {
                customMap = settings.DefaultCustomMap;
            }

            Node node = instantiated.node;
            if (customMap.componentMap.TryGetValue(node.ToolkitPrefabName, out CustomMapItem mapItem))
            {
                if (mapItem.Prefab != null)
                {
                    GameObject go = UnityEngine.Object.Instantiate(mapItem.Prefab, instantiated.parent.node.Position, mapItem.Prefab.transform.rotation, instantiated.parent.transform);

                    if (node.IsUiBackplate)
                    {
                        go.transform.localScale = new Vector3(node.Width * FigmaSettings.PositionScale, node.Height * FigmaSettings.PositionScale, go.transform.localScale.z);
                        go.transform.localPosition = new Vector3(go.transform.localPosition.x, go.transform.localPosition.y, 0.01f);
                    }

                    // Post-Process.
                    ///PostProcess(node, mapItem, go);

                    //go.transform.Translate(go.transform.position - GetTopLeft(go));

                    // Applying offset.
                    ///go.transform.Translate(mapItem.offset);

                    return go;
                }
                else
                {
                    Debug.LogWarning($"{node.Name} not found in prefab Library");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"{node.ToolkitPrefabName} (original name: {node.Name}) not found in prefab Library");
                return null;
            }
        }
        
        private void PostProcess(NodeData node, CustomMapItem mapItem, GameObject go)
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
                    NodeData[] buttonNodes = Array.FindAll(node.children, x => x.name == "Buttons");

                    //Get text in each button
                    List<string> nodeTexts = new List<string>();
                    if (buttonNodes != null)
                    {
                        foreach (NodeData item in buttonNodes)
                        {
                            foreach (NodeData child in item.children)
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

        private void SetPosition(Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter.NodeData document, GameObject go)
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

        private string GetText(NodeData node)
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
                foreach (NodeData child in node.children)
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