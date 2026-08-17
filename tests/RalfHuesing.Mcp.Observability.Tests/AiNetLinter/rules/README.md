# AiNetLinter Rules & Adjustment Log

This directory contains the configuration file [`RalfHuesing.Mcp.Observability.rules.json`](./RalfHuesing.Mcp.Observability.rules.json) used by **AiNetLinter** to validate C# code quality during automated test runs (`AiNetLinterTests.cs`).

---

## 1. Policy for Rule Adjustments (Begründungspflicht)

While the default rules define our code quality baseline, specific architectural constraints (e.g. MCP SDK tool method signatures, xUnit test conventions, framework interfaces) might require adjusting or relaxing certain rules.

### Guidelines for Changes:
1. **Granular over Global:**
   - Always prefer **`PathOverrides`** (for a specific file) or **`ProjectOverrides`** (for test projects) instead of relaxing global metrics in the `"Global"` or `"Metrics"` section.
2. **Mandatory Rationale:**
   - Every rule change, override, or threshold increase **MUST be documented** in this file under [Section 3 (Rule Adjustment Log)](#3-rule-adjustment-log).
   - Unjustified or undocumented rule relaxations are not permitted.
3. **Verification & Hot-Reload:**
   - Call the **`reload_config`** MCP tool on `ainetlinter` so that the resident MCP server immediately adopts the new rules without restarting the process.
   - Run `dotnet test` to ensure:
     - The linter validates cleanly against the solution (`valExitCode == 0`).
     - The updated rules are synchronized to `.agents/rules/AiNetLinter.mdc`.

---

## 2. Template for Documenting Rule Adjustments

When modifying or adding overrides to `RalfHuesing.Mcp.Observability.rules.json`, append a new entry to the log below using this template:

```markdown
### [YYYY-MM-DD] <Rule or Setting Name>

- **Scope / Target:** `PathOverrides["..."]` | `ProjectOverrides["..."]` | `Global` / `Metrics`
- **Previous Value:** `<old value>`
- **New Value:** `<new value>`
- **Reason & Architecture Context:**
  <Detailed technical justification explaining why the default rule was too strict or inappropriate for this specific case.>
- **Considered Alternatives:**
  <Brief explanation of why refactoring or extracting code was not feasible or desirable.>
```

---

## 3. Rule Adjustment Log

### [2026-08-17] PathOverride: MaxMethodParameterCount for FeedbackTools.cs

- **Scope / Target:** `PathOverrides["src/RalfHuesing.Mcp.Observability/Internal/FeedbackTools.cs"].Metrics.MaxMethodParameterCount`
- **Previous Value:** `4` (Global default)
- **New Value:** `10`
- **Reason & Architecture Context:**
  The `report_observability_feedback` MCP tool method requires multiple optional and required parameters (`feedbackType`, `title`, `description`, `relatedTool`, `severity`, `expectedBehavior`, `actualBehavior`, `additionalContext`) directly exposed as method parameters for the MCP SDK schema generator.
- **Considered Alternatives:**
  Wrapping parameters in a complex class would change the MCP tool declaration structure under the current SDK version.

### [2026-08-17] ProjectOverride: MaxMethodLineCount and EnforceSealedClasses for *.Tests

- **Scope / Target:** `ProjectOverrides["*.Tests"]`
- **Previous Value:** `MaxMethodLineCount: 60`, `EnforceSealedClasses: true`
- **New Value:** `MaxMethodLineCount: 120`, `EnforceSealedClasses: false`
- **Reason & Architecture Context:**
  Test methods often contain arrange-act-assert blocks with setup data, mock initialization, and multiple assertions that reasonably exceed 60 lines. Test classes are generally not inherited and do not require mandatory `sealed` modifiers.
- **Considered Alternatives:**
  Overly fragmented test helper methods reduce readability of test specifications.
