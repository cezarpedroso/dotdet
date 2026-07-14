# Private GitHub Repository Analysis Security

DotDet supports private GitHub repository analysis through an explicit GitHub permission upgrade. Normal GitHub login requests only profile/email scopes. Private repository analysis requires the user to enable repository access from the Analyze page.

## Token Handling

- GitHub repository access tokens are captured only by the ASP.NET Core backend during the OAuth callback.
- Tokens are protected at rest with ASP.NET Core Data Protection.
- Tokens are keyed to the authenticated GitHub user.
- Tokens are never returned to the React frontend.
- Tokens are never written to analysis history, exported reports, source preview, logs, or browser storage.
- Disconnecting private repository access deletes the protected token from DotDet local storage.

## Repository Analysis Flow

1. The user enables private repository access.
2. DotDet stores the protected token server-side.
3. The backend lists public and private repositories with the GitHub API.
4. When the user starts analysis, the backend downloads the selected repository default branch ZIP to a temporary location.
5. The archive is validated and extracted through the same `ZipExtractionService` path used by uploaded ZIP analysis.
6. DotDet analyzes the extracted solution and returns a live `AnalysisResult` with source preview for the current session.
7. DotDet saves a sanitized history snapshot without raw source files.
8. DotDet deletes the downloaded ZIP and extracted files.

## Live Results vs History

Live GitHub analysis may return source preview so Code Explorer can inspect the current analysis result. Saved history intentionally removes:

- raw source file contents
- server temp paths
- extracted repository paths
- suppression file path
- solution path

History preserves findings, evidence, scores, architecture data, rule explanations, and export-ready report data.

## Current Limitations

- DotDet analyzes the default branch only.
- DotDet does not synchronize suppressions or findings back to GitHub.
- DotDet does not store downloaded archives or extracted repositories for re-runs.
- If repository access is revoked in GitHub, users must reconnect access from DotDet.
