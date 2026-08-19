# 跨 MCP 元数据 Schema

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Cross-MCP-Metadata-Schema.md)

## 目的

此 Schema 让 WinForms 运行时控件对相互独立的源码、图谱与 IDE MCP 服务器有用，同时不让 WinForms MCP 对它们产生依赖。元数据是对 `winforms_get_source_mapping` 的补充；现有字段和必需参数保持不变。

```text
托管控件快照
  -> WinForms 源码映射
  -> 源码身份与精确位置
  -> CodeGraph 分析或 VS 导航/调试
  -> WinForms 运行时验证
```

RuntimeBridge 保持只读。没有任何 MCP 服务器会调用另一个 MCP 服务器。

## SourceIdentitySnapshot

`SourceIdentitySnapshot` 是可选的。它作为 `mapping.source` 出现在所属 Form 类型上，作为 `mapping.events[event].source` 出现在事件处理器上。它是一个交接记录，不是编译器符号键。

| 字段 | 含义 | 消费方指引 |
| --- | --- | --- |
| `project` | 发现的 `.csproj` 文件名主干 | 人类可读的项目标签 |
| `projectPath` | 发现的 `.csproj` 绝对路径 | VS MCP `build_project` 输入 |
| `sourceRoot` | 规范的有界扫描根目录 | 首选的 CodeGraph `projectPath` 提示 |
| `file` | 绝对源码文件 | 规范的 VS MCP 文档/调试器路径 |
| `projectRelativeFile` | 相对于 `sourceRoot` 的正斜杠路径 | CodeGraph `file` 提示 |
| `line`, `column`, `endLine`, `endColumn` | 从 1 开始的源码区间 | VS 导航；CodeGraph 消歧 |
| `namespace`, `type`, `fullyQualifiedType` | 所属类型元数据 | 搜索与显示上下文 |
| `member`, `memberKind`, `method` | 类型/成员身份 | 简单名称回退搜索 |
| `fullyQualifiedSymbol` | 可读的限定符号提示 | CodeGraph 发现，而非规范键 |
| `runtimeControlId`, `runtimeControlName`, `runtimeControlType` | 运行时到源码的链接 | 回到托管/UIA 检查 |

可空字段在未知时被省略。消费方不得因为某个可选字段缺失就推断出项目、源码根目录或符号不存在。

## 现有源码映射字段

现有的顶层属性仍然是兼容层：

- `control` 是托管运行时身份。
- `declaration`、`initialization` 与 `designer` 保留绝对 `file` 和从 1 开始的区间。它们可以增加可选的 `projectRelativeFile`。
- `namespace`、`type`、`fullyQualifiedType` 和 `codeBehindFile` 保留原有语义。
- `events[event]` 保留 `event`、`method`、`file`、`line` 与 `fullyQualifiedSymbol`。它可以增加精确的 `location` 与 `source` 对象。

旧客户端可以忽略所有新增字段，仍然通过原有字段进行导航。

## 示例

```json
{
  "control": {
    "managedId": "ctrl_18",
    "name": "btnUpgrade",
    "type": "AntdUI.Button"
  },
  "fullyQualifiedType": "NGUS2.UI.Forms.DeviceManagementForm",
  "source": {
    "project": "NGUS2.UI",
    "projectPath": "C:/repo/NGUS2.UI/NGUS2.UI.csproj",
    "sourceRoot": "C:/repo",
    "file": "C:/repo/NGUS2.UI/Forms/DeviceManagementForm.cs",
    "projectRelativeFile": "NGUS2.UI/Forms/DeviceManagementForm.cs",
    "line": 12,
    "column": 15,
    "namespace": "NGUS2.UI.Forms",
    "type": "DeviceManagementForm",
    "fullyQualifiedType": "NGUS2.UI.Forms.DeviceManagementForm",
    "member": "DeviceManagementForm",
    "memberKind": "type",
    "fullyQualifiedSymbol": "NGUS2.UI.Forms.DeviceManagementForm",
    "runtimeControlId": "ctrl_18",
    "runtimeControlName": "btnUpgrade",
    "runtimeControlType": "AntdUI.Button"
  },
  "events": {
    "Click": {
      "method": "BtnUpgrade_Click",
      "file": "C:/repo/NGUS2.UI/Forms/DeviceManagementForm.cs",
      "line": 823,
      "fullyQualifiedSymbol": "NGUS2.UI.Forms.DeviceManagementForm.BtnUpgrade_Click",
      "location": {
        "file": "C:/repo/NGUS2.UI/Forms/DeviceManagementForm.cs",
        "projectRelativeFile": "NGUS2.UI/Forms/DeviceManagementForm.cs",
        "line": 823,
        "column": 18,
        "endLine": 823,
        "endColumn": 66
      }
    }
  }
}
```

## 消费方规则

1. 对 VS MCP 优先使用绝对 `file` 加上从 1 开始的 `line` 与 `column`。
2. 对 CodeGraph 优先将 `fullyQualifiedSymbol`、`projectRelativeFile`、`line` 与 `sourceRoot` 一起使用。使用 `file` 来消除名称歧义。
3. 将 `fullyQualifiedSymbol` 视为提示。重载、生成代码、分部类型和第三方索引可能需要位置字段才能唯一解析。
4. 保留源码坐标基准。CodeGraph 内部列号不同；使用行号作为消歧依据，并让 CodeGraph 解析节点。
5. 绝不要通过此契约暴露 CodeGraph 节点 ID、VS 自动化对象、活的 `Control` 实例或任意反射值。
