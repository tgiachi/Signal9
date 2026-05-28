# Pipeline Configuration Schema Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` or `superpowers:subagent-driven-development` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow each media pipeline task to declare its own configuration in a standard backend-owned schema so the frontend can generate the configuration UI without hardcoded field knowledge.

**Architecture:** The backend exposes a JSON Schema-compatible configuration document for `SignalNineConfig`. Pipeline task configuration is generated from registered pipeline task descriptors and merged into the global config schema. TOML remains the persisted config format; JSON Schema describes the parsed TOML object shape. The frontend fetches this schema and converts it into the existing section/field form model.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Tomlyn, System.Text.Json, React, TanStack Query, Vitest.

---

## Scope Decisions

- Keep TOML as the persisted configuration format.
- Use a JSON Schema-compatible subset as the public schema contract.
- Allow custom JSON Schema extension metadata under `x-signalnine-ui`.
- Generate pipeline task config schema on the backend from task descriptors.
- Move task-specific settings under `Pipeline.Tasks.<TaskName>` as the canonical shape.
- Preserve backward compatibility for existing `Pipeline.PreviewCount` and `Pipeline.OverwriteExistingProbe` TOML values by migrating them into the new task config shape.
- Keep the current raw TOML editor available as the fallback and power-user path.
- Do not implement media scan/import behavior in this plan.
- Do not add visual/E2E checks unless explicitly requested.

## Schema Contract

The backend returns JSON with this shape:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://signalnine.local/schemas/config.json",
  "title": "SignalNine configuration",
  "type": "object",
  "properties": {
    "Pipeline": {
      "type": "object",
      "title": "Media pipeline",
      "x-signalnine-ui": {
        "section": "pipeline",
        "order": 500
      },
      "properties": {
        "Tasks": {
          "type": "object",
          "properties": {
            "Probe": {
              "type": "object",
              "title": "Probe",
              "x-signalnine-ui": {
                "group": "Probe",
                "order": 100
              },
              "properties": {
                "Enabled": {
                  "type": "boolean",
                  "title": "Enabled",
                  "default": true
                },
                "OverwriteExisting": {
                  "type": "boolean",
                  "title": "Overwrite existing probe",
                  "default": false
                }
              }
            }
          }
        }
      }
    }
  }
}
```

Supported field types for the first implementation:

- `string`
- `integer`
- `number`
- `boolean`
- `enum` through standard `enum` plus optional `oneOf` titles

Supported UI metadata:

- `x-signalnine-ui.section`
- `x-signalnine-ui.group`
- `x-signalnine-ui.order`
- `x-signalnine-ui.widget`
- `x-signalnine-ui.secret`

---

## File Structure

- Create `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaDocument.cs`
- Create `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaNode.cs`
- Create `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaUiMetadata.cs`
- Create `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaEnumOption.cs`
- Create `src/SignalNine.Core/Data/Config/PipelineProbeTaskConfig.cs`
- Create `src/SignalNine.Core/Data/Config/PipelinePreviewTaskConfig.cs`
- Modify `src/SignalNine.Core/Data/Config/PipelineConfig.cs`
- Modify `src/SignalNine.Core/Data/Config/PipelineTasksConfig.cs`
- Modify `src/SignalNine.Core/Toml/SignalNineTomlContext.cs`
- Modify `src/SignalNine.Core/Services/ConfigService.cs`
- Create `src/SignalNine.Web/Interfaces/IPipelineTaskConfigSchemaProvider.cs`
- Create `src/SignalNine.Web/Services/Config/ConfigSchemaService.cs`
- Create `src/SignalNine.Web/Services/Pipeline/ProbeMediaTaskConfigSchemaProvider.cs`
- Create `src/SignalNine.Web/Services/Pipeline/ExtractPreviewsTaskConfigSchemaProvider.cs`
- Modify `src/SignalNine.Web/Services/Pipeline/ProbeMediaTask.cs`
- Modify `src/SignalNine.Web/Services/Pipeline/ExtractPreviewsTask.cs`
- Modify `src/SignalNine.Web/Endpoints/ConfigEndpoints.cs`
- Modify `src/SignalNine.Web/Program.cs`
- Create `ui/src/features/config/use-config-schema.ts`
- Create `ui/src/features/config/schema-to-sections.ts`
- Modify `ui/src/features/config/toml-schema.ts`
- Modify `ui/src/features/config/config-form.tsx`
- Modify `ui/src/features/config/config-section-nav.tsx`
- Modify `ui/src/features/config/config-page.tsx`
- Add or update backend tests under `tests/SignalNine.Tests/Core/Services`
- Add or update backend tests under `tests/SignalNine.Tests/Web`
- Add or update frontend tests under `ui/src/features/config/__tests__`

---

### Task 1: Backend Schema DTOs

**Files:**
- Create: `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaDocument.cs`
- Create: `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaNode.cs`
- Create: `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaUiMetadata.cs`
- Create: `src/SignalNine.Core/Data/Config/Schema/ConfigSchemaEnumOption.cs`

- [ ] Create a small JSON Schema-compatible object model.
- [ ] Use nullable properties so serialization omits unused JSON Schema members.
- [ ] Keep extension metadata isolated in `ConfigSchemaUiMetadata`.
- [ ] Avoid a full JSON Schema dependency for the first implementation.
- [ ] Add XML docs if any public interface is introduced in this task.
- [ ] Verify naming and namespaces against `CODE_CONVENTION.md`.

Expected model capabilities:

- root document metadata: `$schema`, `$id`, `title`, `type`
- nested `properties`
- primitive `type`
- `default`
- `description`
- `minimum`
- `maximum`
- `enum`
- `oneOf` or equivalent enum labels
- `x-signalnine-ui`

### Task 2: Canonical Per-Task Pipeline Config

**Files:**
- Create: `src/SignalNine.Core/Data/Config/PipelineProbeTaskConfig.cs`
- Create: `src/SignalNine.Core/Data/Config/PipelinePreviewTaskConfig.cs`
- Modify: `src/SignalNine.Core/Data/Config/PipelineConfig.cs`
- Modify: `src/SignalNine.Core/Data/Config/PipelineTasksConfig.cs`
- Modify: `src/SignalNine.Core/Toml/SignalNineTomlContext.cs`

- [ ] Replace generic task toggles with typed task config classes.
- [ ] Move `OverwriteExistingProbe` to `Pipeline.Tasks.Probe.OverwriteExisting`.
- [ ] Move `PreviewCount` to `Pipeline.Tasks.Preview.PreviewCount`.
- [ ] Keep legacy nullable properties in `PipelineConfig` only if required to read old TOML.
- [ ] Register new config classes in `SignalNineTomlContext`.
- [ ] Keep defaults equivalent to current behavior.

Target TOML shape:

```toml
[Pipeline.Tasks.Probe]
Enabled = true
OverwriteExisting = false

[Pipeline.Tasks.Preview]
Enabled = true
PreviewCount = 5
```

### Task 3: Config Migration And Backfill

**Files:**
- Modify: `src/SignalNine.Core/Services/ConfigService.cs`
- Test: `tests/SignalNine.Tests/Core/Services/ConfigServiceTests.cs`

- [ ] Add tests for loading a legacy config that contains `Pipeline.PreviewCount`.
- [ ] Add tests for loading a legacy config that contains `Pipeline.OverwriteExistingProbe`.
- [ ] Add tests that missing per-task config sections are backfilled with defaults.
- [ ] Normalize legacy values into the canonical `Pipeline.Tasks.*` shape.
- [ ] Save the normalized TOML after migration.
- [ ] Ensure validation still rejects malformed TOML.
- [ ] Run:

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter ConfigServiceTests
```

Expected result: config service tests pass and migrated TOML contains `[Pipeline.Tasks.Probe]` and `[Pipeline.Tasks.Preview]`.

### Task 4: Pipeline Task Schema Providers

**Files:**
- Create: `src/SignalNine.Web/Interfaces/IPipelineTaskConfigSchemaProvider.cs`
- Create: `src/SignalNine.Web/Services/Pipeline/ProbeMediaTaskConfigSchemaProvider.cs`
- Create: `src/SignalNine.Web/Services/Pipeline/ExtractPreviewsTaskConfigSchemaProvider.cs`
- Modify: `src/SignalNine.Web/Program.cs`

- [ ] Define an interface for pipeline task config schema providers.
- [ ] Keep the interface under a Web `Interfaces` namespace and add XML docs.
- [ ] Register one provider per pipeline task.
- [ ] Make the provider expose task name, order, display name, and JSON Schema node.
- [ ] Add the shared `Enabled` field to every task schema.
- [ ] Add `OverwriteExisting` for the probe task.
- [ ] Add `PreviewCount` for the preview task with sane numeric bounds.

Recommended interface shape:

```csharp
public interface IPipelineTaskConfigSchemaProvider
{
    string TaskName { get; }

    string DisplayName { get; }

    int Order { get; }

    ConfigSchemaNode CreateSchema();
}
```

### Task 5: Use Canonical Config In Pipeline Tasks

**Files:**
- Modify: `src/SignalNine.Web/Services/Pipeline/ProbeMediaTask.cs`
- Modify: `src/SignalNine.Web/Services/Pipeline/ExtractPreviewsTask.cs`
- Test: existing pipeline tests or new focused service tests

- [ ] Change probe enablement to read `Pipeline.Tasks.Probe.Enabled`.
- [ ] Change probe overwrite behavior to read `Pipeline.Tasks.Probe.OverwriteExisting`.
- [ ] Change preview enablement to read `Pipeline.Tasks.Preview.Enabled`.
- [ ] Change preview count to read `Pipeline.Tasks.Preview.PreviewCount`.
- [ ] Add or update tests that prove task behavior follows the new config paths.

### Task 6: Global Config Schema Endpoint

**Files:**
- Create: `src/SignalNine.Web/Services/Config/ConfigSchemaService.cs`
- Modify: `src/SignalNine.Web/Endpoints/ConfigEndpoints.cs`
- Modify: `src/SignalNine.Web/Program.cs`
- Test: `tests/SignalNine.Tests/Web/ConfigEndpointTests.cs`

- [ ] Build a global schema document for `SignalNineConfig`.
- [ ] Include existing static config areas: runtime, JWT, job system, FFmpeg pool.
- [ ] Generate the pipeline section from registered `IPipelineTaskConfigSchemaProvider` services.
- [ ] Add `GET /api/config/schema`.
- [ ] Preserve the current raw TOML endpoints.
- [ ] Match the existing authorization policy for `/api/config`.
- [ ] Add endpoint tests that assert the JSON document contains:
  - `$schema`
  - `properties.Pipeline.properties.Tasks.properties.Probe`
  - `properties.Pipeline.properties.Tasks.properties.Preview`
  - `OverwriteExisting`
  - `PreviewCount`

### Task 7: Frontend Schema Fetch

**Files:**
- Create: `ui/src/features/config/use-config-schema.ts`
- Modify: `ui/src/features/config/use-config.ts`

- [ ] Add TanStack Query hook for `GET /api/config/schema`.
- [ ] Use query key `['config', 'schema']`.
- [ ] Do not store secrets from config schema or config values in localStorage.
- [ ] Keep raw TOML fetch unchanged.
- [ ] Surface schema fetch errors with sonner or the existing config page error pattern.

### Task 8: Frontend Schema Adapter

**Files:**
- Create: `ui/src/features/config/schema-to-sections.ts`
- Modify: `ui/src/features/config/toml-schema.ts`
- Test: `ui/src/features/config/__tests__/schema-to-sections.test.ts`

- [ ] Define TypeScript types for the backend JSON Schema subset.
- [ ] Convert nested JSON Schema properties into the existing `SectionSpec` and `FieldSpec` model.
- [ ] Infer TOML paths from the JSON Schema property tree.
- [ ] Sort sections, groups, and fields by `x-signalnine-ui.order`.
- [ ] Convert boolean/string/integer/number fields to existing form controls.
- [ ] Convert enum fields to select controls.
- [ ] Respect `x-signalnine-ui.secret` by rendering password-style fields.
- [ ] Keep a local static fallback schema only as a resilience path.

### Task 9: Config Form Uses Runtime Schema

**Files:**
- Modify: `ui/src/features/config/config-form.tsx`
- Modify: `ui/src/features/config/config-section-nav.tsx`
- Modify: `ui/src/features/config/config-page.tsx`
- Test: `ui/src/features/config/__tests__/config-form.test.tsx`

- [ ] Pass `sections` into `ConfigForm` instead of importing `SCHEMA` directly.
- [ ] Pass `sections` into `ConfigSectionNav`.
- [ ] Show a compact loading state while config text or schema is loading.
- [ ] Keep dirty tracking based on TOML serialization.
- [ ] Ensure generated pipeline fields edit the correct TOML paths.
- [ ] Add tests for:
  - rendering generated schema fields
  - dirty tracker after generated field edits
  - submit calls API with canonical TOML
  - schema fetch failure uses fallback or shows a recoverable error

### Task 10: Verification

**Commands:**

```bash
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter ConfigServiceTests
dotnet test tests/SignalNine.Tests/SignalNine.Tests.csproj --filter ConfigEndpointTests
npm test -- src/features/config/__tests__
npm run typecheck
npm run lint
```

- [ ] Run backend config service tests.
- [ ] Run backend config endpoint tests.
- [ ] Run frontend config tests.
- [ ] Run frontend typecheck.
- [ ] Run frontend lint.
- [ ] Do not run visual checks unless explicitly requested.

### Task 11: Commit

Use narrow staging so concurrent backend/frontend work is preserved.

```bash
git add docs/superpowers/plans/2026-05-28-pipeline-config-schema.md
git add src/SignalNine.Core/Data/Config src/SignalNine.Core/Toml/SignalNineTomlContext.cs src/SignalNine.Core/Services/ConfigService.cs
git add src/SignalNine.Web/Interfaces src/SignalNine.Web/Services/Config src/SignalNine.Web/Services/Pipeline src/SignalNine.Web/Endpoints/ConfigEndpoints.cs src/SignalNine.Web/Program.cs
git add tests/SignalNine.Tests/Core/Services tests/SignalNine.Tests/Web
git add ui/src/features/config
git commit -m "feat(config): add pipeline task schema"
```

If unrelated dirty files are present, use `git commit --only -- <paths>` for the actual implementation commit.

---

## Open Risks

- Tomlyn serialization behavior for nullable legacy properties must be verified before relying on them for migration.
- Existing `/api/config` authorization is currently its own policy surface; this plan preserves it instead of changing auth semantics.
- JSON Schema is used as a UI contract subset, not as a complete validation engine in the first implementation.
- If pipeline tasks become plugin-loaded later, schema providers should be registered through the same plugin discovery path.
