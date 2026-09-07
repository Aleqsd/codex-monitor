# Développement

## Compiler

Windows, .NET SDK 10.0.400 et bibliothèques Dalamud 15 sont nécessaires. `Version.props` centralise la version des assemblies ; `global.json` fixe le SDK ; aucune dépendance au dossier parent n’est requise. `Build.ps1` utilise `dotnet` du PATH, ou un SDK partagé optionnel s’il existe. `-Dotnet` accepte un chemin explicite.

```powershell
.\Build.ps1 -DalamudHome "$env:APPDATA\XIVLauncher\addon\Hooks\15.0.3.2" -RunChecks
```

Le contrôle de connexion réel nécessite un relais déjà actif sur le port 43187. Le script ne démarre pas un second relais réel. Les tests du lanceur utilisent un service Node fictif sur un port libre, sans accès à Codex. Le résultat se trouve dans `plugin/` : DLL (avec les cinq scripts du relais), manifeste et fichier `.deps.json`.

Si le port 43187 est libre, le contrôle explicitement demandé `dotnet run --project tests/CoreChecks.csproj -c Release -- --launch-live` lance les vrais scripts intégrés pour la durée du test et les arrête ensuite. Il refuse un port déjà occupé. Les traces restent dans `artifacts/`, ignoré par Git.

[Contrôles visuels hors jeu](../tests/VisualChecks/README.md).

## Charger une compilation locale

Désactiver la copie installée de Codex Monitor avant un essai local. Dans `/xlsettings`, ajouter le chemin de `plugin/CodexMonitor.dll` à **Dev Plugin Locations**, puis activer le plugin dans `/xlplugins`. Garder les deux fichiers JSON près de la DLL, la configuration existante et une seule copie chargée.

Pour revenir à l’installation normale, désactiver cette copie de développement et retirer uniquement son entrée, puis installer depuis le [dépôt personnalisé](../README.md#installation). Le [relais intégré](../bridge/README.md) utilise le dossier de configuration du plugin ; un relais externe conserve son propre dossier.

## Architecture

Le relais Node observe les tâches locales et expose seulement leurs métadonnées utiles via HTTP loopback. Le client C# actualise son état en arrière-plan toutes les deux secondes. Le dessin ImGui ne fait aucun accès réseau. Les états inconnus, déconnectés ou périmés ne deviennent jamais une réussite.

Le quota provient du compte connecté au CLI Codex. Un auxiliaire géré appelle uniquement `account/rateLimits/read`, chaque minute. La période hebdomadaire est privilégiée et la valeur expire après deux minutes. Le compte CLI peut différer de celui de l’application. Une DLL récente ne suffit pas si l’ancien relais ne fournit pas le quota.

`SurfaceAppearance` et `HudAppearance` conservent des préférences séparées pour la liste des tâches, le HUD et les notifications. Le champ historique `WindowAppearance` concerne uniquement les données : `ObsidianTheme.Chrome` fixe le conteneur, la navigation et les réglages, sans appliquer les anciennes préférences à leur rendu. Une ancienne configuration reçoit les valeurs compatibles avec Obsidienne ; seul l’absence de configuration utilise LMeter. Les presets et leur restauration ne modifient pas les ancres, l’historique ou les sons.

Les couleurs sémantiques restent centralisées dans `ObsidianTheme`, dont le nom historique ne désigne plus le seul thème disponible. L’accent n’est pas une couleur de job. Le fond et les textes ont des opacités indépendantes. Les offsets internes réservent de l’espace pour éviter de couper le texte ; le placement à l’écran reste distinct.

`QuestionDismissals` masque localement des identifiants précis et mémorise ce choix. La projection est mise en cache et partagée par les compteurs, le HUD, les notifications et l’historique. Elle préserve les interventions bloquantes et les nouvelles questions. Une restauration ne rejoue pas la notification. Aucune réponse ou modification de tâche n’est envoyée à Codex.

`CodexTaskLink` construit uniquement des liens `codex://threads/<UUID>` après validation. Le lancement passe par l’association de protocole Windows au clic explicite, hors du dessin par frame. Le format a été confirmé dans l’application installée ; il ne constitue pas une API publique garantie.

## Polices et licences

`UnicodeText` découpe les titres par graphèmes. `EmojiText` mesure les segments de texte et les emojis avec la même largeur que le rendu. `EmojiImages` prépare en arrière-plan un cache borné à 256 textures et les libère au déchargement. La rasterisation logicielle passe par DirectWrite, Direct2D et WIC, avec [l’option de polices en couleur de Microsoft](https://learn.microsoft.com/en-us/windows/win32/directwrite/color-fonts). Les bindings TerraFX sont ceux déjà fournis par Dalamud, sans copie dans le paquet.

Le code de ce dépôt, y compris les cinq scripts du relais embarqués dans la DLL, est sous licence MIT et a été développé avec l’aide substantielle de Codex. Node.js et le CLI restent des prérequis externes. Les sons intégrés sont synthétisés par le plugin. Les images de démonstration sont des rendus des composants réels avec données fictives.

Aucun fichier de police, asset de jeu, capture LMeter ou binaire tiers n’est distribué. Expressway est recherchée par nom de fichier dans les dossiers de polices Windows et utilisateur ; un autre emplacement peut être choisi via « Fichier local ». Segoe UI reste aussi un choix local. Si la police est absente ou ne se charge pas, Dalamud sert de repli. Le sélecteur montre ce statut.

Le chargement utilise l’atlas de polices géré de Dalamud, hors des callbacks de dessin, après validation du réglage. Les glyphes manquants sont complétés à partir de la police par défaut. Aucun droit de redistribution d’Expressway n’a été établi : ne pas l’ajouter à une archive sans vérifier sa licence auprès de son éditeur.

Dalamud, son SDK et ses bindings sont des dépendances externes requises pour compiler/exécuter le plugin ; leurs fichiers ne sont pas inclus dans les releases. Node.js, le CLI Codex et leurs licences restent fournis par leurs distributeurs.

## Diffusion

Le dépôt ne contient aucun workflow GitHub Actions. Les releases sont préparées localement : tests pertinents, DLL et manifeste de même version, archive sans `runtime`, logs, configuration utilisateur, SDK ou cache. Les notes restent séparées du diagnostic local d’une installation.

Le [catalogue Dalamud d’Aleqsd](https://github.com/Aleqsd/dalamud-plugins) est publié séparément par sa tâche de maintenance. Lui transmettre pour chaque nouvelle release le tag, le commit exact, le nom d’archive, les SHA256 de l’archive et de la DLL, la version et un court changelog. Une release GitHub ne met pas automatiquement le catalogue à jour. Les archives existantes restent immuables.

## Pause, lancement et rendu

`ManualQuietMode` reste limité à la session. La pause manuelle rejoint `QuietModeGate`, qui attend deux secondes au calme avant la reprise. `NotificationCenter.BeginManualPause` regroupe les alertes déjà en file ; les événements suivants alimentent le même résumé.

`RelayAutoStart` évalue l’option après connexion et chargement, respecte un arrêt manuel et borne les tentatives. `RelayLauncher` garde les accès système sur son worker ; `RelayRuntimeStorage` ne nettoie que les dossiers plats reconnus et marqués après arrêt confirmé. Le relais limite ses journaux avec `files.mjs`.

`TaskListProjection` conserve les tâches à voir malgré le filtre d’inactivité. `ImGuiListClipper` limite le rendu aux lignes visibles. Les caches de texte sont bornés avec éviction progressive ; le cache de mesure est invalidé lors du rafraîchissement des polices. Une erreur d’ouverture est liée à sa tâche pendant vingt secondes et peut être fermée ou retentée. Les emojis disposent de trois tentatives espacées et d’un diagnostic de repli.
