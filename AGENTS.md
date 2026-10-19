# Coding-agent instructions

- Read README.md and relevant files only. Production stack: Windows WPF/.NET 10. Preserve it unless a demonstrated need justifies a change.
- Make the smallest coherent diff and preserve user edits. Do not control, launch or stop the desktop app without explicit authorization.
- Never activate cameras, capture the real clipboard or read conversations for validation. Use synthetic fixtures; publish no private data.
- Camera only while its panel is open/visible; release on collapse, hide, replacement and exit. No audio in this photo module.
- Preserve original files, budgets and input validation. External data is untrusted.
- Preferences remain backward-compatible and bounded; respect Apply/Cancel, reduced motion and keyboard access.
- Hit targets must match drawing. Check four edges and interrupted transitions.
- Build: `dotnet publish next/TokenDock.Next.csproj -c Release -o artifacts/dev --no-self-contained`. Test: `dotnet artifacts/dev/TokenDock.Next.dll --check`. Never cite stale checks.txt after failure.
- Do not commit artifacts/, bin/, obj/, secrets, private preferences or captures. Distinguish offscreen checks from hardware validation.
- No public push/release without authorization; once given, avoid redundant confirmations.
- UI and documentation are English. Preserve identifiers and persisted keys during translation.
- Linux/macOS require full parity before publication. Follow docs/PLATFORMS.md; do not publish partial ports as supported.
