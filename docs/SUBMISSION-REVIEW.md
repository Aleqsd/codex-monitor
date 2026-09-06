# Notes pour la revue

Préparation de soumission, version 0.6.1.0 conservée. Cette branche n’est pas une nouvelle release ; ses binaires ne remplacent pas les fichiers déjà distribués.

## Développement et ressources

OpenAI Codex a écrit une grande partie du code, notamment pendant des passes autonomes : niveau déclaré **Auto (OpenAI Codex)**. Le développeur a dirigé le produit par ses idées et retours, et confirme avoir testé le plugin en jeu. Cela ne signifie ni revue humaine de chaque ligne, ni essai en jeu du futur commit de préparation.

L’[icône originale et sa source SVG](https://github.com/Aleqsd/dalamud-plugins/tree/main/icons) ont été créées avec l’aide de Codex et sont sous la [licence MIT du catalogue](https://github.com/Aleqsd/dalamud-plugins/blob/main/LICENSE). Les trois sons intégrés sont procéduraux : [`SoundClip.Synthesize`](../src/SoundClip.cs) produit les échantillons PCM à partir de fréquences et d’enveloppes. Ce sont du code de synthèse et des données calculées, sans enregistrement importé. Les [options audio](../src/PresentationOptions.cs) permettent silence, volume et fichiers WAV personnels.

## Ce que lit le relais

- [`bridge.mjs`](../bridge/bridge.mjs) ouvre la base locale `state_*.sqlite` la plus récente en lecture seule, sous `CODEX_HOME` ou `%USERPROFILE%/.codex`. Il extrait identifiants, titres, chemins de projet et modèles des tâches non archivées, et consulte les noms des fichiers de `thread-writer-locks` pour compléter le catalogue. Un verrou seul ne prouve pas une activité.
- [`observer.mjs`](../bridge/observer.mjs) suit le canal Windows `\\.\pipe\codex-ipc`, protocole interne 11. Les demandes autorisées sont `initialize` et `thread-owner-discovery`, avec abonnements/désabonnements au flux des tâches. Ce flux peut contenir l’état complet d’une conversation ; [`questions.mjs`](../bridge/questions.mjs) le réduit à la structure utile et à des identifiants hachés. Aucun corps de message, question, réponse ou sortie d’outil n’est conservé dans la projection ni envoyé au jeu. `runtime/status.json` et `events.jsonl` contiennent des métadonnées privées, dont les titres et chemins de projets.
- [`usage.mjs`](../bridge/usage.mjs) lance un auxiliaire CLI Codex en `app-server --listen stdio://`, sans port supplémentaire. Il demande seulement l’initialisation et `account/rateLimits/read`, chaque minute, avec l’authentification existante du CLI. Il ne lance pas de tâche, de connexion de compte ou de réinitialisation de quota. Seules les fenêtres du quota principal et leur fraîcheur sont exposées ; identités et crédits sont exclus.

## Réseau et installation

Le serveur écoute uniquement sur `127.0.0.1:43187`. Il expose **GET `/api/threads`** et **GET `/health`**, refuse les autres méthodes/routes, vérifie `Host` et refuse une origine web différente. Il n’a pas de jeton d’authentification : un processus local peut lire ces métadonnées. Le [client du plugin](../src/BridgeClient.cs) consulte `/api/threads` toutes les deux secondes, sans proxy ni redirection, avec délai de deux secondes et réponse limitée à 1 Mo. Les données périmées sont invalidées.

Pour la revue, suivre le [guide du relais](../bridge/README.md#première-installation) : Windows, Node.js 22.22.2, application Codex ouverte, puis `bridge/Start-Bridge.ps1` depuis un dossier durable séparé de Dalamud. Pour le quota, installer et connecter le CLI Codex. Un relais déjà actif peut rester lancé. Le port se change avec `-Port` et dans **Réglages → Connexion** du plugin ; `-CodexExe` sélectionne le CLI. L’exécution directe de `bridge.mjs` accepte aussi `--codex-home` et `--output`. Ne lancer qu’une instance. `Stop-Bridge.ps1` permet l’arrêt prévu.

Le [build](../Build.ps1) restaure en mode verrouillé puis compile en Release. [`global.json`](../global.json) fixe .NET SDK 10.0.400, le [projet](../src/CodexMonitor.csproj) utilise Dalamud.NET.Sdk 15.0.0 et le [lock](../src/packages.lock.json) fixe les dépendances. Dans une copie isolée, passer `-Dotnet` et `-DalamudHome` explicitement si nécessaire. Les [instructions de développement](DEVELOPMENT.md#charger-une-compilation-locale) décrivent le chargement de la DLL construite, avec une seule copie de Codex Monitor active.

## Limites de l’observation

L’observation dépend d’interfaces internes Codex susceptibles de changer et des tâches chargées/observables sur ce PC. « Au repos » ne signifie pas objectif accompli. La lecture d’une réponse dans Codex et le bouton local « Passer » ne sont pas exposés ; une question peut être masquée seulement dans FF14. Le compte CLI peut différer de celui de l’application et un quota absent/périmé reste inconnu. Le plugin n’envoie aucune action aux tâches. Les essais hors jeu et la compilation locale ne prouvent pas le résultat du pipeline officiel D17 ni un essai en jeu de ce commit.
