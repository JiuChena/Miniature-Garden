using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitAssetInformation))]
public class UnitAssetInformationEditor : Editor
{
    private bool _identityFoldout = true;
    private bool _statsFoldout = true;
    private bool _statsPrimaryFoldout = true;
    private bool _statsAdvancedFoldout = true;
    private bool _movementFoldout = true;
    private bool _combatFoldout = true;
    private bool _combatConfigFoldout = true;
    private bool _combatIndicatorsFoldout = true;
    private bool _capabilitiesFoldout = true;
    private bool _uiFoldout = true;
    private bool _uiIdentityFoldout = true;
    private bool _uiIconsFoldout = true;
    private bool _numericFoldout = true;
    private bool _strategiesFoldout = true;
    private bool _behaviorsFoldout = true;

    private SerializedProperty _characterIdProperty;
    private SerializedProperty _unitAlignmentProperty;
    private SerializedProperty _healthFormulaProperty;
    private SerializedProperty _attackFormulaProperty;
    private SerializedProperty _defenseFormulaProperty;
    private SerializedProperty _baseCritRateProperty;
    private SerializedProperty _baseCritDamageProperty;
    private SerializedProperty _baseDamageBonusProperty;
    private SerializedProperty _basePenetrationProperty;
    private SerializedProperty _maxEnergyProperty;
    private SerializedProperty _moveSpeedProperty;
    private SerializedProperty _jumpPowerProperty;
    private SerializedProperty _defaultBehaviorTransitionDurationProperty;
    private SerializedProperty _burstCostProperty;
    private SerializedProperty _talentCooldownProperty;
    private SerializedProperty _burstCooldownProperty;
    private SerializedProperty _talentIndicatorProperty;
    private SerializedProperty _burstIndicatorProperty;
    private SerializedProperty _supportsAttackProperty;
    private SerializedProperty _supportsTalentProperty;
    private SerializedProperty _supportsBurstProperty;
    private SerializedProperty _supportsReloadProperty;
    private SerializedProperty _supportsCrouchProperty;
    private SerializedProperty _supportsJumpProperty;
    private SerializedProperty _hitboxTargetLayersProperty;
    private SerializedProperty _displayNameProperty;
    private SerializedProperty _portraitIconProperty;
    private SerializedProperty _teamIconProperty;
    private SerializedProperty _talentIconProperty;
    private SerializedProperty _burstIconProperty;
    private SerializedProperty _weaponIconProperty;
    private SerializedProperty _numericProfileProperty;
    private SerializedProperty _conditionSourceAssetProperty;
    private SerializedProperty _transitionPolicyAssetProperty;
    private SerializedProperty _attackResolverAssetProperty;
    private SerializedProperty _behaviorsProperty;

    private void OnEnable()
    {
        _characterIdProperty = serializedObject.FindProperty("characterId");
        _unitAlignmentProperty = serializedObject.FindProperty("unitAlignment");
        _healthFormulaProperty = serializedObject.FindProperty("healthFormula");
        _attackFormulaProperty = serializedObject.FindProperty("attackFormula");
        _defenseFormulaProperty = serializedObject.FindProperty("defenseFormula");
        _baseCritRateProperty = serializedObject.FindProperty("baseCritRate");
        _baseCritDamageProperty = serializedObject.FindProperty("baseCritDamage");
        _baseDamageBonusProperty = serializedObject.FindProperty("baseDamageBonus");
        _basePenetrationProperty = serializedObject.FindProperty("basePenetration");
        _maxEnergyProperty = serializedObject.FindProperty("maxEnergy");
        _moveSpeedProperty = serializedObject.FindProperty("moveSpeed");
        _jumpPowerProperty = serializedObject.FindProperty("jumpPower");
        _defaultBehaviorTransitionDurationProperty = serializedObject.FindProperty("defaultBehaviorTransitionDuration");
        _burstCostProperty = serializedObject.FindProperty("burstCost");
        _talentCooldownProperty = serializedObject.FindProperty("talentCooldown");
        _burstCooldownProperty = serializedObject.FindProperty("burstCooldown");
        _talentIndicatorProperty = serializedObject.FindProperty("talentIndicator");
        _burstIndicatorProperty = serializedObject.FindProperty("burstIndicator");
        _supportsAttackProperty = serializedObject.FindProperty("supportsAttack");
        _supportsTalentProperty = serializedObject.FindProperty("supportsTalent");
        _supportsBurstProperty = serializedObject.FindProperty("supportsBurst");
        _supportsReloadProperty = serializedObject.FindProperty("supportsReload");
        _supportsCrouchProperty = serializedObject.FindProperty("supportsCrouch");
        _supportsJumpProperty = serializedObject.FindProperty("supportsJump");
        _hitboxTargetLayersProperty = serializedObject.FindProperty("hitboxTargetLayers");
        _displayNameProperty = serializedObject.FindProperty("displayName");
        _portraitIconProperty = serializedObject.FindProperty("portraitIcon");
        _teamIconProperty = serializedObject.FindProperty("teamIcon");
        _talentIconProperty = serializedObject.FindProperty("talentIcon");
        _burstIconProperty = serializedObject.FindProperty("burstIcon");
        _weaponIconProperty = serializedObject.FindProperty("weaponIcon");
        _numericProfileProperty = serializedObject.FindProperty("numericProfile");
        _conditionSourceAssetProperty = serializedObject.FindProperty("conditionSourceAsset");
        _transitionPolicyAssetProperty = serializedObject.FindProperty("transitionPolicyAsset");
        _attackResolverAssetProperty = serializedObject.FindProperty("attackResolverAsset");
        _behaviorsProperty = serializedObject.FindProperty("behaviors");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawScriptReference();
        DrawIdentitySection();
        DrawStatsSection();
        DrawMovementSection();
        DrawCombatSection();
        DrawUISection();
        DrawNumericSection();
        DrawStrategiesSection();
        DrawBehaviorsSection();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptReference()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromScriptableObject((UnitAssetInformation)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }

    private void DrawIdentitySection()
    {
        _identityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_identityFoldout, "Identity");
        if (_identityFoldout)
        {
            EditorGUILayout.PropertyField(_characterIdProperty);
            EditorGUILayout.PropertyField(_unitAlignmentProperty);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawStatsSection()
    {
        _statsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_statsFoldout, "Stats");
        if (_statsFoldout)
        {
            DrawNestedFoldout(ref _statsPrimaryFoldout, "Primary Stats");
            if (_statsPrimaryFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_healthFormulaProperty, true);
                EditorGUILayout.PropertyField(_attackFormulaProperty, true);
                EditorGUILayout.PropertyField(_defenseFormulaProperty, true);
                EditorGUI.indentLevel--;
            }

            DrawNestedFoldout(ref _statsAdvancedFoldout, "Advanced Stats");
            if (_statsAdvancedFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_baseCritRateProperty);
                EditorGUILayout.PropertyField(_baseCritDamageProperty);
                EditorGUILayout.PropertyField(_baseDamageBonusProperty);
                EditorGUILayout.PropertyField(_basePenetrationProperty);
                EditorGUILayout.PropertyField(_maxEnergyProperty);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawMovementSection()
    {
        _movementFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_movementFoldout, "Movement");
        if (_movementFoldout)
        {
            EditorGUILayout.PropertyField(_moveSpeedProperty);
            EditorGUILayout.PropertyField(_jumpPowerProperty);
            EditorGUILayout.PropertyField(_defaultBehaviorTransitionDurationProperty);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawCombatSection()
    {
        _combatFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_combatFoldout, "Combat");
        if (_combatFoldout)
        {
            DrawNestedFoldout(ref _combatConfigFoldout, "Skill Config");
            if (_combatConfigFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_burstCostProperty);
                EditorGUILayout.PropertyField(_talentCooldownProperty);
                EditorGUILayout.PropertyField(_burstCooldownProperty);
                EditorGUI.indentLevel--;
            }

            DrawNestedFoldout(ref _combatIndicatorsFoldout, "Skill Indicators");
            if (_combatIndicatorsFoldout)
            {
                EditorGUI.indentLevel++;
                if (_supportsTalentProperty != null && _supportsTalentProperty.boolValue && _talentIndicatorProperty != null)
                    EditorGUILayout.PropertyField(_talentIndicatorProperty, new GUIContent("Talent Indicator"), true);
                if (_supportsBurstProperty != null && _supportsBurstProperty.boolValue && _burstIndicatorProperty != null)
                    EditorGUILayout.PropertyField(_burstIndicatorProperty, new GUIContent("Burst Indicator"), true);
                EditorGUI.indentLevel--;
            }

            DrawNestedFoldout(ref _capabilitiesFoldout, "Capabilities");
            if (_capabilitiesFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_supportsAttackProperty);
                EditorGUILayout.PropertyField(_supportsTalentProperty);
                EditorGUILayout.PropertyField(_supportsBurstProperty);
                EditorGUILayout.PropertyField(_supportsReloadProperty);
                EditorGUILayout.PropertyField(_supportsCrouchProperty);
                EditorGUILayout.PropertyField(_supportsJumpProperty);
                EditorGUILayout.PropertyField(_hitboxTargetLayersProperty);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawUISection()
    {
        _uiFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_uiFoldout, "UI");
        if (_uiFoldout)
        {
            DrawNestedFoldout(ref _uiIdentityFoldout, "Display");
            if (_uiIdentityFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_displayNameProperty);
                EditorGUI.indentLevel--;
            }

            DrawNestedFoldout(ref _uiIconsFoldout, "Icons");
            if (_uiIconsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_portraitIconProperty);
                EditorGUILayout.PropertyField(_teamIconProperty);
                EditorGUILayout.PropertyField(_talentIconProperty);
                EditorGUILayout.PropertyField(_burstIconProperty);
                EditorGUILayout.PropertyField(_weaponIconProperty);
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawNumericSection()
    {
        _numericFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_numericFoldout, "Numeric");
        if (_numericFoldout)
            EditorGUILayout.PropertyField(_numericProfileProperty);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawStrategiesSection()
    {
        _strategiesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_strategiesFoldout, "Strategies");
        if (_strategiesFoldout)
        {
            EditorGUILayout.PropertyField(_conditionSourceAssetProperty);
            EditorGUILayout.PropertyField(_transitionPolicyAssetProperty);
            EditorGUILayout.PropertyField(_attackResolverAssetProperty);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBehaviorsSection()
    {
        _behaviorsFoldout = EditorGUILayout.Foldout(_behaviorsFoldout, "Behaviors", true);
        if (_behaviorsFoldout)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_behaviorsProperty, true);
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Validate Behavior Table"))
                ((UnitAssetInformation)target).ValidateData();
            EditorGUI.indentLevel--;
        }
    }

    private static void DrawNestedFoldout(ref bool foldout, string label)
    {
        foldout = EditorGUILayout.Foldout(foldout, label, true);
    }
}
