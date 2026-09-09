# Validation 0.11.2

Vérifications locales le 9 septembre 2026 : Windows, Dalamud 15.0.3.3 et .NET SDK 10.0.400.

- Compilation sans avertissement ni erreur.
- **448 contrôles typographiques et 55 rendus** : Expressway locale réellement chargée, absence de police forcée, neuf formats à 100/150/200 %, quota, cloche intégrée, clic vers les réglages, détection du fichier et migration avec Newtonsoft.Json fourni par Dalamud.
- **796 contrôles ImGui et 127 rendus** : navigation, pause/reprise, personnalisation, déplacement, bords d’écran, compteurs, aperçu facultatif et comparaison pixel à pixel des réglages fixes.
- Trois planches du README et un aperçu rapproché sont produits par les composants natifs avec des données fictives. Le fichier Expressway utilisé dans les contrôles reste sur le poste ; aucun fichier de police n’est distribué.

Une configuration standard 0.11.1 est migrée vers Expressway 16, texte blanc et contour noir 128/255. Le format, le fond noir à 50 %, l’échelle 1,25, l’ancre et l’opacité du contenu restent identiques. Les polices personnalisées et les choix effectués après migration sont conservés.

Les modules de données et les cinq scripts du relais 0.11.0 restent inchangés. Les campagnes métier de la 0.11.0 (260 contrôles C#, 25 tests Node, 32 Unicode/liens et 16 contrôles du lanceur) sont une référence précédente, sans nouvelle exécution revendiquée pour ce correctif. Aucun jeu ni relais actif n’a été manipulé.

## Limites

L’utilisateur a confirmé le fonctionnement en jeu de la **0.11.1** le 9 septembre. La DLL **0.11.2** n’a pas encore été chargée dans FF14 pendant cette validation. Le harnais utilise ImGui et les vrais fichiers de police locaux ; les services hôtes, l’atlas géré par Dalamud et les lancements de liens sont simulés. Le rendu final en jeu et le rechargement des polices par Dalamud restent à confirmer sur cette version.

Le relais est expérimental et utilise un protocole local interne de Codex. Les limites de fraîcheur, de lecture et d’extraits restent celles décrites dans la documentation du relais. La préparation officielle 0.11.1 et ses releases restent distinctes de ce correctif.

![HUD Expressway](images/hud-readable.png)

Rendu natif ImGui hors jeu, données fictives.
