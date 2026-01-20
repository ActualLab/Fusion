---
allowed-tools: Read, Edit, Write, Glob, Grep
description: Mermaid diagram syntax guidelines for Fusion docs
argument-hint: [task-description]
---

# Mermaid Diagram Syntax Guidelines

Use this when creating or editing Mermaid diagrams in documentation files.

## Quoting Rules

### Edge Labels with Special Characters

Edge labels containing `()`, `[]`, `<>`, or `<br/>` **must be quoted**:

```mermaid
flowchart LR
    A --> B                          %% OK - no special chars
    A -->|yes| B                     %% OK - no special chars
    A -->|"Set(value)"| B            %% REQUIRED - contains ()
    A -->|"uses [Interface]"| B      %% REQUIRED - contains []
    A -->|"line1<br/>line2"| B       %% REQUIRED - contains <br/>
```

**Wrong:**
```mermaid
flowchart LR
    A -->|Set(value)| B              %% PARSE ERROR!
```

### Node Labels

Parentheses inside `["..."]` node labels are **fine** and don't need escaping:

```mermaid
flowchart LR
    A["Call: Get('a')"]              %% OK - inside ["..."]
    B["Task.FromResult(default)"]    %% OK - inside ["..."]
```

### Escaping Quotes

To include quote characters inside quoted labels, use `#quot;`:

```mermaid
flowchart LR
    A["This has #quot;quoted#quot; text"]
```

Renders as: `This has "quoted" text`

## Layout Preferences

### Prefer Horizontal (LR) Layout

Use `flowchart LR` (left-to-right) by default. It's more compact and readable for most diagrams:

```mermaid
flowchart LR
    A --> B --> C --> D
```

### Subgraph Direction

For flowcharts with subgraphs:
- `flowchart LR/TB` controls **subgraph placement** (how subgraphs are arranged relative to each other)
- `direction LR/TB` inside subgraph controls **internal flow** (how nodes inside are arranged)

```mermaid
flowchart LR
    subgraph GroupA["First Group"]
        direction LR
        A1 --> A2 --> A3
    end
    subgraph GroupB["Second Group"]
        direction LR
        B1 --> B2 --> B3
    end
```

### State Diagrams

Use `direction LR` for horizontal layout:

```mermaid
stateDiagram-v2
    direction LR
    [*] --> StateA
    StateA --> StateB
```

## Class Diagrams

### Generic Type Names

Use separate IDs and display labels to avoid `<` `>` conflicts:

```mermaid
classDiagram
    direction LR
    IState_T <|-- State_T
    State_T <|-- MutableState_T

    class IState_T["IState&lt;T&gt;"] {
        <<interface>>
    }
    class State_T["State&lt;T&gt;"]
    class MutableState_T["MutableState&lt;T&gt;"]
```

## When to Use Tables Instead

Replace diagrams with tables when:
- The diagram shows simple mappings (call -> returns)
- The diagram is essentially a list with descriptions
- A table would be more readable and scannable

**Example - replace this diagram:**
```mermaid
flowchart TD
    Call1["await counters.Get('a')"] --> Returns1["int (value only)"]
    Call2["await Computed.Capture(...)"] --> Returns2["Computed&lt;int&gt;"]
```

**With this table:**
| Call | Returns |
|------|---------|
| `await counters.Get('a')` | `int` (value only) |
| `await Computed.Capture(() => counters.Get('a'))` | `Computed<int>` |

## Task

$ARGUMENTS
