# Validation 0.11.1

Vérifications locales le 7 septembre 2026 : Windows, Dalamud 15.0.3.2 et .NET SDK 10.0.400.

- Compilation sans avertissement ni erreur.
- **796 contrôles ImGui et 127 rendus** : neuf HUD à 100/150/200 %, ouverture des réglages depuis le fond, les compteurs, le quota et la tâche épinglée ; aperçu des tâches facultatif ; pause et reprise depuis la cloche intégrée.
- La cloche reste dans la surface et sans chevauchement avec les compteurs, avec ou sans quota, marges, décalage du texte et taille personnalisée. Le glisser en mode placement et le clic droit pour verrouiller restent fonctionnels.
- Migration avec Newtonsoft.Json fourni par Dalamud : une configuration 0.11.0 contenant `HudQuickPeek: true` revient aux réglages au clic, sans changer son format ni son ancre. Le choix facultatif de l’aperçu se sauvegarde ensuite normalement.
- Navigation, ouvertures explicites de liens simulées, états inconnus, bords d’écran, historique sans extraits et comparaison pixel à pixel des réglages fixes restent couverts. Les trois planches du README utilisent les composants natifs et des données fictives.

Les modules de données, Unicode, notifications et lancement du relais ne changent pas. Leur référence 0.11.0 reste : 260 contrôles C#, 25 tests Node, 32 contrôles Unicode/liens et 16 contrôles du lanceur. Ces campagnes métier n’ont pas été relancées pour ce correctif visuel. Le relais embarqué 0.11.0 est identique octet pour octet ; aucun jeu ni relais actif n’a été manipulé pendant la correction.

## Limites


Cette DLL n’a pas été chargée dans FF14 pendant la validation. Le dessin et les clics ImGui sont réels ; les services du jeu et les lancements de liens fictifs sont simulés. Le rendu dans une session, les transitions de combat et les textures chargées par Dalamud restent à vérifier en jeu.

Le point bleu reflète `hasUnreadTurn`, sans acquittement propre au plugin. Il indique une réponse non lue, pas que l’objectif entier est accompli. Une question peut rester à traiter pendant que la tâche continue. Le protocole local et le lien `codex://threads/<UUID>` sont internes et peuvent évoluer.

Les extraits se limitent aux titres des questions structurées, 240 caractères maximum, et restent dans la mémoire locale. Ni réponses, ni instructions, ni sorties d’outils ne sont exportées. L’historique et la configuration ne stockent pas les extraits. Les captures et fixtures publiques sont fictives ; les traces et données réelles restent hors de la livraison.

Expressway n’est pas installée dans l’environnement de test : son repli est validé. Aucun fichier de police, asset du jeu ou binaire tiers n’est redistribué.

Les [considérations techniques](https://dalamud.dev/plugin-development/technical-considerations/), les [restrictions](https://dalamud.dev/plugin-publishing/restrictions/) et la [politique IA de Dalamud](https://dalamud.dev/plugin-publishing/ai-policy/) ont été relues. Cette livraison ne soumet rien au catalogue officiel.

![Nouveaux mini HUD](images/new-huds.png)

![Aperçu des réponses non lues](images/peek.png)

Rendus ImGui hors jeu, données fictives.
