# CodeGraph MCP 契约分析

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](CodeGraph-MCP-Contract-Analysis.md)

## 范围

本文档记录本地 CodeGraph 参考仓库在提交 `c6aaa20` 处可观测的 MCP 契约。它只是一份集成分析。WinForms MCP 不会添加 CodeGraph 客户端、传输、包引用或跨进程调用。

CodeGraph 工具是对预构建的本地图谱进行的只读查询。它们可以通过 `projectPath` 查询默认项目或其他已建立索引的项目。

## 相关工具契约

| 工具 | 必需的交接字段 | 有用的可选交接字段 | 用途 |
| --- | --- | --- | --- |
| `codegraph_search` | `query` | `kind`, `limit`, `projectPath` | 按名称或部分名称定位符号 |
| `codegraph_callers` | `symbol` | `file`, `limit`, `projectPath` | 发现调用者 |
| `codegraph_callees` | `symbol` | `file`, `limit`, `projectPath` | 发现下游调用 |
| `codegraph_impact` | `symbol` | `file`, `depth`, `projectPath` | 受限的影响分析 |
| `codegraph_node` | 无 | `symbol`, `file`, `line`, `includeCode`, `projectPath` | 解析精确的节点或源码上下文 |
| `codegraph_explore` | `query` | `maxFiles`, `projectPath` | 更广泛的源码与图谱探索 |
| `codegraph_status` / `codegraph_files` | 无 | `projectPath` | 检查索引状态与已索引文件 |

所有查询工具都接受一个 `projectPath`，它可以是项目根目录或其下层的某个目录。CodeGraph 会解析该路径上方最近的 `.codegraph` 索引。这样客户端就可以针对已建立索引的子项目，而无需让服务器相互耦合。

## 符号与文件解析

CodeGraph 以模糊方式解析名称，包括限定后缀。FQN 是高质量的发现提示，但不是由编译器支持、全局唯一的键。`file` 参数用于消除同名成员的歧义，`line` 则进一步把 `codegraph_node` 锚定到具体的声明。

CodeGraph 内部存储的是项目相对的正斜杠路径。其工具契约接受路径或后缀，因此 WinForms MCP 同时暴露两者：

- 现有的绝对 `file` 值对编辑器/调试器使用仍然权威。
- 可选的 `projectRelativeFile` 便于作为 CodeGraph 的 `file` 提示。
- 可选的 `sourceRoot` 是首选的初始 `projectPath` 提示。CodeGraph 可以向上走到实际的 `.codegraph` 根目录。

## 推荐交接

使用完整的元组，而不是仅使用 FQN：

```json
{
  "symbol": "NGUS2.UI.Forms.DeviceManagementForm.BtnUpgrade_Click",
  "file": "Forms/DeviceManagementForm.cs",
  "line": 823,
  "projectPath": "C:/repo"
}
```

稳健的顺序是：

1. 使用 `codegraph_node(symbol, file, line, projectPath)` 验证解析结果。
2. 使用 `codegraph_callers`、`codegraph_callees` 或 `codegraph_impact`，并带上相同的 symbol、file 与项目上下文。
3. 回到 VS MCP 进行编辑/构建/调试，回到 WinForms MCP 进行验证。

未建立索引的路径不是 WinForms 映射失败。CodeGraph 可以报告如何传入有效的 `projectPath` 或初始化索引。

## 边界

WinForms MCP 只输出元数据。它从不触发 CodeGraph 的索引、同步或变更，也不会把 CodeGraph 的内部节点 ID 序列化进其协议。
