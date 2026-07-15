# DotDet Privacy Policy

> This is not legal advice. These terms are provided for the DotDet preview
> project and may need review before broader production or commercial use.

This Privacy Policy describes the current DotDet v0.1 Preview behavior. DotDet
is an early ASP.NET Core production-readiness analysis workbench, and the data
handling model may change as the product matures.

## Information DotDet may collect

DotDet may collect or process the following information when you use the
preview:

- GitHub OAuth profile information needed to identify your session, such as
  GitHub account id, username, display name, avatar, and email if GitHub
  provides it.
- Repository metadata needed to list or analyze repositories, such as owner,
  repository name, default branch, visibility, and selected repository id.
- Uploaded ZIP files and downloaded GitHub repository archives during analysis.
- Analysis results, scores, findings, evidence, rule metadata, dispositions,
  suppressions, exports, and saved report history.
- Browser storage used to restore recent UI state and sanitized analysis
  metadata.
- Operational logs and error information needed to run and troubleshoot the
  preview.

## GitHub OAuth and private repositories

DotDet uses GitHub OAuth for login. Private repository access requires an
explicit repository access upgrade. GitHub access tokens are handled on the
backend and are not intentionally returned to the browser, included in reports,
or exposed in saved history.

Private repository tokens are handled at a high level as server-side credentials
for repository listing and archive download. If you disconnect or remove access,
DotDet should stop using the stored token for future private repository
analysis. You may also revoke DotDet access from GitHub.

## Repository and archive handling

Uploaded ZIP files and downloaded GitHub repository archives are used for
analysis. DotDet validates archives for unsafe paths and processes them in
temporary DotDet-owned locations. Archives and extracted temporary folders are
intended to be cleaned up after analysis.

DotDet does not intentionally store downloaded ZIP files or extracted private
repository folders as saved history.

## Source preview behavior

Live analysis results may include source preview data so the Code Explorer can
show relevant files during the current session. Source preview is capped and is
intended for developer review, not archival storage.

Saved history is sanitized and should preserve findings, evidence, scores, and
report data without retaining full raw source files. Historical reports may show
that source preview is unavailable and ask you to re-run the analysis to inspect
source again.

## Browser storage

DotDet may use browser local storage or session storage to restore recent UI
state, selected tabs, selected findings, and sanitized analysis metadata. The
preview is designed to avoid persisting full source previews, local server paths,
GitHub tokens, and raw private repository source in browser storage.

Logging out or disconnecting should clear cached analysis state that may contain
sensitive session information.

## Exports

DotDet can export JSON, Markdown, and HTML reports. Human-readable exports are
designed to include findings, evidence, snippets, recommendations, and links
without dumping full source files by default.

Default JSON export is sanitized and should not include full source preview for
private GitHub repository analyses. Review any report before sharing it because
file names, project names, snippets, findings, and recommendations can still
reveal information about your codebase.

## Local path analysis

Local filesystem path analysis is blocked outside the Development environment.
It is intended for local development use only and should not be exposed as a
public hosted feature.

## Analysis fidelity for untrusted input

Untrusted ZIP and GitHub repository inputs use safe syntax analysis in the web
process unless isolated semantic analysis is explicitly configured. DotDet does
not run in-process MSBuildWorkspace semantic loading for untrusted uploaded or
GitHub archives in the default preview configuration.

## Third-party services

DotDet may rely on:

- GitHub for OAuth login, repository metadata, repository authorization, and
  repository archive downloads.
- The hosting provider used to run the DotDet preview.

Those services process data under their own policies and controls.

## Data retention

DotDet v0.1 Preview currently stores saved analysis history and related report
metadata for product functionality. Raw source preview is intended to be live
session data, not saved history. Temporary upload and repository archive data is
intended to be deleted after analysis.

Because this is a preview, retention behavior should be reviewed before broader
production or commercial use.

## Deletion and disconnect

You can use logout or disconnect flows to end the current session and stop
future use of connected GitHub access. You can also revoke DotDet access in
GitHub. Saved history or report metadata may require separate deletion behavior
depending on the current preview implementation.

## Contact

For privacy questions, deletion requests, or concerns about DotDet preview data
handling, contact cezarapedroso@gmail.com.
