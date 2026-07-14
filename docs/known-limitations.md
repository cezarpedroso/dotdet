# Known Limitations

DotDet v0.1 is a Preview / Calibration release. The following limitations are
intentional and should be considered when interpreting reports.

## Analysis fidelity

- Uploaded ZIP and public/private GitHub repositories use safe syntax analysis in
  the web process. Full Roslyn/MSBuild semantic loading for untrusted input awaits
  an isolated worker or container boundary.
- Trusted local and sample analysis reports semantic fidelity only when semantic
  projects and documents actually load. Unsupported SDKs, restore state, or
  project-loading failures can produce degraded or syntax-fallback analysis.

## Analyzer boundaries

- Dependency Injection analysis is not a complete runtime service-provider
  simulation. Dynamic registration, reflection, generated code, decorators,
  keyed services, and complex composition methods can limit certainty.
- Security and configuration checks are static indicators. They do not replace
  threat modeling, secret scanning, penetration testing, SAST/DAST, or runtime
  verification.
- EF Core migration inspection cannot prove that backups exist or that
  schema/data changes are operationally safe.
- Architecture layers are inferred from project references, namespaces, source,
  packages, and conventional names. Unconventional structures can reduce
  confidence.
- API/Web UI intent is evidence-based and may remain ambiguous for generated,
  dynamically mapped, or heavily abstracted endpoint configuration.

## Source and history

- Live source preview is session-oriented. Saved history intentionally omits raw
  source and cannot reopen complete Code Explorer content without rerunning.
- Reports contain finding evidence and short guidance snippets, not a complete
  archival copy of the repository.
- Existing development history created by older builds may need to be deleted or
  migrated; current reads sanitize legacy path values.

## GitHub workflows

- Repository analysis targets the default branch.
- Branch selection, pull-request analysis, annotations, and repository write-back
  are not implemented.
- DotDet disconnect deletes its protected stored token but may not revoke the
  OAuth grant at GitHub. Revoke the application in GitHub settings when complete
  revocation is required.

## Interpretation

- The score and status are engineering prioritization aids, not certification or
  compliance evidence.
- A clean report does not prove the absence of security, reliability, performance,
  or domain-specific defects.
- DotDet does not replace maintainer review, runtime observability, load testing,
  database rehearsal, incident readiness, or operational ownership.

See [Engine maturity](engine-maturity.md) and the [Roadmap](roadmap.md).
