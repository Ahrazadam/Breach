#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using BreachPuzzle;

[CustomEditor(typeof(PuzzleComponent))]
public sealed class PuzzleComponentEditor : Editor
{
    SerializedProperty nProp, targetRevealProp, hardModeProp, limitsProp, targetUIProp, rulesProp;
    SerializedProperty libraryProp, activeNodesProp;

    ReorderableList rulesList;
    ReorderableList activeNodesList;

    Vector2 rulesScroll;
    const float MaxViewport = 360f;
    const float MinEmpty = 14f;

    void OnEnable()
    {
        nProp = serializedObject.FindProperty("N");
        targetRevealProp = serializedObject.FindProperty("TargetReveal");
        hardModeProp = serializedObject.FindProperty("HardMode");
        limitsProp = serializedObject.FindProperty("Limits");
        targetUIProp = serializedObject.FindProperty("TargetUI");
        rulesProp = serializedObject.FindProperty("rules") ?? serializedObject.FindProperty("Rules");

        libraryProp = serializedObject.FindProperty("Library");
        activeNodesProp = serializedObject.FindProperty("ActiveNodes");

        BuildRulesList();
        BuildActiveNodesList();
    }

    void BuildRulesList()
    {
        if (rulesProp == null || !rulesProp.isArray) { rulesList = null; return; }

        rulesList = new ReorderableList(serializedObject, rulesProp, true, true, false, false);
        rulesList.headerHeight = 0f;
        rulesList.footerHeight = 0f;

        const float kHandleWidth = 18f;
        const float kGap = 6f;
        const float kPadTop = 3f;
        const float kPadBottom = 4f;
        const float kBetween = 2f;
        float kLine = EditorGUIUtility.singleLineHeight;

        rulesList.elementHeightCallback = i =>
        {
            var e = rulesProp.GetArrayElementAtIndex(i);
            float body = e.isExpanded ? GetChildrenHeight(e) : 0f;
            return kPadTop + kLine + (e.isExpanded ? kBetween + body : 0f) + kPadBottom;
        };

        rulesList.drawElementCallback = (rect, index, active, focused) =>
        {
            var e = rulesProp.GetArrayElementAtIndex(index);

            Rect header = new Rect(
                rect.x + kHandleWidth + kGap,
                rect.y + kPadTop,
                rect.width - (kHandleWidth + kGap),
                kLine
            );

            string nice = ShortType(e.managedReferenceFullTypename);
            e.isExpanded = EditorGUI.Foldout(header, e.isExpanded, string.IsNullOrEmpty(nice) ? "(Rule)" : nice, true);

            if (e.isExpanded)
            {
                float bodyH = GetChildrenHeight(e);
                Rect body = new Rect(header.x, header.yMax + kBetween, header.width, bodyH);
                EditorGUI.indentLevel++;
                DrawChildren(body, e);
                EditorGUI.indentLevel--;
            }
        };

        rulesList.onReorderCallback = _ => serializedObject.ApplyModifiedProperties();
    }

    void BuildActiveNodesList()
    {
        if (activeNodesProp == null || !activeNodesProp.isArray) { activeNodesList = null; return; }

        activeNodesList = new ReorderableList(serializedObject, activeNodesProp, true, true, true, true);
        activeNodesList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, $"Active Nodes ({activeNodesProp.arraySize})");
        };

        activeNodesList.elementHeightCallback = i =>
        {
            var e = activeNodesProp.GetArrayElementAtIndex(i);
            return EditorGUI.GetPropertyHeight(e, true) + 2f;
        };

        activeNodesList.drawElementCallback = (rect, index, active, focused) =>
        {
            var e = activeNodesProp.GetArrayElementAtIndex(index);
            rect.height = EditorGUI.GetPropertyHeight(e, true);
            EditorGUI.PropertyField(rect, e, GUIContent.none, true);
        };

        activeNodesList.onAddCallback = list =>
        {
            int i = activeNodesProp.arraySize;
            activeNodesProp.InsertArrayElementAtIndex(i);
            var e = activeNodesProp.GetArrayElementAtIndex(i);
            e.objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();

        if (rulesList == null) { rulesProp = serializedObject.FindProperty("rules") ?? serializedObject.FindProperty("Rules"); if (rulesProp != null && rulesProp.isArray) BuildRulesList(); }
        if (activeNodesList == null) { activeNodesProp = serializedObject.FindProperty("ActiveNodes"); if (activeNodesProp != null && activeNodesProp.isArray) BuildActiveNodesList(); }

        // Board
        if (nProp != null) EditorGUILayout.PropertyField(nProp);

        // Reveal
        if (targetRevealProp != null) EditorGUILayout.PropertyField(targetRevealProp);
        if (hardModeProp != null) EditorGUILayout.PropertyField(hardModeProp);
        EditorGUILayout.Space();

        // Limits
        if (limitsProp != null) EditorGUILayout.PropertyField(limitsProp, true);

        EditorGUILayout.Space();

        // Library & ActiveNodes
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Node Library", EditorStyles.boldLabel);
            if (libraryProp != null) EditorGUILayout.PropertyField(libraryProp);

            if (activeNodesList != null)
            {
                activeNodesList.DoLayoutList();
            }
            else if (activeNodesProp != null)
            {
                EditorGUILayout.PropertyField(activeNodesProp, true);
            }

            // Validasyon
            var lib = libraryProp?.objectReferenceValue as NodeLibrary;
            int count = activeNodesProp != null ? activeNodesProp.arraySize : 0;

            if (lib == null)
            {
                EditorGUILayout.HelpBox("Library atanmalı.", MessageType.Error);
            }
            else if (count <= 0)
            {
                EditorGUILayout.HelpBox("Active Nodes boş olamaz (en az 1).", MessageType.Error);
            }
            else if (count > 32)
            {
                EditorGUILayout.HelpBox("Active Nodes 32'yi aşmamalı.", MessageType.Error);
            }
            else
            {
                // Library üyesi olmayanları uyar
                bool bad = false;
                for (int i = 0; i < count; i++)
                {
                    var e = activeNodesProp.GetArrayElementAtIndex(i);
                    var node = e.objectReferenceValue as GridNode;
                    if (node != null && (lib.Nodes == null || !lib.Nodes.Contains(node)))
                    {
                        bad = true; break;
                    }
                }
                if (bad)
                {
                    EditorGUILayout.HelpBox("Active Nodes içindeki bazı öğeler seçili Library’de yok.", MessageType.Warning);
                }
            }
        }

        // Rules bloğu
        EditorGUILayout.Space();
        if (rulesList == null)
        {
            EditorGUILayout.HelpBox("rules listesi bulunamadı. [SerializeReference] List<RuleBase> rules gerekli.", MessageType.Error);
        }
        else
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Rules ({rulesProp.arraySize})", EditorStyles.boldLabel);
                float content = 0;
                for (int i = 0; i < rulesList.count; i++) content += rulesList.elementHeightCallback(i) + 2f;

                if (rulesList.count == 0)
                {
                    var area = GUILayoutUtility.GetRect(0, MinEmpty, GUILayout.ExpandWidth(true));
                    rulesList.DoList(new Rect(area.x, area.y, area.width, MinEmpty));
                }
                else if (content > MaxViewport)
                {
                    var area = GUILayoutUtility.GetRect(0, MaxViewport, GUILayout.ExpandWidth(true));
                    var view = new Rect(area.x, area.y, area.width, MaxViewport);
                    var full = new Rect(0, 0, area.width - 16, content);
                    rulesScroll = GUI.BeginScrollView(view, rulesScroll, full);
                    rulesList.DoList(full);
                    GUI.EndScrollView();
                }
                else
                {
                    rulesList.DoLayoutList();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("−", GUILayout.Width(24)))
                    {
                        if (rulesList.index >= 0 && rulesList.index < rulesProp.arraySize)
                        {
                            rulesProp.DeleteArrayElementAtIndex(rulesList.index);
                            serializedObject.ApplyModifiedProperties();
                            Repaint();
                        }
                    }

                    if (GUILayout.Button("+", GUILayout.Width(24)))
                    {
                        ShowRuleAddMenu();
                    }
                }
            }
        }

        // UI
        if (targetUIProp != null) EditorGUILayout.PropertyField(targetUIProp);

        // Actions
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate", GUILayout.Height(26)))
            {
                serializedObject.ApplyModifiedProperties();
                ((PuzzleComponent)target).GeneratePuzzle();
            }
            if (GUILayout.Button("Show", GUILayout.Height(26)))
            {
                serializedObject.ApplyModifiedProperties();
                ((PuzzleComponent)target).ShowOnUI();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ---- helpers ----

    void ShowRuleAddMenu()
    {
        var menu = new GenericMenu();
        var types = TypeCache.GetTypesDerivedFrom<RuleBase>()
                             .Where(t => t.IsClass && !t.IsAbstract)
                             .OrderBy(t => t.FullName);

        int c = 0;
        foreach (var t in types)
        {
            c++;
            menu.AddItem(new GUIContent(t.FullName), false, () =>
            {
                int idx = rulesProp.arraySize;
                rulesProp.InsertArrayElementAtIndex(idx);
                var e = rulesProp.GetArrayElementAtIndex(idx);
                e.managedReferenceValue = Activator.CreateInstance(t);
                serializedObject.ApplyModifiedProperties();
                rulesList.index = idx;
                Repaint();
            });
        }
        if (c == 0) { menu.AddDisabledItem(new GUIContent("No Rule types found")); }

        menu.ShowAsContext();
    }

    static void DrawChildren(Rect rect, SerializedProperty root)
    {
        var p = root.Copy();
        var end = p.GetEndProperty();
        bool enter = true;
        p.NextVisible(enter);
        float y = rect.y;
        while (!SerializedProperty.EqualContents(p, end))
        {
            float h = EditorGUI.GetPropertyHeight(p, true);
            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), p, true);
            y += h + 2f;
            if (!p.NextVisible(false)) break;
        }
    }

    static float GetChildrenHeight(SerializedProperty root)
    {
        var p = root.Copy();
        var end = p.GetEndProperty();
        float sum = 0f;
        p.NextVisible(true);
        while (!SerializedProperty.EqualContents(p, end))
        {
            sum += EditorGUI.GetPropertyHeight(p, true) + 2f;
            if (!p.NextVisible(false)) break;
        }
        return Mathf.Max(0f, sum - 2f);
    }

    static string ShortType(string managedRefFullTypename)
    {
        if (string.IsNullOrEmpty(managedRefFullTypename)) return "(empty)";
        int sp = managedRefFullTypename.IndexOf(' ');
        string full = sp >= 0 ? managedRefFullTypename[(sp + 1)..] : managedRefFullTypename;
        int dot = full.LastIndexOf('.');
        return dot >= 0 ? full[(dot + 1)..] : full;
    }
}
#endif
