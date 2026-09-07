# Relais local

Le relais suit les tâches de Codex sur ce PC et fournit leurs états au plugin FF14.

## Première installation

Prérequis : Windows, Node.js 22.22.2 minimum et Codex ouvert. Aucun paquet npm propre au relais. Le quota demande aussi un CLI Codex installé et connecté.

Avec Codex Monitor **0.7.0 ou plus récent**, ouvrir `/codex config` → **Connexion** → **Lancer le relais**. Les cinq scripts MIT de ce dépôt sont inclus dans la DLL. Le plugin les extrait au clic dans son dossier de configuration Dalamud, sous `CodexMonitor/relay/bridge/<empreinte>/`, puis lance Node.js en arrière-plan. Aucune console et aucun téléchargement. Depuis la 0.9.0, une option désactivée par défaut permet le lancement automatique après connexion au personnage et fin du chargement. Un arrêt manuel est respecté jusqu’à la prochaine connexion ; les échecs transitoires font au maximum trois tentatives espacées. Node.js est détecté dans son emplacement Windows habituel ou dans le PATH ; son chemin peut être indiqué dans « Node.js introuvable ? ».

**Arrêter mon relais** arrête uniquement l’instance créée par le plugin. Elle est également arrêtée au déchargement du plugin ; une surveillance du processus parent est prévue pour la fermeture du jeu. Chaque lancement possède son dossier `relay/runtime/<identifiant>/` et son signal d’arrêt. Un relais préexistant est conservé et reste géré séparément. Un port occupé par un service non reconnu bloque le lancement.

Les scripts sont embarqués, **pas Node.js ni le CLI Codex**. Les données `runtime/` restent privées. Les copies extraites sont identifiées par leur contenu ; une copie modifiée est refusée. Aucun accès réseau ou disque ne se fait dans le dessin ImGui.

## Lancement séparé, facultatif

Télécharger le ZIP complet de la [dernière release Codex Monitor](https://github.com/Aleqsd/codex-monitor/releases), puis l’extraire dans un dossier durable de ton choix, hors des dossiers de plugins gérés par Dalamud. Le sous-dossier `bridge/` contient le relais ; il conserve ses fichiers `runtime/` localement.

Ouvrir PowerShell dans ce sous-dossier et lancer :

```powershell
.\Start-Bridge.ps1
```

Il écoute sur `127.0.0.1:43187`. `Stop-Bridge.ps1` l’arrête et `Show-Status.ps1` montre son état. Ne lancer qu’une instance. `Start-Bridge.ps1 -CodexExe 'C:\chemin\codex.exe'` permet une installation du CLI différente du PATH.

## Mises à jour

Si le relais fonctionne déjà, il peut rester lancé. Une mise à jour du plugin apporte les scripts intégrés pour son prochain lancement. Pour un relais lancé séparément, arrêter l’ancienne instance avec son `Stop-Bridge.ps1`, puis utiliser le bouton du plugin ou le nouveau dossier externe. Garder une seule instance et ne pas déplacer un dossier dont le relais tourne encore.

Le script optionnel `Activer-Quota.ps1` peut remplacer un ancien relais reconnu dans une livraison voisine. Il refuse de modifier un processus inconnu. Sa syntaxe est vérifiée ; sa bascule automatique reste non validée en conditions réelles. L’arrêt et le démarrage séparés sont disponibles.

## Données

SQLite est ouvert en lecture seule. Le canal local interne `codex-ipc` confirme les propriétaires et les états. Les titres, projets, modèles et identifiants restent locaux ; le contenu des conversations, questions, réponses et sorties d’outils n’est pas transmis au jeu. Les fichiers `runtime/` et les journaux sont privés et ignorés par Git.

Les questions structurées sont suivies même si le travail continue. Une lecture dans Codex n’est pas exposée comme un état « lu » : le plugin ne crée pas de compteur de non-lus.

Le quota expose sa date de lecture et au plus deux fenêtres de limites. Il utilise le compartiment principal Codex, jamais Spark à sa place. Les identités, crédits et données de paiement ne sont pas exportés. Une erreur ou une donnée trop ancienne masque la valeur sans interrompre le suivi des tâches.

La connexion HTTP est limitée à loopback, sans écriture ni requête d’une autre origine web. Le protocole interne de Codex peut changer ; une incompatibilité invalide les états au lieu de conserver de fausses tâches actives.

## Tests

```powershell
node --test .\observer.test.mjs .\usage.test.mjs
```

[Validation et limites connues](../docs/VALIDATION.md).

Les journaux sont limités à deux fichiers de 1 Mio. Le plugin conserve trois anciens dossiers d’exécution terminés lors du prochain lancement ; seuls les dossiers reconnus, marqués après arrêt confirmé et sans contenu étranger sont nettoyés. Les anciens dossiers non marqués et les relais externes restent intacts. Les diagnostics de quota transmettent uniquement une catégorie de problème, jamais le texte brut des erreurs ou des identifiants de compte.
