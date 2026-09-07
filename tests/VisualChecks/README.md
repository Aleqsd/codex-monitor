# Vérifier le rendu hors jeu — 0.11.0

Le harnais crée son propre contexte ImGui et lie les composants réels de `src/`. Seuls les services hôtes et les données sont simulés. Aucun contrôle du bureau ou du jeu.

Définir `DALAMUD_HOME` vers les bibliothèques Dalamud installées, puis lancer avec le SDK .NET 10.0.400 :

```powershell
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release -- --ui-preview artifacts/ui-011
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release -- --readme-previews artifacts/readme-011
```

Le premier parcours vérifie 556 interactions et invariants, produit 109 PPM et un relevé de performance. Il couvre les neuf HUD, les clics par compteur, les liens explicites, le pin, les états inconnus, les bords de l’écran, les réglages fixes, les échelles 100/150/200 % et la migration via le sérialiseur de Dalamud.

Le second produit trois planches dessinées par les composants réels. Les PPM peuvent être convertis sans modification en PNG. Les rendus sont hors jeu et fictifs ; ils ne prouvent pas l’intégration pendant une session FFXIV.

Ce parcours est la référence pour l’interface 0.11.0. Les anciennes campagnes avec coordonnées d’onglets sont conservées comme historique de développement et ne valident pas cette nouvelle navigation.

Les bibliothèques, journaux, binaires et campagnes générées restent sous `artifacts/`, hors Git. Expressway n’étant pas installée, son repli est utilisé ; aucun fichier de police n’est redistribué.
