# TokenDock

**Une barre Windows personnalisable pour les tokens IA, la musique, les copies, la caméra et la RAM.**

TokenDock se place en haut, en bas, sur un côté ou librement sur l’écran. Survole une icône pour ouvrir son panneau, glisse les éléments pour les réordonner, et garde les informations utiles à portée de souris.

Application native **C# / WPF / .NET 10**, sans navigateur embarqué. Version initiale publique : **v0.1.0**, issue de la version de développement v41. Projet en évolution : les tests automatisés ne remplacent pas la validation avec chaque webcam, lecteur multimédia et configuration d’écran.

[**Télécharger pour Windows**](https://github.com/Lockxii/TokenDock/releases/latest) · [Modifier avec une IA](docs/AI_GUIDE.md) · [Contribuer](CONTRIBUTING.md) · [Signaler un problème](https://github.com/Lockxii/TokenDock/issues)

## Sommaire

- [Installation](#installation)
- [Premiers pas](#premiers-pas)
- [Modules](#modules)
- [Personnalisation](#personnalisation)
- [Données et confidentialité](#données-et-confidentialité)
- [Compiler et vérifier](#compiler-et-vérifier)
- [Modifier avec une IA](#modifier-avec-une-ia)
- [Dépannage](#dépannage)
- [Architecture](#architecture)
- [Limites et licence](#limites-et-licence)

## Installation

### Prérequis

- Windows 10 version 2004 ou ultérieure, ou Windows 11.
- Processeur **x64** pour l’archive publiée. Pas de version macOS/Linux.
- Une webcam pour le module Caméra et un lecteur exposant les commandes multimédias Windows pour Musique.
- Aucun compte TokenDock, clé API ou abonnement requis pour l’application.

### Installer la version prête à utiliser

1. Ouvre la [dernière release](https://github.com/Lockxii/TokenDock/releases/latest).
2. Télécharge `TokenDock-v0.1.0-win-x64.zip` et, si souhaité, `SHA256SUMS.txt`.
3. **Extrais tout le ZIP** dans un dossier où tu peux écrire, par exemple `Documents\Applications\TokenDock`.
4. Lance `TokenDock.Next.exe` depuis le dossier extrait. Ne lance pas directement depuis l’archive et ne déplace pas uniquement l’exécutable : les bibliothèques et `assets/` doivent rester ensemble.
5. L’archive autonome contient .NET : aucun runtime à installer séparément.

Le binaire n’est pas signé. Windows peut afficher un avertissement de réputation : vérifie la provenance de la release et son empreinte avant de décider de l’exécuter. Ne désactive pas globalement les protections Windows. Lance l’application normalement, **pas en administrateur**.

Vérifier une archive dans PowerShell :

```powershell
Get-FileHash .\TokenDock-v0.1.0-win-x64.zip -Algorithm SHA256
```

Compare le résultat à la ligne correspondante dans `SHA256SUMS.txt`.

### Mettre à jour

1. Menu de la barre ou icône de notification → **Quitter**.
2. Extrais la nouvelle version dans un nouveau dossier.
3. Lance le nouvel exécutable. Les préférences sont conservées dans `%LOCALAPPDATA%\TokenDock`.

Une seule instance peut fonctionner à la fois. Ouvrir une autre version pendant que l’ancienne tourne ne la remplace pas.

### Désinstaller

Quitte TokenDock, puis supprime son dossier extrait. Pour réinitialiser également les réglages, supprime le dossier `%LOCALAPPDATA%\TokenDock` après avoir sauvegardé ce que tu souhaites conserver. Les photos dans `Images\TokenDock` restent indépendantes ; ne les supprime que si tu n’en as plus besoin. Aucun démarrage automatique ni service Windows n’est ajouté.

## Premiers pas

- **Survol** d’une icône : ouvre le panneau après un court délai (60 ms).
- **Clic** sur un module : maintient son panneau ouvert.
- **Clic droit** : position, Personnaliser, masquer, réinitialiser les tokens ou quitter.
- **Glisser une icône** : change son ordre directement dans la barre.
- **Glisser un espace vide** : déplace la barre entière. Elle peut rester au milieu de l’écran.
- **Échap** : annule un déplacement d’icône ou ferme le panneau.
- **F1**, quand la barre a le focus : ouvre/replie le panneau ; ce n’est pas un raccourci global.
- Barre masquée : **double-clique l’icône TokenDock dans la zone de notification Windows**, éventuellement dans les icônes cachées.

## Modules

### Tokens : Codex, Claude Code, OpenCode

Les compteurs lisent les données locales publiées par les outils. Ils ne représentent ni une facture, ni un quota d’abonnement, ni un compteur universel de toute l’activité du compte.

| Source | Emplacement lu |
| --- | --- |
| Codex | `%USERPROFILE%\.codex\sessions` ; variable `CODEX_HOME` prise en compte |
| Claude Code | `%USERPROFILE%\.claude\projects` ; variable `CLAUDE_CONFIG_DIR` prise en compte |
| OpenCode | `%USERPROFILE%\.local\share\opencode\opencode.db` ; variable `XDG_DATA_HOME` prise en compte |

Codex et Claude utilisent les journaux JSONL ; OpenCode est interrogé en SQLite **lecture seule**, environ une fois par seconde. Les chemins et formats des outils peuvent évoluer.

- Les compteurs suivent les événements disponibles pendant l’utilisation ; ils ne reconstituent pas tout l’historique avant le lancement.
- L’interface distingue entrée, sortie et cache lorsque ces valeurs sont disponibles.
- Le graphe fournit des informations au survol.
- La vue globale additionne les outils visibles ; sélectionner une plateforme affiche son détail.
- **Pas de promesse de token par token** : si l’outil publie son usage en fin de réponse, TokenDock ne peut pas connaître les tokens intermédiaires.
- Réinitialiser remet les compteurs de TokenDock à zéro, sans modifier les fichiers des outils.

### Musique

Pochette et titre dans la barre repliée, lecteur intégré au survol, lecture/pause, précédent/suivant et progression. Certaines applications ne publient pas de durée, de pochette ou de commandes compatibles : les fonctions disponibles dépendent du lecteur Windows actif.

### Copies et fichiers temporaires

À activer dans Personnaliser. L’historique du presse-papiers est **local, en mémoire et limité à 20 cartes** : texte, images et fichiers. Il commence après activation, sans import initial du presse-papiers.

- Clique une carte pour copier ; une coche confirme la réussite.
- Glisse des fichiers sur la barre pour les déposer dans Copies.
- Les fichiers déposés sont copiés dans un stockage temporaire ; les originaux ne sont pas déplacés.
- Glisse une carte de fichiers vers l’Explorateur ou une application qui accepte les fichiers.
- La croix apparaît au survol ou au focus clavier pour retirer une seule carte.
- **Effacer** retire les cartes. Les copies temporaires restent jusqu’à la fermeture normale pour ne pas invalider un transfert en cours.
- Limites : 100 fichiers par dépôt, 256 Mo de stockage temporaire par session ; dossiers et points de réanalyse refusés. Texte : 32 768 caractères ; images : environ 4 millions de pixels ; budget mémoire d’historique : 32 Mio.

Un arrêt brutal peut laisser des fichiers temporaires dans `%TEMP%\TokenDock-shelf`. Aucun nettoyage des fichiers d’origine n’est effectué. L’historique ne survit pas au redémarrage.

### Caméra et photos

Ajoute **Caméra** dans Personnaliser, puis Appliquer. Le panneau utilise la caméra couleur par défaut.

- Caméra activée uniquement pendant que son panneau est ouvert et visible.
- Repli, masquage, changement de module ou fermeture libèrent la capture.
- Aucun microphone, enregistrement vidéo ou envoi réseau.
- Aperçu circulaire ; largeur de capture demandée au plus 640 pixels, ratio de la caméra conservé ; affichage plafonné à environ 15 images/s.
- Le déclencheur enregistre un **PNG rond avec transparence**, recadré comme l’aperçu, dans `Images\TokenDock`.
- **Copies s’ouvre immédiatement**, photo en premier et panneau maintenu ouvert. Tu peux copier ou glisser le PNG ailleurs.
- Copies devient accessible avec Caméra sans activer implicitement l’écoute de tout le presse-papiers.
- Retirer la carte ne supprime pas la photo enregistrée.

Le respect de la transparence lors du collage dépend de l’application de destination ; le fichier PNG conserve son canal alpha.

### Chat et RAM

Le chat indique la **RAM physique disponible**, pas seulement la mémoire totalement inutilisée. Il court plus vite lorsque la mémoire disponible diminue.

Dans Personnaliser → Chat · RAM : seuil orange, seuil rouge, vitesse de 0,25× à 2×, RAM simulée avec aperçu animé. Les couleurs évoluent progressivement sous les seuils. Les valeurs simulées ne modifient pas la mémoire réelle. Le panneau RAM affiche aussi les principaux consommateurs lorsque leurs informations sont accessibles.

## Personnalisation

Ouvre le menu clic droit → **Personnaliser**.

- Glisse les éléments de la palette dans l’aperçu, mélange outils, musique, copies, caméra et chat.
- Glisse vers la zone de retrait pour masquer un élément ; les réglages du chat et les options de compteur sont séparés.
- L’orientation de l’aperçu sert à composer la barre ; la position réelle se choisit dans le menu de la barre.
- Active/désactive compteur supérieur, pochette et titre compact.
- **Appliquer** sauvegarde ; **Annuler** abandonne le brouillon. Le réordre directement dans la barre est, lui, enregistré immédiatement.
- Quand Caméra est activée, Copies reste disponible pour recevoir les photos.

### Ajouter une source de tokens JSON

Options → nom de la source → **Connecter** → sélectionne un fichier JSON local. Trois sources personnalisées maximum. Exemple :

```json
{"input":120,"output":45,"cache":0}
```

Les nombres sont des compteurs cumulés mis à jour par ton intégration. Donner un nom à une source ne connecte pas automatiquement un outil. TokenDock lit le fichier ; un script externe doit publier les valeurs réelles. Utilise de préférence une écriture dans un fichier temporaire puis un remplacement atomique pour éviter les lectures partielles. Voir [l’exemple](custom-usage.example.json) et `CustomFeeds` dans [Personalization.cs](next/Personalization.cs).

## Données et confidentialité

| Donnée | Emplacement / comportement |
| --- | --- |
| Préférences et position | `%LOCALAPPDATA%\TokenDock` |
| Compteurs / historique de copies | Mémoire de l’application ; pas de synchronisation cloud |
| Fichiers déposés | `%TEMP%\TokenDock-shelf`, copies indépendantes |
| Photos prises explicitement | Dossier système Images, sous-dossier `TokenDock` |
| Journaux IA | Lus localement, jamais publiés par TokenDock |

Pas de télémétrie applicative, compte serveur ou clé API intégrée. Les marqueurs Windows d’exclusion de l’historique sont respectés lorsqu’ils sont fournis. Les applications sources peuvent néanmoins copier des données sensibles : n’active l’historique que si tu le souhaites. Les outils IA et lecteurs utilisés ont leurs propres politiques de confidentialité.

Le dépôt et les releases n’incluent aucune photo réelle, conversation, préférence utilisateur ou base OpenCode. Les images générées par `--check` sont des fixtures de test.

## Compiler et vérifier

Installe le **SDK .NET 10** sous Windows depuis [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0), puis Git. Le SDK est nécessaire pour développer, pas pour l’archive autonome.

```powershell
git clone https://github.com/Lockxii/TokenDock.git
cd TokenDock
dotnet restore next/TokenDock.Next.csproj
dotnet publish next/TokenDock.Next.csproj -c Release -o artifacts/dev --no-self-contained
dotnet artifacts/dev/TokenDock.Next.dll --check
```

Les contrôles produisent `artifacts/dev/checks.txt` et des rendus PNG hors écran. Au moment de cette publication : **2 130 contrôles réussis**. Ce nombre peut évoluer avec les contributions. `--check` ne prend pas le contrôle du bureau et ne démarre pas une vraie capture caméra ; les tests de copies utilisent des données artificielles. Certains contrôles inspectent les métriques système locales.

Pour lancer manuellement ta compilation, après avoir quitté toute autre instance :

```powershell
.\artifacts\dev\TokenDock.Next.exe
```

Construire une archive autonome avec runtime et empreinte SHA-256 :

```powershell
.\scripts\package.ps1 -Version 0.1.0
```

Le script compile, lance `--check`, puis empaquette uniquement le runtime nécessaire et les assets. Les captures et fichiers de test ne vont pas dans le ZIP. Une compilation CI Windows vérifie aussi les changements poussés sur GitHub.

## Modifier avec une IA

Lis le [guide complet](docs/AI_GUIDE.md) pour Codex, Claude Code, OpenCode ou un autre assistant. Aucune clé ou plugin particulier n’est requis par ce dépôt.

Exemple de demande :

> Lis AGENTS.md et les fichiers concernés. Ajoute un réglage du délai de survol entre 30 et 300 ms dans Personnaliser, avec valeur par défaut de 60 ms. Préserve le debounce, la continuité des animations et les réglages existants. Travaille sur une branche, exécute les contrôles hors écran, ne lance pas l’application sur mon bureau et ne publie rien sans demande explicite. Donne le résumé du diff et les résultats de validation.

Ne transmets pas à une IA tes journaux privés, captures caméra, presse-papiers, secrets ou fichiers de configuration personnels. Le code et les fixtures suffisent pour la plupart des changements.

## Dépannage

| Problème | Vérifications |
| --- | --- |
| Rien ne s’ouvre | Quitte l’ancienne instance via la zone de notification ; conserve tous les fichiers extraits ensemble. |
| Barre masquée | Double-clic sur l’icône TokenDock près de l’horloge Windows. |
| Compteurs à zéro | Vérifie les chemins des sources, leur statut et qu’un nouvel événement d’usage a été publié après lancement. Une source absente n’est pas une consommation nulle confirmée. |
| Caméra indisponible | Paramètres Windows → Confidentialité et sécurité → Caméra : autoriser les applications de bureau ; vérifier la caméra et les applications qui la monopolisent, puis réouvrir le panneau. |
| Musique vide | Le lecteur doit publier une session multimédia Windows ; essayer pendant une lecture active. |
| Dépôt refusé | Active Copies, utilise des fichiers locaux classiques et respecte les limites. Explorateur et TokenDock doivent fonctionner au même niveau de privilège : ne lance pas TokenDock en administrateur. |
| Photo non transparente après collage | Glisse le fichier PNG vers la destination ; certains logiciels aplatissent les images du presse-papiers. |
| Réglages impossibles à enregistrer | Vérifie l’accès à `%LOCALAPPDATA%\TokenDock`, puis réessaie. |
| Animations absentes | TokenDock tient compte du réglage Windows qui désactive les animations. |
| Police/échelle bizarre | Précise dans une issue le bord utilisé, la mise à l’échelle Windows et le nombre d’écrans. |

Pour une issue utile : version, Windows, DPI/écrans, étapes minimales, résultat attendu/obtenu. Floute noms de fichiers, visages et contenu personnel des captures. N’envoie pas tes journaux IA complets.

## Architecture

| Fichier | Rôle |
| --- | --- |
| `next/App.cs` | Fenêtre, timers de survol, notification, intégration des modules, entrée `--check` |
| `next/NotchSurface.cs` | Rendu du rail, géométrie, hit tests, transitions, panneaux RAM/tokens |
| `next/SpringAnimation.cs` | Animation amortie et reprise de vitesse |
| `next/Personalization.cs` | Préférences, ordre global, sources JSON personnalisées |
| `next/PersonalizationView.cs`, `CatSettings.cs` | Éditeur visuel et aperçu RAM |
| `next/IconGesture.cs` | Réordre direct des icônes |
| `Meter.cs`, `MemoryInfo.cs` | Sources IA et RAM système |
| `next/MusicModule.cs`, `MusicPane.cs` | Session multimédia et lecteur |
| `next/ClipboardModule.cs`, `CopyItem.cs`, `FileShelf.cs` | Historique et stockage temporaire |
| `next/InlineModules.cs` | Cartes Copies et hôte musique |
| `next/CameraPane.cs` | Capture, cycle de vie caméra, recadrage rond et photo |
| `next/*Checks.cs` | Contrôles hors écran et fixtures |
| `assets/` | Logos, sprites et icônes distribués |

## Limites et licence

Le projet privilégie les API natives et évite les services permanents, mais **aucune consommation CPU/RAM universelle n’est garantie**. La caméra et certains lecteurs peuvent coûter davantage selon le matériel. Les formats des outils IA, la prise en charge multimédia et les permissions Windows peuvent changer.

Code sous [licence MIT](LICENSE). Voir [les notices des assets et composants tiers](THIRD_PARTY_NOTICES.md). TokenDock est indépendant d’OpenAI, Anthropic, OpenCode et boring.notch.
