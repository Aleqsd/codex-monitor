# Validation 0.6.0

Prérelease expérimentale. Vérifications locales le 6 septembre 2026 sur Windows, Dalamud 15.0.3.2, .NET SDK 10.0.400 et Node.js 22.22.2.

- Compilation : zéro avertissement, zéro erreur.
- 124 contrôles C# et 20 tests Node passent. Ils couvrent les états, les questions structurées, le quota, les coupures/reconnexions, les sons, les timers et la reprise après le mode discret. La lecture HTTP du relais déjà actif est acceptée par le parseur.
- 24 interactions ImGui hors jeu passent : navigation, modes d’indicateur, audio, styles du HUD, quota, ouverture/déplacement/verrouillage, animations, couleur, opacité, visibilité, contour, restauration, preset, police et taille.
- 18 contrôles supplémentaires vérifient normalisation, dimensions et migrations d’apparence avec le sérialiseur Newtonsoft.Json installé dans Dalamud. Les deux contrôles précédents de migration/persistance du fond passent aussi.
- 113 images ImGui hors jeu : 56 vues des skins et polices, 42 vues des parcours et six HUD, 15 vues des fonds. Les vues de skin couvrent 100 %, 150 % et 200 %, largeur minimale, texte long, grande police, fonds clair/sombre et transparence nulle. Les données sont fictives.
- Le vrai composant de notification est exécuté avec des conditions de combat simulées : une alerte interrompue retrouve sa durée complète et expire normalement.

Les rendus utilisent les composants C# réels et un rasteriseur CPU des triangles ImGui. Les services hôtes sont simulés, avec Segoe UI et Consolas installées localement. Ils ne constituent pas un essai dans FF14.

## Limites

La DLL 0.6.0 n’a pas été chargée ou testée en jeu pendant cette validation. Le chargement et le cycle de vie des polices via l’atlas géré de Dalamud restent à vérifier pendant une session. Expressway n’était pas disponible : le repli est montré, son fichier n’est ni testé ni distribué.

Le remplacement automatique d’un ancien relais par `Activer-Quota.ps1` a seulement été vérifié syntaxiquement. Aucun relais actif n’a été redémarré pour cette évolution visuelle. Un quota affiché « — » peut provenir d’un ancien relais, d’un CLI absent/non connecté ou d’une lecture périmée ; le diagnostic distingue l’ancien relais.

Le protocole interne de Codex Windows peut évoluer. Les fonctions de contrôle de tâches restent hors périmètre : le plugin observe, sans lancer, interrompre, répondre ou approuver.

## Images

![Fenêtre](images/tasks.png)

![Réglages d’apparence](images/appearance.png)

![Formats du HUD](images/hud.png)

Aperçus ImGui hors jeu avec données fictives ; aucune capture LMeter ou tâche réelle n’est publiée.
