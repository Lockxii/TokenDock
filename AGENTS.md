# Instructions pour les assistants de code

- Lire README.md et uniquement les fichiers pertinents. Application Windows WPF/.NET 10 ; conserver le stack et les dépendances natives sauf besoin démontré.
- Faire le plus petit changement cohérent ; préserver les modifications de l’utilisateur. Ne pas lancer, arrêter ou piloter l’application sur le bureau sans demande explicite.
- Ne pas activer de caméra, capturer le presse-papiers réel ou lire des conversations pour valider un changement. Utiliser des fixtures. Ne publier aucune donnée personnelle.
- Caméra uniquement quand son panneau est visible et ouvert ; libérer la session au repli, masquage, remplacement ou fermeture. Audio interdit dans ce module photo.
- Préserver les originaux des fichiers déposés, les budgets et validations des entrées. Les données externes sont non fiables.
- Préférences rétrocompatibles, valeurs bornées, Appliquer/Annuler respectés. Respect des animations Windows et accès clavier.
- Zones de survol/dépôt cohérentes avec le rendu. Tester les quatre bords, les changements rapides et les transitions interrompues.
- Vérification : `dotnet publish next/TokenDock.Next.csproj -c Release -o artifacts/dev --no-self-contained`, puis `dotnet artifacts/dev/TokenDock.Next.dll --check`. Arrêter si un contrôle échoue ; ne pas annoncer une réussite sur un ancien checks.txt.
- Ne jamais committer artifacts/, bin/, obj/, secrets, préférences ou captures personnelles. Distinguer contrôles hors écran et tests matériels réels dans le compte rendu.
- Aucun push, release ou changement public sans demande de l’utilisateur. Une fois explicitement demandé, exécuter sans confirmations répétées.
