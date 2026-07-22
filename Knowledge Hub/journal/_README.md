---
tags: [index, journal]
created: 2026-06-19
updated: 2026-06-19
---

# 日记目录

## 格式
每天一篇 `YYYY-MM-DD.md`，追加式写入。模板见 [[../templates/daily-journal]]。

## 内容规范
- **讨论**：用 `[[wikilink]]` 链接到具体笔记
- **决策**：重要决定记入 `decisions/` 后在此引用
- **修改的文件**：列出路径
- **不写**：长篇代码块、完整分析（提炼到领域笔记再链接）

## 最近日记
```dataview
TABLE tags, date
FROM "journal"
WHERE file.name != "_README"
SORT date DESC
LIMIT 15
```
