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

Le [relais local se prépare et se lance séparément](bridge/README.md#première-installation), dans un dossier durable hors des dossiers de plugins Dalamud. Il nécessite Node.js 22.22.2 et Codex ouvert ; le quota demande aussi le CLI Codex connecté. Si ton relais fonctionne déjà, garde-le lancé. Une mise à jour Dalamud ne met pas le relais à jour.

## Utilisation

`/codex` ouvre les tâches. `/codex config` règle le HUD, les notifications, les sons et l’apparence. `/codex preview` place les notifications.

Cliquer sur une tâche avec un « ? » permet de masquer sa question dans FF14 ou de la réafficher. Les nouvelles questions restent signalées.

Dans **Notifications → Design**, personnaliser le fond, la transparence, les coins, les icônes et le texte avec un aperçu. LMeter, Obsidienne et Nuit habillent les éléments du plugin ; les réglages gardent une présentation fixe. Expressway est facultative et locale, avec repli Dalamud.

Le relais utilise un protocole interne de Codex qui peut évoluer. Le plugin ne lance ni n’approuve de tâche.

[Compilation et fonctionnement](docs/DEVELOPMENT.md) · [Validation](docs/VALIDATION.md) · [Notifications](docs/NOTIFICATIONS.md)
