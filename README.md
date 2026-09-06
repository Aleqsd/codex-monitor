# Codex Monitor

Vos tâches Codex dans FF14 : état du travail, questions en attente, notifications et quota restant.

![Fenêtre de Codex Monitor](docs/images/tasks.png)
*Aperçu ImGui hors jeu, avec des données fictives.*

## Installation

Version expérimentale pour **Windows et Dalamud 15**. Le relais nécessite **Node.js 22.22.2** et Codex ouvert. Pour le quota, le CLI Codex doit aussi être installé et connecté.

1. Télécharger et extraire l’archive de la [dernière prérelease](https://github.com/Aleqsd/codex-monitor/releases).
2. Lancer `bridge/Start-Bridge.ps1` avec PowerShell.
3. Ajouter le chemin de `plugin/CodexMonitor.dll` aux **Dev Plugin Locations** dans `/xlsettings`, puis activer **Codex Monitor** dans `/xlplugins`.

Pour une mise à jour, désactiver le plugin avant de changer son emplacement. Garder un seul relais et une seule DLL chargée.

## Utilisation

`/codex` ouvre les tâches. `/codex config` règle le HUD, les notifications, les sons et l’apparence. `/codex preview` place les notifications.

Cliquer sur une tâche avec un « ? » permet de masquer sa question dans FF14 ou de la réafficher. Les nouvelles questions restent signalées.

Dans **Notifications → Design**, personnaliser le fond, la transparence, les coins, les icônes et le texte avec un aperçu. LMeter, Obsidienne et Nuit habillent les éléments du plugin ; les réglages gardent une présentation fixe. Expressway est facultative et locale, avec repli Dalamud.

Le relais utilise un protocole interne de Codex qui peut évoluer. Le plugin ne lance ni n’approuve de tâche.

[Compilation et fonctionnement](docs/DEVELOPMENT.md) · [Validation](docs/VALIDATION.md) · [Notifications](docs/NOTIFICATIONS.md)
