## Vue d'ensemble

Ce projet comprend plusieurs composants :

1. **Gauniv.WebServer** - Serveur web ASP.NET Core avec interface d'administration et API REST
2. **Gauniv.GameServer** - Serveur de jeu multijoueur en temps réel (WebSocket/SignalR)
3. **Game (Godot)** - Client du jeu de mémorisation multijoueur
5. **PostgreSQL** - Base de données pour stocker les utilisateurs, jeux, scores, etc.
6. **Nginx** - Reverse proxy pour router les requêtes

---

## Installation et Lancement

### 1. Conteneurs Docker

```powershell
# 1. Se placer à la racine du projet
cd <path_to_project_root>

# 2. Lancer tous les services avec Docker Compose
docker-compose up -d
```

---

### 2. Jeu de memory Godot

Le jeu est un client Godot qui se connecte au GameServer via WebSocket.

#### Configuration importante si test sur plusieurs machines : Changer l'IP du serveur

Avant de lancer le jeu, modifier l'adresse du serveur :

1. **Ouvrir le projet Godot :**
   - Lancer l'éditeur Godot
   - Ouvrir le projet situé dans le dossier `Game/`

2. **Modifier l'URL du serveur :**
   - Dans l'éditeur Godot, ouvrir le fichier `NetworkManager.cs`
   - Localiser la ligne 10 :
   ```csharp
   private string serverUrl = "ws://localhost:5000/gamehub";
   ```
   - Modifier l'adresse selon votre configuration :
     - **Si vous jouez en local :** `ws://localhost:5000/gamehub`
     - **Si le serveur est sur une autre machine :** `ws://<IP_DU_SERVEUR>:5000/gamehub`

3. **Créer le fichier exécutable (.exe) :**
   - Dans Godot, aller dans `Project` → `Export...`
   - Sélectionner ou créer un template d'export pour Windows
   - Configurer les paramètres d'export si nécessaire
   - Cliquer sur `Export Project` et choisir un emplacement (par exemple : `Game/build/memorygame.exe`)

#### Comment jouer :

1. **Créer ou rejoindre une partie :**
   - Créer une nouvelle partie et partager le code
   - OU rejoindre une partie existante avec un code

si on cree une partie, on devient le MJ (Maître du Jeu), si on rejoint, on est joueur.

2. **Gameplay :**
   - Le MJ (Maître du Jeu) lance le jeu, sélectionne une ou plusieurs cases et decide de changer de round ou finir la partie
   - Les joueurs doivent cliquer sur la ou les cases sélectionnées dans l'ordre
   - Le classement final est affiché à la fin de la partie

3. **Deconnexion**
   - En cas de déconnexion, le joueur peut se reconnecter en utilisant le même nom dans la même partie
   - Si le MJ se déconnecte, la game est terminée pour tous les joueurs

---

### 3. Site Web ASP.NET Core

Le site web est diponible a l'adresse : `http://localhost:8457/` apres le lancement des serveurs via docker-compose.*

Il permet en tant qu'utilisateur :
- Creer un compte ou se connecter
- Visualiser la liste des jeux disponibles
    - Filtrer les recherches :
        - Par catégorie
        - Par prix
        - Par nom
        - Par taille de fichier
        - Par jeux posseédés
- Acheter des jeux
- Visualiser l'ensemble des utilisateur ayant un compte ainsi que les utilisateur en ligne
- Visualiser la bibliothèque de jeux achetés

En tant qu'administrateur :
- Gérer les utilisateurs (suppression)
- Gérer les jeux (ajout, suppression, modification)
- Gérer les catégories de jeux (ajout, suppression, modification)
- Visualiser certaines statistiques :
    - Revenus des derniers mois
    - Revenus totaux
    - Nombre de jeux
    - Nombre d'utilisateurs
    - Le nombre d'utilisateurs actifs dans les 30 derniers jours
    - Les jeux les plus populaires (par nombre d'achats)



### 4. Client MAUI

Le client n'a pas été développé.

---

## Auteurs

- Mateus LOPES
- Marvin CONIL

