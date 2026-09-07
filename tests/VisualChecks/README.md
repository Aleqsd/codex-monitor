# Vérifier le rendu hors jeu

Ce petit exécutable crée son propre contexte ImGui. Il lie les composants réels de `src/` et remplace seulement les services de Dalamud et les données de tâches. Aucun accès au jeu, aucune interaction avec le bureau et aucun second relais ne sont nécessaires.

Pré requis : Windows, .NET 10.0.400 et les bibliothèques installées de Dalamud 15.0.3.2. Depuis la racine du dépôt, utiliser le SDK du PATH ou remplacer `dotnet` par `..\.tools\dotnet\dotnet.exe`.

```powershell
$env:DALAMUD_HOME = "$env:APPDATA\XIVLauncher\addon\Hooks\15.0.3.2"
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release -- artifacts/visual
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --visibility-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --navigation-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --navigation-preview artifacts/navigation
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --emoji-raster
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --visibility-preview artifacts/visibility
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --readme-previews artifacts/readme
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --revision-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --revision-preview artifacts/revision
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --interaction-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --hud-interaction-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --hud-animation-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --appearance-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --appearance-preview artifacts/visual
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --quiet-render-smoke
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --motion-preview artifacts/motion
dotnet run --project tests/VisualChecks/VisualChecks.csproj -c Release --no-build -- --audio-smoke
```

La première commande écrit 42 images PPM et trois exemples WAV à 18 % dans `artifacts/visual`. Elle inclut les six HUD en direct, au repos, sans quota, hors ligne et avec de grands compteurs. Le rasteriseur CPU consomme les triangles et la texture de police produits par le vrai ImGui ; il ne remplace pas le rendu du jeu. Les contrôles d’interaction simulent six clics généraux puis cinq actions sur le HUD : style, quota, ouverture, glisser-déposer et verrouillage. Le contrôle audio envoie un son à volume nul à la sortie audio Windows pour vérifier la fin et l’annulation ; aucun son audible n’est nécessaire.

Conversion facultative en PNG si Python et Pillow sont installés :

```powershell
python -c "from PIL import Image; from pathlib import Path; [Image.open(p).save(p.with_suffix('.png')) for p in Path('artifacts/visual').glob('*.ppm')]"
```

Les données sont fictives. Ces contrôles ne prouvent ni l’apparence exacte sous la police et le thème du jeu, ni les interactions avec les conditions réelles de combat. Les bibliothèques Dalamud, les binaires et les campagnes générées restent hors du dépôt. Seules les images fictives sélectionnées pour la documentation vont sous `docs/images/`.

`--appearance-preview` ajoute 15 rendus des six styles avec des fonds et transparences différents, ainsi que la section de réglages. `--appearance-smoke` utilise les contrôles réels (couleur, opacité, contour, réinitialisation, visibilité) puis le sérialiseur Newtonsoft.Json installé dans Dalamud pour vérifier migration et persistance. Définir `DALAMUD_HOME` avant cette vérification.

`--hud-animation-smoke` vérifie les boutons d’essai et de désactivation ; `--quiet-render-smoke` fait avancer le vrai rendu avec un combat simulé, une brève sortie et la reprise complète d’une notification verte. `--motion-preview` produit 42 images à 20 images/s illustrant les changements des compteurs et du quota. Aucune fenêtre du bureau ni tâche Codex n’est manipulée par ces essais.


`--revision-smoke` couvre 13 interactions : masquage/restauration d’une question, navigation vers son design, preset, opacité, bordure, coins, icône, barre de durée, police et taille. Il compare aussi les pixels du panneau de connexion avant/après modification des styles fonctionnels et vérifie migration/persistance avec le sérialiseur installé. `--skin-smoke` ajoute 18 contrôles d’apparence et géométrie à ce parcours. `--revision-preview` produit 32 vues aux échelles 100/150/200 %, largeur minimale, formulaire défilé et notifications minimalistes ou transparentes.

`--skin-preview artifacts/skin` produit 56 vues supplémentaires des données, du HUD et des notifications avec fond clair, transparence totale, grandes polices et police locale alternative. Le conteneur et les réglages gardent leur présentation fixe.

`--readme-previews artifacts/readme` dessine deux planches compactes pour le README : les six mini HUD et trois notifications. Il appelle les fonctions de dessin réelles avec des titres et quotas fictifs ; aucune DLL du plugin n’est recompilée et aucune fenêtre du jeu n’est manipulée.

Le harnais charge Segoe UI et Consolas depuis Windows. Expressway n’est pas présente dans cet environnement : les vues correspondantes montrent le repli, sans prétendre valider son fichier. Le chargement via l’atlas géré de Dalamud reste à vérifier en jeu.

`--navigation-preview` produit 21 vues : tâches avec emojis, historique, réglages des alertes, notifications à 100/150/200 % et Panneau fin avec quatre états de quota. `--navigation-smoke` clique les véritables boutons et vérifie leurs destinations avec un lancement Windows simulé, le résumé, les aperçus inertes et les seuils du quota. `--emoji-raster` utilise réellement DirectWrite/Direct2D/WIC et vérifie six glyphes colorés, dont une séquence avec teinte de peau et jointure. `tests/CoreChecks.csproj --presentation-checks` ajoute 32 contrôles de graphèmes, limites de titres, validation des identifiants, double clic et erreur d’ouverture.

`--automation-preview artifacts/automation` vérifie 78 interactions et invariants supplémentaires : tâche inactive avec question dans la vue filtrée, menu de pause et reprise sur six HUD à 100/150/200 %, 49 rendus de connexion/quota/pause/liste longue et mesure du coût des titres longs. `--automation-config-checks` vérifie trois cas de migration/persistance avec le sérialiseur Dalamud installé. Les mesures CPU hors jeu ne sont pas des mesures de FPS FFXIV.

`--workflow-preview artifacts/workflow` couvre 193 contrôles natifs et 44 vues : 72 clics de compteurs sur les six HUD à 100/150/200 %, menu favoris/silence, vue des réponses prêtes et modèle/effort, historique filtrable, sélection de projets, diagnostic et alertes de quota. Les trois cas de migration passent par Newtonsoft.Json installé avec Dalamud. Les fixtures de diagnostic sont fictives ; le contrôle `--launch-live` vérifie séparément le vrai diagnostic Node/Codex.
