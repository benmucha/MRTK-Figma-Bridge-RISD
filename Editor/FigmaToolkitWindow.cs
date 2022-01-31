// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter
{
    /// <summary>
    /// Partial class. Editor UI window, simplifies interacting with the Figma Bridge
    /// </summary>
    /// <remarks>HAD TO RENAME THIS WITH THE "A" AT THE END BC THE WINDOW WASNT SHOWING WITH THE ORIGINAL NAME - im guessing this is due to reflection bs but we can worry abt renaming later</remarks>
    public partial class FigmaToolkitWindowA : EditorWindow
    {
        private FigmaToolkitManager toolkitManager;
        private SerializedObject settings;
        private SerializedObject data;

        private VisualElement panelContainer;
        private ToolbarBreadcrumbs breadcrumbs;
        private Stack<string> breadcrumbItems = new Stack<string>();

        //[MenuItem("Mixed Reality/Toolkit/MRTK Figma Bridge (RISD)/Figma Bridge Window")]
        [MenuItem("RISD/MRTK Figma/Import")]
        public static void ShowWindow()
        {
            Debug.Log("SHOW WINDOW");
            //FigmaToolkitWindow wnd = GetWindow<FigmaToolkitWindow>();
            FigmaToolkitWindowA wnd = GetWindow<FigmaToolkitWindowA>();
            wnd.titleContent = new GUIContent("MRTK Figma Bridge (RISD)");
            wnd.minSize = new UnityEngine.Vector2(350, 440);
            Debug.Log("SHOWED: " + wnd.position);
        }

        public void CreateGUI()
        {
            Debug.Log("CreateGUIs: " + FigmaSettings.FigmaStylesheetPath);
            
            toolkitManager = new FigmaToolkitManager(FigmaSettings.EditorGetOrCreateSettings(), FigmaToolkitData.EditorGetOrCreateData());
            settings = new SerializedObject(toolkitManager.settings);
            data = new SerializedObject(toolkitManager.data);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(FigmaSettings.FigmaStylesheetPath);
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(styleSheet);
            root.style.flexGrow = 1;

            panelContainer = new VisualElement();
            panelContainer.AddToClassList("panel-container");

            var toolbar = new Toolbar();
            breadcrumbs = new ToolbarBreadcrumbs();
            toolbar.Add(breadcrumbs);

            root.Add(toolbar);
            root.Add(panelContainer);

            ShowPanel(BuildHomePanel(settings));
            
            Debug.Log("CreateGUIf: " + styleSheet + " - " + FigmaSettings.FigmaStylesheetPath);
            
        }

        private VisualElement BuildHomePanel(SerializedObject settings)
        {
            var panel = new VisualElement();
            panel.name = "Home";
            panel.style.paddingLeft = 15;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 2;
            panel.style.flexGrow = 1;

            var header = new Label("MRTK Figma Importer (RISD)");
            header.AddToClassList("heading");

            var subheader = new Label("To get started, please enter your Figma Token below");
            var accessTokenButton = new ToolbarButton(() => { Application.OpenURL("https://www.figma.com/developers/api#access-tokens"); });
            accessTokenButton.text = "Click here for help generating access tokens";
            var figmaTokenField = new TextField("Figma Token");
            figmaTokenField.labelElement.style.minWidth = StyleKeyword.Auto;
            figmaTokenField.labelElement.style.paddingRight = 10f;
            figmaTokenField.BindProperty(settings.FindProperty("FigmaToken"));
            var filesButton = new Button(() =>
            {
                if (string.IsNullOrEmpty(figmaTokenField.text))
                {
                    Debug.LogError("Figma Token missing");
                }
                else
                {
                    ShowPanel(filesPanel);
                }
            })
            { text = "Open Files" };

            panel.Add(header);
            panel.Add(subheader);
            panel.Add(figmaTokenField);
            panel.Add(accessTokenButton);
            panel.Add(filesButton);

            return panel;
        }

        private void ShowPanel(VisualElement visualElement)
        {
            panelContainer.Clear();
            panelContainer.Add(visualElement);
            Debug.Log("ShowPanel");
            BreadcrumbPush(visualElement);
        }

        private void BreadcrumbClick(VisualElement visualElement)
        {
            while (visualElement.name != breadcrumbItems.Peek())
            {
                BreadcrumbPop();
            }

            BreadcrumbPop();
            ShowPanel(visualElement);
        }

        private void BreadcrumbPush(VisualElement visualElement)
        {
            breadcrumbItems.Push(visualElement.name);
            breadcrumbs.PushItem(visualElement.name, () =>
            {
                BreadcrumbClick(visualElement);
            });
        }

        private void BreadcrumbPop()
        {
            breadcrumbs.PopItem();
            breadcrumbItems.Pop();
        }
    }
}