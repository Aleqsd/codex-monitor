# Notifications

Dans **Réglages → Notifications**, à la suite des réglages de placement, choisir LMeter, Obsidienne ou Nuit. Fond, opacité, police locale, taille et couleur du texte, contour/ombre, marges et alignement sont propres aux alertes. Les offsets internes déplacent le texte ; les réglages de placement déplacent l’ensemble à l’écran. Un aperçu fidèle et une restauration du thème conservent l’ancre et les autres préférences.


Surface sombre arrondie, symbole circulaire, titre de tâche et projet, statut coloré et barre de durée. Vert pour une réponse prête, ambre pour une réponse ou approbation, rouge pour une erreur.

Le fond, son opacité de 0 à 100 %, le contour, les coins de 0 à 24 px, l’icône et la barre de durée se règlent indépendamment. Police, couleur et taille du texte restent personnalisables. Le panneau de réglages garde une présentation fixe.

## Placer les notifications

1. Saisir `/codex preview`, ou ouvrir `/codex config` → **Notifications** et cliquer sur **Placer les notifications**.
2. Faire glisser la notification d’exemple à la souris. La position est sauvegardée au relâchement.
3. Fermer l’aperçu avec sa croix, le bouton **Terminer le placement**, ou `/codex preview`.

L’ancre est le centre du bord supérieur de la première notification. Elle est enregistrée relativement à la fenêtre de jeu pour suivre les changements de résolution. Les notifications restent dans les limites de l’écran et s’empilent vers le haut quand l’ancre se trouve dans sa moitié inférieure. **Recentrer** restaure la position initiale et ouvre l’aperçu.

Dans les réglages : choisir l’un des cinq exemples, ajuster la taille de 75 à 150 %, la durée de 4 à 15 secondes, ou activer **Réduire les animations**. **Tester une notification** ferme le placement et lance une notification temporaire à l’ancre choisie, avec le son associé si les sons sont actifs.

La version 0.3 ajoute une grille de placement, l’aimantation à cette grille et aux repères du centre/des bords, six boutons d’alignement, ainsi que des décalages X/Y en pixels. Le déplacement à la souris ou un alignement remet ces décalages à zéro. La grille et l’aimantation peuvent être désactivées séparément. L’aperçu montre de une à trois notifications ; déplacer la première positionne tout le groupe. **Empilement** permet de choisir automatique, vers le haut ou vers le bas. Les limites de l’écran restent prioritaires.

Commandes de test : `/codex test`, `/codex test input`, `/codex test approval`, `/codex test error`, `/codex test question`. Elles fonctionnent aussi sans relais et ne changent aucune tâche Codex.

## Questions pendant le travail

L’option **Une question pendant que Codex continue** déclenche une notification « Question posée » et le son d’intervention. Chaque nouvelle question possède un identifiant : une question toujours présente ne déclenche pas d’alertes répétées, mais une deuxième question sur la même tâche est signalée. La pause des notifications et son résumé s’appliquent également.

La tâche garde son état **En cours** et affiche aussi **Question posée**. Elle compte dans les tâches actives et dans celles demandant une intervention. La détection utilise les questions structurées de Codex, sans chercher les points d’interrogation dans les messages ordinaires. La 0.11.0 transmet un extrait borné du titre des questions structurées pour les notifications et aperçus, sans réponse ni sortie d’outil. Il est facultatif et absent de l’historique sauvegardé.

Pour les messages asynchrones, le relais suit le dernier échange confirmé et retire le signal après une réponse acceptée via la carte de question Codex, ou lorsqu’un nouvel échange remplace le précédent. Il suit aussi les demandes non bloquantes exposées par le serveur et leur suppression. Une simple lecture, un brouillon ou une réponse libre sans lien explicite avec la question ne prouve pas que celle-ci est résolue. Le bouton local « Passer » n’est pas exposé par ce flux. Seules les questions visibles dans les données chargées par Codex sont observables.

Si une ancienne question reste affichée alors que sa carte est introuvable dans Codex, cliquer sur la tâche puis **Masquer cette question**. Cela retire son signal du HUD, des compteurs et des notifications, sans répondre dans Codex. Le choix reste enregistré après rechargement ; les nouvelles questions et les interventions bloquantes continuent de s’afficher. Pour annuler, retrouver la tâche dans **Tout afficher** puis cliquer sur **Réafficher les questions masquées**. L’historique indique « Question masquée dans FF14 ».

Cette fonction nécessite de lancer le relais livré avec la version 0.4.0. Une première connexion ou une reconnexion ne rejoue pas les anciennes alertes ; les questions encore présentes restent visibles dans les compteurs.

## Comportement

- Trois notifications visibles au maximum, moins si la hauteur disponible l’exige.
- Survol : suspend la durée. Clic sur le corps : ouvre la liste des tâches. **Ouvrir dans Codex** : ouvre cette tâche dans l’application ; **Voir l’historique** sur un résumé : consulte les événements du plugin. Croix : ferme la notification. Les exemples ne lancent aucune application.
- L’aperçu reste affiché et suspend la présentation des vraies notifications pendant le placement.
- Les notifications en attente commencent leur durée lorsqu’elles apparaissent. La file contient au maximum 20 éléments ; en cas de dépassement, le plus ancien élément après les trois premiers est remplacé.
- Un nouvel état d’une tâche déjà dans la file remplace son ancien message.
- Aucune alerte au premier abonnement ou à la reconnexion. Une coupure du relais efface la file pour éviter d’afficher des états périmés.
- L’aperçu est désactivé à chaque chargement du plugin ; la position et les réglages sont conservés.

## Pause des notifications

Les options **Pendant les combats** et **Pendant les cinématiques** sont activées par défaut. Elles utilisent les conditions exposées par [Dalamud](https://dalamud.dev/api/Dalamud.Game.ClientState.Conditions/Enums/ConditionFlag/). Les notifications déjà visibles sont masquées et leur durée est suspendue. Les nouvelles alertes sont inscrites dans l’historique et différées. Les sons, y compris les écoutes manuelles, sont silencieux ; une lecture déjà en cours est interrompue.

À la sortie, un seul résumé regroupe les réponses prêtes et les demandes encore en attente. Cliquer dessus ouvre l’historique. Les demandes résolues entre-temps ne sont pas comptées comme en attente. Une coupure du relais abandonne le résumé en attente mais conserve l’historique. Le lot différé est limité aux 100 dernières alertes. Les aperçus et notifications de test attendent eux aussi la fin de la pause.

## Historique

`/codex history` ouvre les 100 derniers événements, du plus récent au plus ancien, avec leur heure locale. **Demandes à traiter seulement** filtre les demandes encore actives. **Effacer** vide le journal local.

La version 0.4 avait supprimé les anciens non-lus locaux, qui ne suivaient pas les lectures dans Codex. Depuis la 0.10.0, le relais lit le véritable indicateur `hasUnreadTurn` : « Prêtes » compte les tâches inactives dont la réponse est non lue dans Codex. Le point bleu et la notification de fin disparaissent quand Codex retire cet indicateur. Ce signal ne signifie pas que l’objectif entier est terminé. Il n’existe toujours aucun bouton « Marquer lu » propre au jeu. Lire une question ne la résout pas ; les demandes explicites gardent leur état.

Les anciennes alertes sont distinguées des interventions actuellement en cours. Lorsque le relais ou une tâche n’est plus observable, l’historique indique **État actuel inconnu**. Il conserve aussi les événements dont les popups sont désactivés. Les exemples de test et résumés ne créent pas d’entrées supplémentaires.

L’historique est enregistré dans la configuration locale du plugin, avec seulement les métadonnées déjà reçues du relais. Il reste disponible après un rechargement. Il ne reconstitue pas les événements survenus lorsque le plugin ou le relais était arrêté.

## Mini HUD

Dans `/codex config` → **HUD**, choisir **Mini HUD**, **Texte de la barre** ou **Masqué**. Un aperçu montre le choix en direct. Mini HUD et texte ne s’affichent pas simultanément. Masquer l’indicateur conserve les notifications et l’accès par `/codex`.

Le mini HUD affiche les tâches en cours et les interventions en attente sur deux lignes Obsidienne, avec l’état de connexion et la pause des notifications. Au survol, il montre jusqu’à huit titres, en donnant la priorité aux interventions. Un clic ouvre la liste des tâches.

`/codex hud` bascule entre mini HUD et texte. **Déplacer le mini HUD** permet de le déplacer à la souris ; un clic droit ou **Verrouiller la position** termine le placement. Sa position relative est sauvegardée séparément de celle des notifications. **Recentrer** restaure sa position initiale. La taille va de 75 à 150 % et l’opacité de 35 à 100 %.

## Sons

Dans `/codex config` → **Sons**, choisir le volume général et un son pour **Réponse prête**, **Réponse ou approbation**, et **Erreur**. Trois sons synthétisés sont inclus : **Verre**, **Goutte** et **Velours**, d’environ 0,65 seconde chacun. Par défaut les sons sont actifs à 18 %. Chaque événement peut aussi être silencieux. **Écouter** teste le choix au volume réglé, en respectant la pause des notifications et la désactivation générale.

**Fichier WAV** accepte le chemin absolu d’un fichier local : PCM 16 bits, mono ou stéréo, de 8 à 96 kHz, jusqu’à 3 secondes et 2 Mo. Les erreurs de format et de lecture sont affichées dans les réglages. Le fichier est lu en arrière-plan et son volume est ajusté sans modifier celui du jeu ou de Windows.

Un seul son automatique est joué au plus toutes les deux secondes, sans superposition. Si plusieurs événements arrivent ensemble, l’erreur a priorité, puis la demande d’intervention, puis une réponse prête. Les événements dont les notifications sont désactivées restent silencieux. Le résumé au retour de la pause des notifications produit au plus un son ; aucun son n’est joué au premier abonnement ou à la reconnexion. Les écoutes manuelles peuvent remplacer une écoute en cours.

Le rendu est propre au plugin et ne dépend pas de l’emplacement des Messages du HUD. La DLL 0.4.0 doit être chargée pour utiliser ces ajouts. Le rendu ImGui a été vérifié hors jeu ; l’apparence dans FF14, le confort sonore et la réaction réelle au combat restent à vérifier en jeu.

## Titres et ouverture dans Codex

**Réponse prête** signifie que Codex a fini sa réponse. **Sans activité** signifie que la tâche n’est pas en train de travailler ; cela ne garantit pas que son objectif soit terminé. **À voir** regroupe les demandes de réponse, d’approbation, les questions et les erreurs actuelles.

Les boutons de la liste, de l’historique et des notifications utilisent un lien `codex://threads/<identifiant>`, seulement après un clic et pour un identifiant de tâche valide. Une erreur Windows reste visible ; l’alerte n’est pas supprimée par le bouton. L’application Codex doit être installée avec son association de liens. Le résumé et les aperçus n’inventent aucun identifiant. Le format a été vérifié dans l’application locale ; il peut évoluer avec Codex.

Les emojis sont dessinés en couleur à partir de Segoe UI Emoji installée sur Windows, séparément de la police choisie pour le texte. Les séquences composées restent groupées et la troncature respecte les caractères complets. La couverture dépend de la police Windows, notamment pour les symboles les plus récents ; un losange sert de repli pendant le chargement ou si le rendu échoue. Aucun fichier de police ni pack d’images n’est téléchargé ou distribué.

Le Panneau fin n’a plus de barre de quota. Le texte du quota est vert au-dessus de 50 %, ambre de 20 à 50 %, rouge sous 20 %, et gris si la donnée manque. Les autres styles conservent leur forme ; leurs pourcentages suivent ces mêmes couleurs.

## Ne pas déranger

La cloche du mini HUD, le bouton de la fenêtre ou `/codex dnd` mettent les alertes en pause (30 minutes avec la commande sans argument). Le menu propose 15 minutes, 30 minutes, une heure ou la session. `/codex dnd off` reprend, `/codex dnd session` attend une réactivation. La pause manuelle expire à la déconnexion du personnage.

Les compteurs et l’historique continuent ; notifications et sons, y compris les écoutes d’essai, attendent. La reprise conserve le délai de deux secondes au calme et les règles de combat/cinématique. Les alertes en file et les nouveaux événements sont regroupés ; les questions déjà traitées ne restent pas annoncées comme en attente.

## Suivi et événements rapprochés (0.10.0)

**Réglages → Suivi** sélectionne les projets qui alimentent la liste, le HUD et les nouvelles notifications. Tous les projets restent suivis par défaut. La sélection distingue les dossiers, même si leur nom est identique ; les favoris sont locaux à FF14 et restent soumis à cette sélection. Cliquer sur une tâche permet de la mettre en favori ou de couper ses alertes pendant 30 min ou 1 h. L’état et l’historique restent accessibles ; les alertes anciennes ne sont pas rejouées à la réactivation.

Les notifications ordinaires attendent deux secondes pour regrouper les événements rapprochés d’une même tâche. Les erreurs sont immédiates et prioritaires, puis les demandes d’intervention, puis le quota et les réponses prêtes. Une reprise du travail, une lecture dans Codex ou la résolution d’une demande retire l’alerte devenue obsolète. Ce regroupement peut être désactivé dans Notifications ; la pause au survol et la durée réellement visible sont conservées.

L’historique recherche les titres et les projets, filtre questions/demandes, erreurs, réponses prêtes ou quota, et regroupe les événements par tâche. Les événements anciens restent dans l’historique même si leur projet n’est plus suivi.

Les alertes de quota proposent les seuils 20, 10 et 5 % par défaut, personnalisables de 1 à 99 %. Elles suivent les deux périodes fournies, une fois par seuil et par période identifiée. Plusieurs seuils franchis ensemble donnent une seule alerte pour cette période. Les marques sont sauvegardées pour éviter les répétitions après rechargement. La première lecture et la reconnexion posent une référence silencieuse ; une donnée inconnue, périmée ou sans échéance de période ne déclenche rien. La pause conserve au plus une alerte pertinente par période, avec la valeur actuelle et le délai de renouvellement au retour.

Le modèle et l’effort affichés proviennent en priorité des paramètres de l’exécution chargée, puis du réglage de la tâche quand ces paramètres manquent. Le survol indique cette provenance. Une donnée absente reste « non fourni » ; aucune valeur par défaut n’est inventée. Les tâches et indicateurs anciens continuent à fonctionner avec un relais antérieur, sans prétendre disposer de son point bleu.

## Interface et aperçus (0.11.0)

Ruban, Focus et Tâche épinglée complètent les six formats existants. Focus est le défaut des nouvelles installations ; les préférences déjà sauvegardées sont conservées. Le menu d’une tâche peut choisir le format épinglé et la tâche suivie en une action.

Le clic sur un compteur ouvre un aperçu près du HUD. La liste complète et l’ouverture explicite de la tâche dans Codex restent accessibles. L’aperçu se ferme au clic extérieur et respecte les bords de l’écran. Depuis la 0.11.1, le clic sur le HUD ouvre les réglages par défaut. L’aperçu des tâches est un choix facultatif dans « Au clic sur le HUD ». La cloche reste intégrée dans chaque format et utilise sa propre zone de clic.

La réponse non lue est bleue, l’activité neutre, l’intervention ambre et l’erreur rouge. Le titre de la notification passe avant l’événement. Un extrait de question est limité à deux lignes ; un ancien relais ou une question sans titre structuré conserve l’affichage sans extrait. Résoudre ou masquer la question retire aussi son extrait.
