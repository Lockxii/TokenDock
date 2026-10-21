# macOS signing and notarization

> Optional future reference only. The owner has chosen local source installation without Developer ID signing or notarization. No certificates or signing secrets are needed for that route. Keep the signing workflow disabled. Use the [README agent installation guide](../README.md#macos--linux-install-with-your-ai-agent).

Status: prepared, not distribution-validated. Ordinary native CI signs development bundles ad hoc with hardened runtime and executes their checks. It does not have Developer ID certificates, submit to Apple, or publish installers. Full product parity remains required even after signing succeeds.

## Required on the signing Mac

- Xcode command-line tools and the matching .NET SDK.
- A Developer ID Application certificate **and private key** in the keychain.
- A Developer ID Installer certificate **and private key** for PKG distribution.
- A `notarytool` keychain profile created with your Apple Developer credentials. Use Apple's interactive `xcrun notarytool store-credentials TokenDock` flow; never put passwords, API keys, or certificate exports in this repository, issues, or chat.

Inspect installed identities using `security find-identity -v` (use `-p codesigning` to filter application signing identities). Configure the following in the signing shell, using your own certificate names (these names are not secrets):

```bash
export MACOS_APP_IDENTITY='Developer ID Application: YOUR NAME (TEAMID)'
export MACOS_INSTALLER_IDENTITY='Developer ID Installer: YOUR NAME (TEAMID)'
export MACOS_NOTARY_PROFILE='TokenDock'
bash scripts/package-native.sh osx-arm64 # or osx-x64 on Intel
```

The output directory must not already exist. This is an explicit Apple submission, not a public release. Do not run it for unreviewed code. For CI credentials, use a protected GitHub environment, restricted trusted refs, a temporary keychain and cleanup in an always-run step; never expose signing secrets to pull-request code. The manual signing rehearsal workflow is gated off until explicitly configured; normal CI never receives signing secrets.

## What the scripts do

1. Build a self-contained single-file managed app on the matching architecture. Keep the apphost/native libraries in `Contents/MacOS`, data/scripts in `Contents/Resources`, the camera app in `Contents/Helpers`, and MediaRemote in `Contents/Frameworks`.
2. Sign every Mach-O dependency, then framework bundles and the camera app, then the outer app. Signing is inside-out; `--deep` is used only for verification.
3. Enable hardened runtime. The .NET host receives JIT and Apple Events permissions; the camera helper receives camera permission. No sandbox, debugger, unsigned-executable-memory or library-validation bypass entitlement is added. Real permission prompts still require validation.
4. Verify nested signatures and execute synthetic app/helper checks.
5. When a notarization profile is explicitly set, submit an app ZIP and require `Accepted`, staple the app and assess it with Gatekeeper before building containers.
6. Create the signed PKG and signed DMG, submit each, require acceptance, staple and validate their tickets, then assess with Gatekeeper. Failure exits without a release.

Without the identity/profile variables the same packaging script makes **ad-hoc development** artifacts only. A successful ad-hoc signature does not establish Apple trust. Notarization timeouts/rejections must be investigated with `xcrun notarytool log SUBMISSION_ID --keychain-profile TokenDock`; do not bypass the check.

## Before publication

Verify both architecture artifacts on a clean user account, including the downloaded/quarantined DMG and PKG, nested camera permission and teardown, media permissions, upgrade/removal, and the remaining parity gates in [PLATFORMS.md](PLATFORMS.md). Keep the signature, entitlement, notarization and Gatekeeper reports with the release evidence. Generate final hashes only after stapling. No release workflow or automatic upload of binaries is provided.

References: [Apple notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow), [Apple hardened runtime](https://help.apple.com/xcode/mac/current/en.lproj/devf87a2ac8f.html), [.NET macOS deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos).

## Exact handoff for signing from GitHub-hosted macOS

A personal Mac is not required for the signing job: the project already builds on hosted macOS runners. You do need an active Apple Developer Program membership and permission to create Developer ID certificates for its team.

Prepare these items **in a protected secret store, never in chat or Git**:

| Item | Required contents |
| --- | --- |
| Application signing identity | A valid **Developer ID Application** certificate and its matching private key, exported together as a password-protected `.p12`. A `.cer` alone cannot sign. |
| Installer signing identity | A valid **Developer ID Installer** certificate and its matching private key, also in `.p12`. The two identities may share one export if both private keys are included. |
| Export password(s) | The password used to encrypt each `.p12`. |
| Apple team | The Team ID that owns both certificates. |
| Notarization authentication | The Apple ID associated with the team and an **app-specific password**, not its normal sign-in password. Alternatively, a team App Store Connect API `.p8` private key, Key ID and Issuer ID with the required access. Use one authentication method. |
| Identity names | Exact `Developer ID Application: … (TEAMID)` and `Developer ID Installer: … (TEAMID)` names. These names and Team ID can be shared as non-secret configuration. |

## Protected manual rehearsal workflow

`.github/workflows/macos-signing.yml` is manual-only. It signs and notarizes development containers; it never creates a release or uploads binaries. It is deliberately disabled unless the **repository variable** `MACOS_SIGNING_ENABLED` is `true`. No certificates or notarization credentials are currently configured.

Before enabling it:

1. Merge and review the intended code on `main`, then require a successful `Native port integration` **push** run for that exact commit. The dispatch input must equal the selected main commit SHA; arbitrary source refs are rejected.
2. Create the GitHub environment `macos-signing`. Configure required reviewers, prevent self-review, and a single deployment branch policy of type branch named exactly `main`. The workflow checks these settings through GitHub's API and fails closed if they cannot be verified. This requires a trusted reviewer other than the dispatching user.
3. Add environment variables `MACOS_APP_IDENTITY`, `MACOS_INSTALLER_IDENTITY`, and `APPLE_TEAM_ID` with the exact certificate identity names and team.
4. Add encrypted environment secrets `MACOS_APP_P12_BASE64`, `MACOS_APP_P12_PASSWORD`, `MACOS_INSTALLER_P12_BASE64`, `MACOS_INSTALLER_P12_PASSWORD`. Base64 is transport encoding, not encryption; keep the exports password-protected.
5. For notarization, add either `APPLE_ID` and `APPLE_APP_SPECIFIC_PASSWORD`, or `APPLE_API_KEY_P8_BASE64`, `APPLE_API_KEY_ID`, and `APPLE_API_ISSUER_ID`. If the API key is present, that method is selected. Do not configure both methods unnecessarily.
6. Set the repository enable variable only after the environment restrictions and secrets are in place. Dispatch from `main` with its reviewed 40-character commit SHA.

The workflow uses pinned official actions and a read-only GitHub token. A build/check step runs without signing secrets. Credentials are supplied only to the keychain preparation step; they are never printed. The preparation script imports keys into a temporary keychain, validates the requested identities, stores a notarytool profile there, and removes decrypted exports. Notarization uses that explicit keychain. An always-run cleanup restores the original keychain search list and deletes the temporary keychain; error paths attempt both operations. Hosted-runner teardown is the final boundary for abrupt termination.

Credential-free tests cover import failure, invalid exports, both authentication methods, secret-safe error messages, cleanup failures, wrong teams and unowned directories. Native CI also creates/removes a real **empty** temporary keychain and verifies search-list restoration. These tests do not establish that actual Developer ID credentials or notarization work: neither has been exercised against Apple.

Successful rehearsals write final container hashes to the job summary after stapling; they do not retain binaries. Full parity gates remain mandatory before a separate publication workflow can be introduced. No notarization request has been submitted by this preparation work.
