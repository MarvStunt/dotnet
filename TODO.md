# Description breve du jeu

Jeu de mémorisation où le MJ choisit un pattern de plusieurs cases qui s'allument dans un certain ordre. Les joueurs doivent reproduire le pattern en cliquant sur les cases dans le bon ordre.

# Fonctionnalités principales
MJ :
- Création d'une partie
- Choix du pattern (séquence de cases) incrémental (1er tour : 1 case, 2ème tour : 2 cases, etc.)
- Voir la liste des joueurs et leurs scores
- Pouvoir arrêter une partie
- Démarrage de la partie

Joueurs :
- Rejoindre une partie via un code unique
- Voir le pattern s'afficher
- Reproduire le pattern en cliquant sur les cases
- Voir son score et le classement des joueurs

# Interfaces
- Home page
- Page de création/rejoindre une partie
- Page mon compte (login / mot de passe)
- Page de jeu (damier, affichage du pattern, score, classement)

# Serveur C#
Le serveur doit gérer la logique du jeu et la communication entre les joueurs et le MJ.
Il doit également gérer l'authentification des joueurs et la persistance des données (parties, joueurs, scores).

## API REST
Une API REST doit être mise à disposition pour permettre aux clients externes de consulter et gérer les parties.
Cette API doit permettre de :
- S’authentifier
- Créer une partie (MJ uniquement)
- Rejoindre une partie (joueur uniquement)
- Démarrer une partie (MJ uniquement)
- Envoyer les actions des joueurs (joueur uniquement)
- Récupérer les scores et le classement (tout le monde)
- Arrêter une partie (MJ uniquement)