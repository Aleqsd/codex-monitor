# Codex Monitor

<img src="https://raw.githubusercontent.com/Aleqsd/dalamud-plugins/main/icons/CodexMonitor.png" width="64" height="64" alt="Icône Codex Monitor">

Vos tâches Codex dans FF14 : état du travail, questions en attente, notifications et quota restant.

![Fenêtre de Codex Monitor](docs/images/tasks.png)
*Aperçu ImGui hors jeu, avec des données fictives.*

## Installation

Version expérimentale pour **Windows et Dalamud 15**, disponible dans le dépôt personnalisé d’Aleqsd.

1. Dans les paramètres Dalamud (`/xlsettings`), ouvrir **Dépôts de plugins personnalisés**.
2. Ajouter cette URL, activer la ligne et enregistrer :

```text
https://raw.githubusercontent.com/Aleqsd/dalamud-plugins/main/repo.json
```

3. Dans `/xlplugins`, chercher **Codex Monitor** et cliquer sur **Installer**. Les versions ajoutées à ce catalogue se mettent ensuite à jour depuis Dalamud.

Si tu utilisais une DLL de développement, désactive cette copie et retire uniquement son entrée de **Dev Plugin Locations** avant l’installation. Conserve tes fichiers de configuration et une seule copie chargée.

À partir de la version **0.7.0**, ouvrir **Réglages → Connexion → Lancer le relais**. Les scripts du relais sont inclus dans le plugin ; Node.js 22.22.2 minimum et Codex restent nécessaires, ainsi qu’un CLI Codex connecté pour le quota. Le démarrage est manuel, sans fenêtre PowerShell. Un relais déjà actif peut rester lancé. [Fonctionnement et lancement séparé](bridge/README.md).

## Utilisation

`/codex` ouvre les tâches. `/codex config` règle le HUD, les notifications, les sons et l’apparence. `/codex preview` place les notifications.

La fenêtre reste fermée au chargement. Dans **Réglages → Visibilité**, choisir quand masquer le plugin : écran titre, chargements, cinématiques et mode photo par défaut ; combats et instances en option. Les alertes de l’écran titre ne sont pas rejouées à la connexion.

Cliquer sur une tâche avec un « ? » permet de masquer sa question dans FF14 ou de la réafficher. Les nouvelles questions restent signalées.

Dans **Notifications → Design**, personnaliser le fond, la transparence, les coins, les icônes et le texte avec un aperçu. LMeter, Obsidienne et Nuit habillent les éléments du plugin ; les réglages gardent une présentation fixe. Expressway est facultative et locale, avec repli Dalamud.

Le relais utilise un protocole interne de Codex qui peut évoluer. Le plugin ne lance ni n’approuve de tâche.

## Mini HUD

Six formats, du simple texte au panneau compact, avec les tâches actives, les questions et le quota restant.

![Les six formats de mini HUD : Fil, Capsule, Balise, Liseré, Totem et Panneau fin](docs/images/mini-huds.png)

## Notifications

Fin d’un tour, question posée ou résumé au retour du combat. Voici les thèmes LMeter, Obsidienne et Nuit :

![Aperçus de notifications : tour terminé, question posée et résumé pendant votre absence](docs/images/notifications.png)

*Ces aperçus proviennent des composants ImGui réels, hors jeu, avec des données fictives.*

[Compilation et fonctionnement](docs/DEVELOPMENT.md) · [Validation](docs/VALIDATION.md) · [Notifications](docs/NOTIFICATIONS.md)
