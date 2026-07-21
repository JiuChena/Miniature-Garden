using System;
using System.Collections.Generic;
using BehaviorCore;
using UnityEditor;
using UnityEngine;

internal sealed class BehaviorAuthoringHelperWindow : EditorWindow
{
    private static readonly string[] CoreBehaviorKeys =
    {
        BehaviorKeys.Idle,
        BehaviorKeys.CrouchIdle,
        BehaviorKeys.Move,
        BehaviorKeys.MoveJump,
        BehaviorKeys.AttackStart,
        BehaviorKeys.AttackLoop,
        BehaviorKeys.AttackEnd,
        BehaviorKeys.CrouchAttackStart,
        BehaviorKeys.CrouchAttackLoop,
        BehaviorKeys.CrouchAttackEnd,
        BehaviorKeys.Reload,
        BehaviorKeys.CrouchReload,
        BehaviorKeys.Talent,
        BehaviorKeys.Burst,
        BehaviorKeys.Death,
    };

    private UnitAssetInformation selectedUnit;
    private BehaviorClip selectedBehaviorClip;
    private Vector2 behaviorScroll;
    private Vector2 numericScroll;
    private Vector2 validationScroll;

    [MenuItem("MiniatureGarden/Behavior/Authoring Helper")]
    private static void Open()
    {
        GetWindow<BehaviorAuthoringHelperWindow>("Behavior Authoring");
    }

    private void OnEnable()
    {
        TryAssignFromSelection();
        Selection.selectionChanged += TryAssignFromSelection;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= TryAssignFromSelection;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Behavior 作者辅助工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("用于检查 Unit 配置、补齐核心行为 key，并浏览行为表与数值 key。", MessageType.Info);

        selectedUnit = (UnitAssetInformation)EditorGUILayout.ObjectField(
            "Unit Config", selectedUnit, typeof(UnitAssetInformation), false);
        selectedBehaviorClip = (BehaviorClip)EditorGUILayout.ObjectField(
            "Behavior Clip", selectedBehaviorClip, typeof(BehaviorClip), false);

        EditorGUILayout.Space(6f);
        DrawToolbar();
        EditorGUILayout.Space(6f);

        if (selectedUnit == null && selectedBehaviorClip == null)
        {
            EditorGUILayout.HelpBox("请选择 UnitAssetInformation 或 BehaviorClip。", MessageType.None);
            return;
        }

        if (selectedUnit != null)
        {
            DrawBehaviorPanel();
            EditorGUILayout.Space(6f);
            DrawNumericPanel();
        }

        if (selectedBehaviorClip != null)
        {
            EditorGUILayout.Space(6f);
            DrawBehaviorClipValidationPanel();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedUnit == null))
            {
                if (GUILayout.Button("补齐核心行为 key", GUILayout.Height(24f)))
                    EnsureCoreBehaviorKeys();

                if (GUILayout.Button("校验 Unit", GUILayout.Height(24f)))
                    ValidateSelectedUnit();
            }

            using (new EditorGUI.DisabledScope(selectedBehaviorClip == null))
            {
                if (GUILayout.Button("校验 BehaviorClip", GUILayout.Height(24f)))
                    ValidateSelectedBehaviorClip();
            }
        }
    }

    private void DrawBehaviorPanel()
    {
        EditorGUILayout.LabelField("Unit 行为表", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            behaviorScroll = EditorGUILayout.BeginScrollView(behaviorScroll, GUILayout.Height(220f));
            for (int i = 0; i < CoreBehaviorKeys.Length; i++)
                DrawCoreBehaviorRow(CoreBehaviorKeys[i]);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("当前行为条目", EditorStyles.miniBoldLabel);
            BehaviorEntry[] entries = selectedUnit.behaviors ?? Array.Empty<BehaviorEntry>();
            if (entries.Length == 0)
            {
                EditorGUILayout.HelpBox("当前 Unit 行为表为空。", MessageType.Warning);
            }
            else
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    BehaviorEntry entry = entries[i];
                    if (entry == null)
                    {
                        EditorGUILayout.LabelField($"[{i}] <Null>");
                        continue;
                    }

                    int clipCount = entry.clips != null ? entry.clips.Length : 0;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"[{i}] {entry.key}", GUILayout.MinWidth(220f));
                        EditorGUILayout.LabelField($"Clips={clipCount}", GUILayout.Width(80f));
                        if (GUILayout.Button("复制", GUILayout.Width(52f)))
                            EditorGUIUtility.systemCopyBuffer = entry.key;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawCoreBehaviorRow(string behaviorKey)
    {
        bool exists = selectedUnit.ContainsBehaviorEntry(behaviorKey);
        BehaviorClip[] group = selectedUnit.GetBehaviorGroup(behaviorKey);
        bool hasClip = group != null && group.Length > 0;
        string state = !exists ? "未建条目" : hasClip ? $"已配置 {group.Length} 个 Clip" : "已建条目，未配置 Clip";

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(behaviorKey, GUILayout.MinWidth(180f));
            EditorGUILayout.LabelField(state, GUILayout.MinWidth(150f));
            if (!exists && GUILayout.Button("添加", GUILayout.Width(52f)))
                AddBehaviorKey(behaviorKey);
            if (GUILayout.Button("复制", GUILayout.Width(52f)))
                EditorGUIUtility.systemCopyBuffer = behaviorKey;
        }
    }

    private void DrawNumericPanel()
    {
        EditorGUILayout.LabelField("Unit 数值 key", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (selectedUnit.numericProfile == null)
            {
                EditorGUILayout.HelpBox("当前 Unit 未绑定 UnitAbilityNumericProfile。", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("选中 Numeric Profile", GUILayout.Height(22f)))
                Selection.activeObject = selectedUnit.numericProfile;

            UnitAbilityNumericEntry[] entries = selectedUnit.numericProfile.entries ?? Array.Empty<UnitAbilityNumericEntry>();
            numericScroll = EditorGUILayout.BeginScrollView(numericScroll, GUILayout.Height(180f));
            if (entries.Length == 0)
            {
                EditorGUILayout.HelpBox("当前 Numeric Profile 没有任何条目。", MessageType.Warning);
            }
            else
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    UnitAbilityNumericEntry entry = entries[i];
                    if (entry == null)
                    {
                        EditorGUILayout.LabelField($"[{i}] <Null>");
                        continue;
                    }

                    int valueCount = entry.levelValues != null ? entry.levelValues.Length : 0;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(entry.key, GUILayout.MinWidth(220f));
                        EditorGUILayout.LabelField(entry.levelGroup.ToString(), GUILayout.Width(120f));
                        EditorGUILayout.LabelField($"Lv={valueCount}", GUILayout.Width(60f));
                        if (GUILayout.Button("复制", GUILayout.Width(52f)))
                            EditorGUIUtility.systemCopyBuffer = entry.key;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawBehaviorClipValidationPanel()
    {
        EditorGUILayout.LabelField("BehaviorClip 校验", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            List<string> issues = new List<string>();
            selectedBehaviorClip.CollectValidationIssues(issues);
            validationScroll = EditorGUILayout.BeginScrollView(validationScroll, GUILayout.Height(120f));
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("当前 BehaviorClip 未发现配置问题。", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < issues.Count; i++)
                    EditorGUILayout.HelpBox(issues[i], MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void EnsureCoreBehaviorKeys()
    {
        if (selectedUnit == null)
            return;

        int addedCount = 0;
        for (int i = 0; i < CoreBehaviorKeys.Length; i++)
        {
            if (selectedUnit.EnsureBehaviorEntry(CoreBehaviorKeys[i]))
                addedCount++;
        }

        EditorUtility.SetDirty(selectedUnit);
        AssetDatabase.SaveAssets();
        Debug.Log($"[{selectedUnit.name}] 已补齐 {addedCount} 个核心行为 key 条目。", selectedUnit);
    }

    private void AddBehaviorKey(string behaviorKey)
    {
        if (selectedUnit == null || string.IsNullOrWhiteSpace(behaviorKey))
            return;

        if (!selectedUnit.EnsureBehaviorEntry(behaviorKey))
            return;

        EditorUtility.SetDirty(selectedUnit);
        AssetDatabase.SaveAssets();
    }

    private void ValidateSelectedUnit()
    {
        if (selectedUnit == null)
            return;

        bool valid = selectedUnit.ValidateData();
        if (valid)
            Debug.Log($"[{selectedUnit.name}] Unit 配置校验通过。", selectedUnit);
    }

    private void ValidateSelectedBehaviorClip()
    {
        if (selectedBehaviorClip == null)
            return;

        List<string> issues = new List<string>();
        selectedBehaviorClip.CollectValidationIssues(issues);
        if (issues.Count == 0)
        {
            Debug.Log($"[{selectedBehaviorClip.name}] BehaviorClip 校验通过。", selectedBehaviorClip);
            return;
        }

        for (int i = 0; i < issues.Count; i++)
            Debug.LogWarning($"[{selectedBehaviorClip.name}] {issues[i]}", selectedBehaviorClip);
    }

    private void TryAssignFromSelection()
    {
        UnityEngine.Object activeObject = Selection.activeObject;
        if (activeObject is UnitAssetInformation unitAssetInformation)
            selectedUnit = unitAssetInformation;
        else if (activeObject is BehaviorClip behaviorClip)
            selectedBehaviorClip = behaviorClip;

        Repaint();
    }
}
