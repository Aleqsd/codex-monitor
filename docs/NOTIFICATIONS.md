# Notifications

Dans **Réglages → Notifications → Design** (également accessible par **Apparence → Notifications**), choisir LMeter, Obsidienne ou Nuit. Fond, opacité, police locale, taille et couleur du texte, contour/ombre, marges et alignement sont propres aux alertes. Les offsets internes déplacent le texte ; les réglages de placement déplacent l’ensemble à l’écran. Un aperçu fidèle et une restauration du thème conservent l’ancre et les autres préférences.


Surface sombre arrondie, symbole circulaire, titre de tâche et projet, statut coloré et barre de durée. Vert pour un tour terminé, ambre pour une réponse ou approbation, rouge pour une erreur.

Le fond, son opacité de 0 à 100 %, le contour, les coins de 0 à 24 px, l’icône et la barre de durée se règlent indépendamment. Police, couleur et taille du texte restent personnalisables. Le panneau de réglages garde une présentation fixe.

## Placer les notifications

1. Saisir `/codex preview`, ou ouvrir `/codex config` → **Notifications** et cliquer sur **Placer les notifications**.
2. Faire glisser la notification d’exemple à la souris. La position est sauvegardée au relâchement.
3. Fermer l’aperçu avec sa croix, le bouton **Terminer le placement**, ou `/codex preview`.

L’ancre est le centre du bord supérieur de la première notification. Elle est enregistrée relativement à la fenêtre de jeu pour suivre les changements de résolution. Les notifications restent dans les limites de l’écran et s’empilent vers le haut quand l’ancre se trouve dans sa moitié inférieure. **Recentrer** restaure la position initiale et ouvre l’aperçu.

Dans les réglages : choisir l’un des quatre exemples, ajuster la taille de 75 à 150 %, la durée de 4 à 15 secondes, ou activer **Réduire les animations**. **Tester une notification** ferme le placement et lance une notification temporaire à l’ancre choisie, avec le son associé si les sons sont actifs.

La version 0.3 ajoute une grille de placement, l’aimantation à cette grille et aux repères du centre/des bords, six boutons d’alignement, ainsi que des décalages X/Y en pixels. Le déplacement à la souris ou un alignement remet ces décalages à zéro. La grille et l’aimantation peuvent être désactivées séparément. L’aperçu montre de une à trois notifications ; déplacer la première positionne tout le groupe. **Empilement** permet de choisir automatique, vers le haut ou vers le bas. Les limites de l’écran restent prioritaires.

Commandes de test : `/codex test`, `/codex test input`, `/codex test approval`, `/codex test error`, `/codex test question`. Elles fonctionnent aussi sans relais et ne changent aucune tâche Codex.

## Questions pendant le travail

L’option **Une question pendant que Codex continue** déclenche une notification « Question posée » et le son d’intervention. Chaque nouvelle question possède un identifiant : une question toujours présente ne déclenche pas d’alertes répétées, mais une deuxième question sur la même tâche est signalée. Le mode discret et son résumé s’appliquent également.

La tâche garde son état **En cours** et affiche aussi le nombre de questions posées. Elle compte dans les tâches actives et dans celles demandant une intervention. La détection utilise les questions structurées de Codex, sans chercher les points d’interrogation dans les messages ordinaires. Le texte des questions et des réponses n’est pas envoyé au jeu.

Pour les messages asynchrones, le relais suit le dernier tour confirmé et retire le signal après une réponse acceptée via la carte de question Codex, ou lorsqu’un nouveau tour remplace le précédent. Il suit aussi les demandes non bloquantes exposées par le serveur et leur suppression. Une simple lecture, un brouillon ou une réponse libre sans lien explicite avec la question ne prouve pas que celle-ci est résolue. Le bouton local « Passer » n’est pas exposé par ce flux. Seules les questions visibles dans les données chargées par Codex sont observables.

Si une ancienne question reste affichée alors que sa carte est introuvable dans Codex, cliquer sur la tâche puis **Masquer cette question**. Cela retire son signal du HUD, des compteurs et des notifications, sans répondre dans Codex. Le choix reste enregistré après rechargement ; les nouvelles questions et les interventions bloquantes continuent de s’afficher. Pour annuler, retrouver la tâche dans **Tout afficher** puis cliquer sur **Réafficher les questions masquées**. L’historique indique « Question masquée dans FF14 ».

Cette fonction nécessite de lancer le relais livré avec la version 0.4.0. Une première connexion ou une reconnexion ne rejoue pas les anciennes alertes ; les questions encore présentes restent visibles dans les compteurs.

## Comportement

- Trois notifications visibles au maximum, moins si la hauteur disponible l’exige.
- Survol : suspend la durée. Clic sur le corps : ouvre la liste des tâches. Croix : ferme la notification.
- L’aperçu reste affiché et suspend la présentation des vraies notifications pendant le placement.
- Les notifications en attente commencent leur durée lorsqu’elles apparaissent. La file contient au maximum 20 éléments ; en cas de dépassement, le plus ancien élément après les trois premiers est remplacé.
- Un nouvel état d’une tâche déjà dans la file remplace son ancien message.
- Aucune alerte au premier abonnement ou à la reconnexion. Une coupure du relais efface la file pour éviter d’afficher des états périmés.
- L’aperçu est désactivé à chaque chargement du plugin ; la position et les réglages sont conservés.

## Mode discret

Les options **Pendant les combats** et **Pendant les cinématiques** sont activées par défaut. Elles utilisent les conditions exposées par [Dalamud](https://dalamud.dev/api/Dalamud.Game.ClientState.Conditions/Enums/ConditionFlag/). Les notifications déjà visibles sont masquées et leur durée est suspendue. Les nouvelles alertes sont inscrites dans l’historique et différées. Les sons, y compris les écoutes manuelles, sont silencieux ; une lecture déjà en cours est interrompue.

À la sortie, un seul résumé regroupe les tours terminés et les demandes encore en attente. Cliquer dessus ouvre l’historique. Les demandes résolues entre-temps ne sont pas comptées comme en attente. Une coupure du relais abandonne le résumé en attente mais conserve l’historique. Le lot différé est limité aux 100 dernières alertes. Les aperçus et notifications de test attendent eux aussi la fin du mode discret.

## Historique

`/codex history` ouvre les 100 derniers événements, du plus récent au plus ancien, avec leur heure locale. **Interventions en cours seulement** filtre les demandes encore actives. **Effacer** vide le journal local.

La version 0.4 supprime les non-lus et les boutons « Marquer lu ». Le relais ne fournit pas les lectures effectuées dans Codex : un compteur local pouvait donc rester allumé après lecture sur PC. Les indicateurs suivent maintenant uniquement les tâches actives et les interventions actuellement requises. Lire une demande ne la résout pas ; répondre, approuver ou reprendre la tâche fait évoluer son état. Les anciennes marques de lecture sont ignorées à la migration et les événements sont conservés. Les notifications restent temporaires et ne demandent aucun acquittement.

Les anciennes alertes sont distinguées des interventions actuellement en cours. Lorsque le relais ou une tâche n’est plus observable, l’historique indique **État actuel inconnu**. Il conserve aussi les événements dont les popups sont désactivés. Les exemples de test et résumés ne créent pas d’entrées supplémentaires.

L’historique est enregistré dans la configuration locale du plugin, avec seulement les métadonnées déjà reçues du relais. Il reste disponible après un rechargement. Il ne reconstitue pas les événements survenus lorsque le plugin ou le relais était arrêté.

## Mini HUD

Dans `/codex config` → **Affichage**, choisir **Mini HUD**, **Texte de la barre** ou **Masqué**. Un aperçu montre le choix en direct. Mini HUD et texte ne s’affichent pas simultanément. Masquer l’indicateur conserve les notifications et l’accès par `/codex`.

Le mini HUD affiche les tâches en cours et les interventions en attente sur deux lignes Obsidienne, avec l’état de connexion et le mode discret. Au survol, il montre jusqu’à huit titres, en donnant la priorité aux interventions. Un clic ouvre la liste des tâches.

`/codex hud` bascule entre mini HUD et texte. **Déplacer le mini HUD** permet de le déplacer à la souris ; un clic droit ou **Verrouiller la position** termine le placement. Sa position relative est sauvegardée séparément de celle des notifications. **Recentrer** restaure sa position initiale. La taille va de 75 à 150 % et l’opacité de 35 à 100 %.

## Sons

Dans `/codex config` → **Sons**, choisir le volume général et un son pour **Tour terminé**, **Réponse ou approbation**, et **Erreur**. Trois sons synthétisés sont inclus : **Verre**, **Goutte** et **Velours**, d’environ 0,65 seconde chacun. Par défaut les sons sont actifs à 18 %. Chaque événement peut aussi être silencieux. **Écouter** teste le choix au volume réglé, en respectant le mode discret et la désactivation générale.

**Fichier WAV** accepte le chemin absolu d’un fichier local : PCM 16 bits, mono ou stéréo, de 8 à 96 kHz, jusqu’à 3 secondes et 2 Mo. Les erreurs de format et de lecture sont affichées dans les réglages. Le fichier est lu en arrière-plan et son volume est ajusté sans modifier celui du jeu ou de Windows.

Un seul son automatique est joué au plus toutes les deux secondes, sans superposition. Si plusieurs événements arrivent ensemble, l’erreur a priorité, puis la demande d’intervention, puis la fin d’un tour. Les événements dont les notifications sont désactivées restent silencieux. Le résumé au retour du mode discret produit au plus un son ; aucun son n’est joué au premier abonnement ou à la reconnexion. Les écoutes manuelles peuvent remplacer une écoute en cours.

Le rendu est propre au plugin et ne dépend pas de l’emplacement des Messages du HUD. La DLL 0.4.0 doit être chargée pour utiliser ces ajouts. Le rendu ImGui a été vérifié hors jeu ; l’apparence dans FF14, le confort sonore et la réaction réelle au combat restent à vérifier en jeu.
