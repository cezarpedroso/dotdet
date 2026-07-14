# DotDet Roadmap

This roadmap describes current intent, not committed dates. Priorities may change
as calibration and Preview usage reveal higher-value work.

## v0.1 Preview

- Publish the public documentation set.
- Continue real-repository calibration and false-positive regression coverage.
- Validate ZIP, public GitHub, and private GitHub workflows in staging.
- Stabilize source/path sanitization, fidelity reporting, exports, and history.
- Keep rule growth secondary to trust, evidence quality, and scoring consistency.

## v0.2

- Add branch selection and branch-aware repository metadata.
- Expand the deterministic calibration sample suite.
- Improve host-scoped DI registration/lifetime reasoning.
- Add environment-aware security and configuration severity.
- Expand analysis fidelity, project-load diagnostics, and timing metadata.
- Improve report comparison and rerun workflows without retaining raw source.

## v0.3

- Add pull-request analysis and review-oriented output.
- Introduce an isolated worker/container boundary for semantic analysis of
  untrusted ZIP and GitHub repositories.
- Expand architecture and composition-root reasoning for modular and mixed-host
  applications.

## Later

- SARIF and CI integrations.
- Team/repository policy configuration and severity overrides.
- Trend analysis across sanitized report snapshots.
- Broader framework/version calibration.
- Carefully scoped remediation automation where confidence and review controls
  are sufficient.

DotDet will continue to prefer explainable, context-aware findings over maximizing
rule count. See [Rule quality principles](rule-quality.md).
