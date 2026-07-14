# Private GitHub Repository Security

DotDet v0.1 Preview supports private repository analysis through a separate,
explicit GitHub repository-access flow. Enabling this access expands the trust
placed in a DotDet instance. Review the safeguards in this document before
authorizing the feature.

## Permission and purpose

Normal DotDet login requests `read:user` and `user:email`. To list and download
private repositories, the user must separately authorize GitHub's `repo` OAuth
scope. This is a broad scope, even though DotDet uses the resulting access only
to:

1. list repositories available to that GitHub account;
2. read repository metadata, including its default branch;
3. download the selected default-branch archive for analysis.

Review the scope shown by GitHub before authorizing it. DotDet v0.1 does not claim
that the broad permission has been reduced to repository-level or read-only
access.

## What DotDet does

- Keeps the repository token on the ASP.NET Core backend.
- Protects the stored token with ASP.NET Core Data Protection.
- Scopes stored access to the authenticated GitHub user.
- Downloads the selected repository's default branch into temporary storage.
- Validates and extracts the archive through the ZIP upload safety path.
- Uses safe syntax analysis in the main web process.
- Returns repository-relative live source preview for the active session.
- Saves a sanitized history snapshot without raw source.
- Deletes the downloaded archive and extracted directory after analysis.

## What DotDet does not do

DotDet does not:

- push commits or modify repository contents;
- create or update pull requests, issues, releases, or branches;
- execute repository build scripts through in-process MSBuild for untrusted input;
- send the GitHub repository token to the frontend;
- store raw private repository source in saved analysis history;
- retain downloaded ZIP files or extracted repository directories;
- include full private source in default JSON, Markdown, or HTML reports.

## Token storage and disconnect

The token is protected at rest using ASP.NET Core Data Protection and stored in
DotDet-owned backend storage. Operator configuration details are intentionally
kept outside the public documentation set.

Disconnecting repository access deletes DotDet's stored token and disables its
use by DotDet. The Preview may not revoke the authorization grant at GitHub. To
fully withdraw authorization, also revoke the DotDet OAuth application in GitHub
account settings.

## Live source, history, and exports

Live source preview is returned only after the selected repository has been
downloaded and analyzed. Paths are repository-relative and do not intentionally
expose DotDet server paths. The current browser session can use this data in Code
Explorer.

History stores findings, evidence, scores, architecture data, rule explanations,
repository metadata, and dispositions. It does not retain source-preview content.
A historical report therefore cannot reopen complete source; re-run the analysis
to inspect source preview again.

Default exports are sanitized. Markdown and HTML reports contain finding evidence
and useful short snippets but do not dump source files. JSON export does not
include private repository source preview, even when live preview is available.

## Limitations

- Analysis targets the repository's default branch only.
- Full semantic Roslyn/MSBuild analysis is not available for untrusted GitHub
  input until an isolated worker exists.
- DotDet does not synchronize findings or suppressions back to GitHub.

## Checklist before enabling access

- [ ] Confirm the GitHub OAuth scopes are no broader than the intended workflow.
- [ ] Analyze a test private repository and inspect browser network responses.
- [ ] Confirm history and all export formats contain no full source or token.
- [ ] Confirm temporary archives and extracted folders are removed after success,
      failure, cancellation, and timeout.
- [ ] Test disconnect and, when required, GitHub-side OAuth revocation.
See [Security](security.md) for the broader model.
