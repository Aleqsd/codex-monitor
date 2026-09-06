# Relais local

Le relais suit les tâches de Codex sur ce PC et fournit leurs états au plugin FF14.

## Démarrer

Prérequis : Windows, Node.js 22.22.2 et Codex ouvert. Aucun paquet npm propre au relais. Le quota demande aussi un CLI Codex installé et connecté.

```powershell
.\Start-Bridge.ps1
```

Il écoute sur `127.0.0.1:43187`. `Stop-Bridge.ps1` l’arrête et `Show-Status.ps1` montre son état. Ne lancer qu’une instance. `Start-Bridge.ps1 -CodexExe 'C:\chemin\codex.exe'` permet une installation du CLI différente du PATH.

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
