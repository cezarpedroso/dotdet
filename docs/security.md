# DotDet Security

This document describes the public security and privacy model for DotDet v0.1
Preview. It is not a claim that DotDet has completed an independent security
assessment.

## Security model

DotDet separates trusted product code from analyzed repository content. Local
development paths and the bundled sample may use Roslyn/MSBuild semantic loading.
Uploaded ZIP files and GitHub repositories are untrusted inputs and use safe
syntax analysis in the web process. They do not invoke `MSBuildWorkspace` or
design-time MSBuild unless a future isolated worker is explicitly configured.

DotDet is designed around these boundaries:

- the browser receives only the data required for the current workbench session;
- GitHub repository tokens remain on the backend;
- saved reports retain findings and guidance, not full source files;
- uploaded and downloaded archives are temporary and validated before extraction;
- server-local paths are removed before analysis results leave the backend.

## Authentication

DotDet uses GitHub OAuth for user authentication. ZIP upload and public/private
GitHub repository analysis require an authenticated session. The bundled sample
analysis is public but rate-limited. Local path analysis requires authentication,
is restricted to the Development environment, and returns no production-server path
access outside that environment.

Authentication and OAuth correlation cookies are protected for secure browser
sessions. Operator configuration details are intentionally not part of the
public documentation set.

## GitHub OAuth and repository access

Normal login requests the `read:user` and `user:email` scopes. Private repository
analysis uses an explicit repository-access flow that currently adds GitHub's
`repo` OAuth scope. The backend receives the resulting token, associates it with
the authenticated GitHub user, and protects it at rest with ASP.NET Core Data
Protection. The `repo` scope is broad; users and operators should review it before
enabling private access.

Repository tokens are not:

- returned to the React frontend;
- written to browser storage;
- included in API report responses, history, or exports;
- intentionally written to application logs.

Disconnecting repository access deletes DotDet's stored token. DotDet does not
currently guarantee revocation of the OAuth grant at GitHub; users who require
complete revocation should also revoke the application in GitHub account
settings.

See [Private GitHub repository security](private-github-security.md).

## Source preview and frontend data

A live successful analysis may include repository-relative source preview so the
Code Explorer can display the files being reviewed. This preview is intended for
the active session only.

Saved history is sanitized and does not store raw source files. It keeps scores,
findings, evidence, rule guidance, project/architecture metadata, dispositions,
and report data. Opening a historical report clearly states that source preview
was not retained.

Browser persistence strips source files, source-preview payloads, solution paths,
repository roots, suppression paths, and other server-local path fields. Default
JSON, Markdown, and HTML exports are generated from sanitized report data. Human-
readable reports include relevant short remediation snippets, not complete source
files. Private GitHub repository results cannot include full source preview in
JSON export.

## Path and root-cause sanitization

Paths shown in live reports are repository-relative where possible. Absolute
server paths, temporary extraction roots, random extraction identifiers, and
local user-profile paths are removed from issue paths, evidence, source-preview
metadata, project graphs, architecture maps, browser persistence, and exports.
Root-cause keys use repository-relative file segments; unsafe legacy values are
sanitized when history is read.

## Archive safety

Uploaded and GitHub archives share the same extraction service. Controls include:

- compressed archive size limits;
- individual entry size limits;
- total expanded-size limits;
- entry-count limits;
- rooted-path, traversal, empty-segment, and unsupported-path rejection;
- extraction constrained to a DotDet-owned temporary directory;
- cancellation-aware streaming and extraction;
- cleanup after successful analysis and after failures.

These controls reduce archive abuse risk but do not replace operating-system or
container resource limits.

## Request controls

Analysis endpoints use request throttling, per-user or per-IP concurrency limits,
and an execution timeout. Cancellation flows through repository download,
extraction, analysis, and source-preview collection. Limit and timeout responses
use safe problem details rather than server paths.

## Known limitations

- DotDet has not yet introduced an isolated semantic-analysis worker for untrusted
  repositories.
- Static configuration checks do not prove runtime security.
- Disconnect removes DotDet's stored token but may not revoke the GitHub OAuth
  grant itself.

The complete product list is in [Known limitations](known-limitations.md).

## Responsible disclosure

Do not include private repository source, credentials, tokens, or exploitable
infrastructure details in a public issue. For ordinary product defects, use
[GitHub Issues](https://github.com/cezarpedroso/dotdet/issues). For a suspected
security vulnerability, contact the repository owner privately through the
contact method listed on the DotDet GitHub profile or the application's Contact
page, and provide only the minimum information needed to establish a secure
follow-up channel.
