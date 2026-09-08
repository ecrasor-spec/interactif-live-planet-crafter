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

Les signatures internes du jeu peuvent changer après une mise à jour de The Planet Crafter. Les actions actuellement garanties sont les huit actions de jauges (oxygène, eau, nourriture et santé). Les autres actions restent listées pour préserver la configuration, mais le pont renvoie explicitement une erreur `501` tant que leur mapping interne n'est pas validé, au lieu de les annoncer comme exécutées.

Sur Xbox Game Pass, le plugin reste volontairement sans injection de `MonoBehaviour` supplémentaire : cette injection peut provoquer un crash IL2CPP sur certaines versions de Planet Crafter. Les actions non mappées sont refusées explicitement tant qu’un appel interne sûr n’a pas été validé.

## Compatibilité

- Windows x64
- The Planet Crafter Steam
- The Planet Crafter Xbox Game Pass PC, si l’installation autorise le modding et l’écriture dans le dossier du jeu
- BepInEx 5 x64
- BepInEx 6 IL2CPP x64 pour Xbox Game Pass

Le plugin ne dépend pas de Steam : le dépôt publie une DLL Mono pour Steam et une DLL IL2CPP dédiée à Xbox Game Pass. L’application choisit automatiquement la bonne DLL. Sur Xbox Game Pass, Windows peut protéger le dossier `WindowsApps` ; dans ce cas il faut utiliser le dossier de jeu modifiable proposé par l’application Xbox ou sélectionner une installation autorisée.

Après une mise à jour majeure du jeu, utiliser **Vérifier les mises à jour** dans Interactif Live avant de relancer un live.

## Publication

Les releases GitHub contiennent uniquement le fichier DLL. Le manifeste public est utilisé par Interactif Live pour connaître la dernière version et son empreinte SHA-256.
