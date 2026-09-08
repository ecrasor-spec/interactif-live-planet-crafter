# Interactif Live — The Planet Crafter

Plugin BepInEx pour relier **The Planet Crafter** à Interactif Live et aux événements TikTok LIVE.

## Installation

1. Installer BepInEx 5 x64 dans le dossier de The Planet Crafter.
2. Lancer le jeu une fois puis le fermer.
3. Copier `InteractifLive.PlanetCrafter.dll` dans :

   `The Planet Crafter/BepInEx/plugins/`

4. Relancer le jeu.
5. Dans Interactif Live, sélectionner The Planet Crafter, enregistrer le dossier, puis associer les cadeaux aux actions.

L’application automatise ces étapes avec le bouton **Installer / mettre à jour BepInEx + le plugin**.

## Pont local

Le plugin écoute uniquement sur la machine du joueur :

`http://127.0.0.1:18948`

- `GET /health` vérifie que le plugin est chargé.
- `POST /action` reçoit une action et son donateur.

Le pont n’accepte que les identifiants d’actions définis par Interactif Live. Il ne reçoit aucune connexion entrante depuis Internet.

## Actions

Le plugin prend en charge les identifiants suivants :

- Survie : `restore_oxygen`, `restore_water`, `restore_food`, `restore_health`, `drain_oxygen`, `drain_water`, `drain_food`, `damage_player`.
- Objets : `give_random_item`, `give_random_items_5`, `give_random_items_10`, `remove_random_item`.
- Monde : `meteor_shower_beneficial`, `meteor_storm`, `boost_terraform`, `bad_weather`.
- Machines et joueur : `repair_nearby_machines`, `disable_nearby_machines`, `surprise_teleport`, `slow_player`.

Les signatures internes du jeu peuvent changer après une mise à jour de The Planet Crafter. Les actions incompatibles sont journalisées dans le log BepInEx sans faire crasher le jeu.

## Compatibilité

- Windows x64
- The Planet Crafter Steam
- BepInEx 5 x64

Après une mise à jour majeure du jeu, utiliser **Vérifier les mises à jour** dans Interactif Live avant de relancer un live.

## Publication

Les releases GitHub contiennent uniquement le fichier DLL. Le manifeste public est utilisé par Interactif Live pour connaître la dernière version et son empreinte SHA-256.
