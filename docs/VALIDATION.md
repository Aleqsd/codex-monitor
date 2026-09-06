# Validation du prototype 0.1.0

Validation du 6 septembre 2026 : Dalamud 15.0.3.2, API 15, SDK .NET 10.0.400.

- Compilation : zéro avertissement et zéro erreur.
- Relais : six contrôles de protocole passent ; les états de cinq tâches correspondent aux outils natifs de Codex. Un véritable passage actif → repos a été observé.
- Client C# : quinze contrôles passent, dont un accès au relais réel. Ils couvrent les états périmés, coupures, reconnexions et notifications initiales.
- Jeu : plugin activé par l’utilisateur ; fenêtre, cinq tâches (deux en cours et trois au repos) et compteur observés.
- Interactions : ouverture/fermeture depuis le compteur, ouverture/retour des réglages, recherche d’une tâche par titre.
- Coupure réelle du relais : liste effacée et compteur « Codex hors ligne ».
- Redémarrage réel du relais : retour automatique des cinq tâches.

Les notifications de réponse/approbation ont été vérifiées avec des événements simulés. Leur apparence en jeu n’a pas été testée. Les nouveaux designs ne sont pas encore intégrés. Le contrôle du jeu a été arrêté à la demande de l’utilisateur.

DLL observée en jeu : SHA-256 `EAC7B2A58C423C294B9CDA3B33C997DEF63312B610EFC58B36CE6A9397BF6583`.

La migration ne remplace pas l’exemplaire chargé dans FF14. Le build produit dans ce dépôt doit être chargé séparément lors du prochain essai autorisé. Les captures et instantanés privés restent hors du dépôt.
