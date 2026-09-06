# Codex Monitor — Dalamud 15

Plugin personnel pour afficher les tâches Codex de ce PC dans FF14. Ce dépôt indépendant rassemble les sources du plugin (`src/`), les contrôles C# (`tests/`) et le relais Windows local (`bridge/`).

## Fonctionnalités

- Fenêtre `/codex`, filtrable par titre ou projet, avec états colorés.
- Compteur dans la barre d'informations : tâches en cours et interventions requises. Cliquer dessus ouvre la fenêtre.
- Notifications lorsqu'un tour se termine ou qu'une tâche demande une réponse/approbation.
- Réglages `/codex config` pour l'affichage, les notifications et le port local.
- Reconnexion automatique et disparition des anciens états lors d'une coupure.

« Au repos » signifie qu'aucun tour n'est en cours. Cela ne prouve pas que tout l'objectif de la tâche est terminé. Aucune notification de fin n'est créée lors de la première connexion ou d'une reconnexion.

## Charger le plugin

Le fichier à charger est `plugin/CodexMonitor.dll`, accompagné des deux fichiers JSON du même dossier.

Dans FF14 :

1. Ouvrir `/xlsettings`, puis la section expérimentale, activer **Enable Developer Mode** et repérer les emplacements de plugins de développement.
2. Ajouter le chemin absolu de `CodexMonitor.dll`, cliquer sur `+` et enregistrer.
3. Ouvrir `/xlplugins` et activer **Codex Monitor** parmi les plugins de développement si nécessaire.
4. Utiliser `/codex` pour afficher ou masquer la fenêtre.

Le relais local doit aussi être lancé : exécuter `bridge/Start-Bridge.ps1` dans une console PowerShell. Il écoute sur `127.0.0.1:43187`. Ne lancer qu’un relais et ne charger qu’un emplacement de Codex Monitor. Désactiver le plugin dans Dalamud avant de remplacer une DLL déjà chargée.

Une fois le relais lancé, il reste autonome : aucune conversation Codex supplémentaire n'est nécessaire pour actualiser les états.

## Arrêt et retrait

- Fermer la fenêtre avec sa croix ou `/codex` ; le compteur et les notifications restent actifs.
- Désactiver le plugin dans `/xlplugins` pour tout arrêter côté jeu.
- Exécuter `Stop-Bridge.ps1` dans le dossier du relais pour arrêter le service local.
- Pour retirer définitivement le plugin, supprimer son emplacement de développement dans les paramètres Dalamud. Les autres plugins ne sont pas concernés.

## Compilation et tests

Version : **0.1.0.0**. Auteur : **Aleqsd**. SDK : **Dalamud.NET.Sdk 15.0.0**, **.NET 10**.

Compilation vérifiée avec les bibliothèques installées de Dalamud **15.0.3.2** : zéro erreur et zéro avertissement.

```powershell
.\Build.ps1 -DalamudHome "$env:APPDATA\XIVLauncher\addon\Hooks\15.0.3.2"
```

Quinze contrôles du client C# couvrent le contrat réel du relais, les états périmés, les coupures réseau, la reprise automatique et les notifications. Le relais doit être lancé pour le dernier contrôle.

```powershell
dotnet run --project .\tests\CoreChecks.csproj -c Release
```

Le script utilise le SDK partagé `../.tools/dotnet/dotnet.exe` lorsqu’il existe, puis celui du `PATH`. Le paramètre `-Dotnet <chemin>` permet un autre emplacement. `global.json` fixe le SDK 10.0.400. Le relais a été validé avec Node.js 22.22.2.

Pour compiler et lancer tous les contrôles, relais déjà actif : `./Build.ps1 -RunChecks`. SDK, caches, DLL générées, configurations locales et données de tâches restent hors de Git.

## Connexion et limites

Le plugin interroge uniquement `http://127.0.0.1:<port>/api/threads`, toutes les deux secondes et en arrière-plan. Les redirections et proxies HTTP sont désactivés. Le rendu ne fait aucun accès réseau. Les tâches ne sont jamais démarrées, interrompues ou approuvées depuis le jeu.

Le relais repose sur le protocole interne de Codex Windows (version de flux 11, application testée 26.901.6511.0). Une mise à jour de Codex peut demander une adaptation du relais. Les tâches sans état confirmé sont masquées par défaut ; elles peuvent être affichées dans les réglages.

Le chargement, le rendu, le compteur, la recherche et la reconnexion ont été observés dans FF14 : voir [la validation](docs/VALIDATION.md). La migration conserve l’exemplaire précédemment chargé à son ancien emplacement ; une compilation dans ce dépôt ne recharge pas le jeu.

Les notifications actuelles utilisent le rendu standard de Dalamud. Les [quatre designs proposés](docs/NOTIFICATIONS.md) attendent le choix de l’utilisateur avant intégration. Les captures et instantanés privés du prototype ne sont pas inclus dans ce dépôt.
