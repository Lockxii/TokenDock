# macOS signing and notarization

Status: prepared, not distribution-validated. Ordinary native CI signs development bundles ad hoc with hardened runtime and executes their checks. It does not have Developer ID certificates, submit to Apple, or publish installers. Full product parity remains required even after signing succeeds.

## Required on the signing Mac

- Xcode command-line tools and the matching .NET SDK.
- A Developer ID Application certificate **and private key** in the keychain.
- A Developer ID Installer certificate **and private key** for PKG distribution.
- A `notarytool` keychain profile created with your Apple Developer credentials. Use Apple's interactive `xcrun notarytool store-credentials TokenDock` flow; never put passwords, API keys, or certificate exports in this repository, issues, or chat.

Inspect installed identities using `security find-identity -v -p codesigning`. Configure the following in the signing shell, using your own certificate names (these names are not secrets):

```bash
export MACOS_APP_IDENTITY='Developer ID Application: YOUR NAME (TEAMID)'
export MACOS_INSTALLER_IDENTITY='Developer ID Installer: YOUR NAME (TEAMID)'
export MACOS_NOTARY_PROFILE='TokenDock'
bash scripts/package-native.sh osx-arm64 # or osx-x64 on Intel
```

The output directory must not already exist. This is an explicit Apple submission, not a public release. Do not run it for unreviewed code. For CI credentials, use a protected GitHub environment, restricted trusted refs, a temporary keychain and cleanup in an always-run step; never expose signing secrets to pull-request code. No secret-bearing workflow is enabled yet.

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

For a future signing workflow, create a protected GitHub environment named `macos-signing`, restrict it to the reviewed signing ref, and require a maintainer review. Suggested secret names are `MACOS_APP_P12_BASE64`, `MACOS_APP_P12_PASSWORD`, `MACOS_INSTALLER_P12_BASE64`, `MACOS_INSTALLER_P12_PASSWORD`, `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`. Base64 only transports the encrypted export; it is not encryption. These names are a handoff convention, **not an enabled secret-consuming workflow**. The credential-free validation workflow never reads these secrets.

Once configured, a reviewed manual signing job must import the certificates into a temporary keychain, grant codesign access only to the imported signing keys, create the `MACOS_NOTARY_PROFILE` there, invoke the existing package/sign/notarize scripts, and delete the keychain and decrypted exports in an always-run cleanup. Signing must never run on fork/PR code. Do not publish while the remaining parity gates are open, even if notarization is accepted.

No certificate material or private keys are currently configured in this repository. No notarization request has been submitted.
