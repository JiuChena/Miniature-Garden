---
tags: [convention, naming]
created: 2026-06-19
updated: 2026-06-24
---

# 命名规范

## 类名
- PascalCase，无拼写错误
- ✓ UnitAssetInformation / ✗ CharacterAssetInforamtion

## 字段
- 不与 Unity 基类成员冲突
- ✗ `public string name;` → ✓ `public string displayName;`
- ✗ `public string modle;` → ✓ `public string model;`

## 枚举
- 单独放 Enums/ 文件夹，不定义在具体类内部

## 命名空间
- `CoreFramework` — 基础框架层
- `BehaviorCore` — 技能系统（BehaviorClip、BehaviorInterpreter、Hitbox）
- 其余模块在对应文件夹下

## 字符串标识
- 统一用 `assetID`、`displayName`，不用 `name`

## 文件组织
- 每个 Module 在 `Scripts/C#/领域/` 下独立文件夹
- Editor 脚本在 `Editor/领域/` 下
- ScriptableObject 配置在 `ScriptableObjects/`
