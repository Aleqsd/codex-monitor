# Prototype Codex → Dalamud

Le branchement à l'application Codex Windows existante est validé : **5 états sur 5 correspondent à ceux renvoyés par les outils de l'application**. Le prototype reçoit également les modifications en direct.

Ce dossier contient le relais local prêt à être consommé par un futur plugin Dalamud. L'interface en jeu reste à développer.

## Utilisation

Prérequis testé : Windows, Codex ouvert, Node.js 22.22.2. Aucun paquet npm à installer.

Dans PowerShell, depuis ce dossier :

```powershell
.\Start-Bridge.ps1
```

Depuis un autre terminal :

```powershell
.\Show-Status.ps1
```

Le relais expose [les états en JSON](http://127.0.0.1:43187/api/threads) et [sa connexion](http://127.0.0.1:43187/health). Le futur plugin pourra lire `/api/threads` en arrière-plan, par exemple toutes les deux secondes, puis afficher les valeurs en mémoire dans sa fenêtre Dalamud.

Pour arrêter un relais déjà lancé :

```powershell
.\Stop-Bridge.ps1
```

Pour une lecture ponctuelle sans service HTTP :

```powershell
node --disable-warning=ExperimentalWarning .\bridge.mjs --once --limit 12
```

Les fichiers `runtime/status.json` et `runtime/events.jsonl` contiennent uniquement la projection des états et les transitions. `initial: true` distingue le premier état reçu d'un véritable changement d'état. Après arrêt normal, `status.json` indique la déconnexion. Pour une intégration en jeu, utiliser le service HTTP : une absence de réponse doit aussi être affichée comme une déconnexion.

## États

| Valeur | Affichage | Signification |
|---|---|---|
| `active` | En cours | Un tour est actif. |
| `needsInput` | Réponse attendue | Codex demande une réponse. |
| `needsApproval` | Approbation attendue | Une approbation est attendue. |
| `idle` | Au repos | Aucun tour actif ; cela ne prouve pas que l'objectif entier est terminé. |
| `error` | Erreur | Codex signale une erreur système. |
| `notLoaded` | Non chargée | État explicitement annoncé par Codex. |
| `unobserved` | Non observée | Aucune fenêtre propriétaire n'a confirmé l'état. |
| `disconnected` | Déconnecté | La connexion à Codex ou à la fenêtre propriétaire est perdue. |

Les états transitoires de connexion et les versions incompatibles sont aussi explicites. Le relais ne déduit jamais qu'une tâche est terminée à partir de l'âge d'un fichier.

## Fonctionnement et limites

- Les identifiants et titres des tâches récentes sont lus dans SQLite en lecture seule. Les fichiers de verrouillage fournissent des identifiants supplémentaires à vérifier ; leur présence n'est pas une preuve d'activité.
- Le relais se connecte au canal Windows `\\.\pipe\codex-ipc`, s'identifie comme `dalamud-status-observer`, découvre la fenêtre propriétaire et s'abonne comme observateur. Il ne démarre aucun nouveau serveur Codex.
- Les seules requêtes émises sont `initialize` et `thread-owner-discovery`. Les autres messages gèrent l'abonnement ; aucune action de lancement, interruption, réponse ou approbation d'une tâche n'est implémentée.
- Le flux interne peut contenir l'historique d'une conversation à l'abonnement. Le relais le reçoit en mémoire puis le jette, et ne conserve que le titre, le projet, le modèle et l'état. L'API et les fichiers produits ne publient pas les messages ni les sorties d'outils.
- La connexion HTTP écoute uniquement sur `127.0.0.1`. Les écritures HTTP et les requêtes issues d'une autre origine web sont refusées. Aucun service cloud ni appel de génération n'est utilisé par ce relais.
- Une vérification du propriétaire a lieu toutes les dix secondes. Les changements d'état arrivent par événements. Une rupture de révision invalide l'état puis demande une nouvelle photographie ; une déconnexion provoque une tentative de reconnexion après trois secondes.
- **Le canal est interne à Codex**, donc susceptible de changer. Version testée : application Windows **26.901.6511.0**, protocole de flux **11**. Une mise à jour pourra nécessiter une adaptation.
- Une première souscription aux cinq tâches a transféré environ **32 Mo** sur le canal local. Le relais occupait environ **54 Mio de RAM** au relevé. C'est une raison de conserver ce traitement hors du processus de FF14 ; l'API destinée au jeu reste une petite réponse JSON.

## Preuves et tests

- Une comparaison horodatée avec les outils natifs de Codex et un véritable passage `active` → `idle` ont été observés le 6 septembre 2026. Les instantanés et journaux privés restent dans l’ancien dossier du prototype, hors du dépôt Git.
- [Validation du plugin et du relais](../docs/VALIDATION.md).
- Six tests couvrent le décodage des trames, les états d'attente, le filtrage des données, les ruptures de révision, l'incompatibilité de version, les transitions, les pertes de propriétaire/connexion et la reconnexion.

```powershell
node --test .\observer.test.mjs
```

Les états `active` et `idle`, les mises à jour du flux et un véritable passage `active` → `idle` ont été vérifiés avec les tâches existantes. Les états d'attente d'une réponse/approbation ont été vérifiés avec des événements simulés. Le relais n'a lancé ni interrompu de tâche. L’affichage de cinq tâches dans FF14, la coupure du relais et sa reconnexion automatique ont également été observés.
