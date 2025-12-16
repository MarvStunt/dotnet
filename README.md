# Plateforme de distribution de contenu + Editeur

## But

Construire un web service avec son client Windows pour gérer une plateforme de distribution de contenu limitée aux jeux vidéo. 

Ajouter à celui-ci un jeu multijoueur comprenant le serveur ainsi que le jeu correspondant.t

## A rendre

Un web service de stockage et de gestion des jeux en ligne.

Un logiciel sous Windows pour parcourir les jeux, en télécharger un et jouer à celui-ci.

Un serveur de jeu orchestrant le fonctionnement d’au moins un jeu.

Une application permettant de jouer à un jeu.

# Contrainte

Langages autorisés : C#, HTML, Javascript, CSS, TypeScript

Serveur web : ASP.Net Core

Logiciel Windows : WPF

Serveur de jeux : C#

Jeu : C# avec Godot, Unity, Winform, WPF, MAUI, ...

## Projet de départ

Votre solution devra être basée sur le projet Library.sln.

La partie serveur est dans le projet Gauniv.WebServer.

La partie client est dans le projet Gauniv.Client.

La connexion entre votre client et votre serveur est dans le projet Gauniv.Network.

Vous devrez créer les deux projets pour le serveur de jeu et le jeu lui-même.

Le serveur de jeu devra se nommer Gauniv.GameServer.

Le jeu devra se nommer Gauniv.Game.

# Aide

## Base de données

Pour des informations sur le fonctionnement d’Entity Framework : <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=vs>

## MAUI – Gesture

Pour les éléments ne prenant pas en charge l’évènement click

```xml
<Button Clicked="" />
```

Vous pouvez utiliser les Gesture

```xml
<Label>
    <Label.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding AppearingCommand}">
    </Label.Behaviors>
</Label>
```

## MAUI – Evènement

Si vous avez besoin de transmettre un évènement depuis une View vers un ModelView vous pouvez utiliser un tag fournit par la librairie CommunityToolkit

Ex :

```xml
<Label Focused="xxx">
```

Ajouter ce namespace :

```xml
<xxx 
    xmlns:toolkit=http://schemas.microsoft.com/dotnet/2022/maui/toolkit>
</xxx>
```

Vous pouvez alors utiliser le tag suivant :
```xml
<Label>
    <Label.Behaviors>
        <toolkit:EventToCommandBehavior EventName="Focused"
            Command="{Binding FocusedCommand}"
            x:TypeArguments="FocusedEventArgs" />
    </Label.Behaviors>
</Label>
```
## DTO
Pour renvoyer un objet différent de celui contenu dans votre base utiliser un DTO
 - Vous fait votre DTO à la main: https://learn.microsoft.com/en-us/aspnet/web-api/overview/data/using-web-api-with-entity-framework/part-5
 - Vous utiliser la librairie AutoMapper: https://automapper.org/

## Entity Framewrok
Si vous obtenez un objet null lors de la lecture d'une liaison d'un objet stocké en BDD
ex : `appDbContext.Games.Categories.Where() => Categories is null`

Pour que Entity Framework retourne les categories avec les jeux :
 - Utilisez la méthode Include : `appDbContext.Games.Include(b => b.Categories).Where(x => x.Price > 0)`
 - Utiliser le LazyLoading
            https://learn.microsoft.com/en-us/ef/ef6/querying/related-data


## Devellopement

Il peut être plus facile dans un premier temps de tester les différents logiciels séparément :

- Injecter des données de test dans la BDD.
- Avant de faire des requêtes HTTP depuis le client, créer votre interface grâce à des données statiques.
- Au lieu de charger un vrai binaire, utiliser un fichier texte que vous ouvrez avec votre éditeur par défaut.

# Fonctionnalité attendue

## Livrable

- Un serveur web contenant: 
  - Une interface d'administration
  - Une API REST
- Un client lourd permettant: 
  - La consultation des jeux
  - Le téléchargement de jeux
  - Le lancement de jeux
- Un serveur autonome de jeu
- Un jeu

# Plateforme de distribution de contenu (ASP.NET)

## Modèle de données

Stocker un ensemble de jeux consistant en :
 - Une liste des jeux accessibles
 - Une liste des jeux achetés
 - Une liste de genres permettant de caractériser les jeux

Sachant que :

- Un jeu contient au minimum :
  - Un Id
  - Un nom
  - Une description
  - Un payload (binaire du jeu)
  - Un prix
  - Des catégories (Un jeu peut avoir plusieurs catégories)

- Un utilisateur contient au minimum :
  - Un Id
  - Un nom
  - Un prénom
  - Une liste des jeux achetées

## administration

Un administrateur doit pouvoir :
 - Ajouter des jeux
 - Supprimer des jeux
 - Modifier un jeu
 - Ajouter de nouvelles catégories
 - Modifier une catégorie
 - Supprimer une catégorie

Un utilisateur doit pouvoir :
 - Consulter la liste des jeux possédés
 - Acheter un nouveau jeu
 - Voir les jeux possédés
 - Consulter la liste des autres joueurs inscrits et leurs statuts en temps réel

Tout le monde peut :
 - Consulter la liste de tous les jeux
   - Filtrer par nom / prix / catégorie / possédé / taille
 - Consulter la liste de toutes les catégories

### Options

- Afficher des filtres dans la liste des jeux pour filtrer par catégorie / prix / possédé.
- Une page affichant les statistiques sur :
  - Le nombre total de jeux disponibles
  - Le nombre de jeux par catégorie
  - Le nombre moyen de jeux joués par compte
  - Le temps moyen joué par jeu
  - Le maximum de joueurs en simultané sur la plateforme et par jeu
- Un jeu pouvant faire plusieurs Gio, il est nécessaire de pouvoir les stocker sur autre chose qu’une base de données classique. Trouver et mettre en place un mécanisme pour stocker les jeux hors de la BDD.
- En suivant le même principe, il est nécessaire de ne pas stocker l’ensemble du fichier en mémoire avant de l’envoyer. Streamer le binaire en direct pour réduire l’empreinte mémoire de votre serveur.

Au lieu d’afficher la liste de tous les joueurs, faites en sorte que chaque joueur ait une liste d’amis.

## API

Une API REST doit être mise à disposition pour permettre à des clients externes de consulter la librairie.

Cette API doit permettre de :

- S’authentifier
- Récupérer le binaire d’un jeu et le copier localement (/ ! \\ Un jeu pouvant faire plusieurs Gio, il est impensable de stocker l’ensemble du binaire en mémoire)
- Lister les catégories disponibles (tout le monde)
- Lister les jeux (incluant filtre + pagination) (tout le monde)
  - `/game`
  - `/game?offset=10&limit=15`
  - `/game?category=3`
  - `/game?category[]=3&category[]=4`
  - `/game?offset=10&limit=15&category[]=3`
  - `/game?offset=10&limit=15&category[]=3&category[]=2`
- Lister les jeux possédés (incluant filtre + pagination) (joueur connecté uniquement)
  - `/game`
  - `/game?offset=10&limit=15`
  - `/game?category=3`
  - `/game?category[]=3&category[]=4`
  - `/game?offset=10&limit=15&category[]=3`
  - `/game?offset=10&limit=15&category[]=3&category[]=2`

La liste des jeux et la liste de mes jeux peuvent être factorisées en une seule API.


# Application (WPF, MAUI, WINUI)

L’application doit pouvoir permettre de :

- Lister les jeux (vous pouvez définir la limite comme bon vous semble)
  - Incluant la pagination (scroll infini, bouton ou autres)
  - Filtrer par jeux possédés / catégorie / prix / …
- Lister les jeux possédés par le joueur (vous pouvez définir la limite comme bon vous semble)
  - Incluant la pagination (scroll infini, bouton ou autres)
  - Filtrer par jeux possédés / catégorie / prix / …
- Afficher les détails d’un jeu (nom, description, statuts, catégories)
- Télécharger, supprimer et lancer un jeu
  - L’utilisateur ne devra pas voir les boutons "jouer" et "supprimer" si le jeu n’a pas été téléchargé
  - De même, le bouton "télécharger" ne sera pas visible si le jeu est déjà disponible
- Jouer à un jeu
  - Visualiser l’état du jeu (non téléchargé, prêt, en jeu, …)
  - Contrôler le jeu (lancement, arrêt forcé, …)
- Voir et mettre à jour son profil d’application (dossier d’installation, identifiants, …)

L’ensemble des données concernant les jeux devra provenir du serveur.


## Options

- Afficher la description avec un formatage : style de police, couleur, taille du texte, ...
  - Penser au RTF, HTML, PDF, ...
  - Dans un premier temps, gérez uniquement un format. Si vous avez fini, vous pouvez gérer plusieurs formats en même temps
- Lire la description grâce à l'API [System.Speech.SpeechSynthesizer](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer?view=net-9.0)
  - Gérer la lecture / l'arrêt / la pause / la reprise
  - Changer les boutons de contrôle en fonction de l'état de la lecture (comme un lecteur vidéo, ex : YouTube)
  - Commencer à lire à partir de la sélection de l'utilisateur. L'utilisateur doit pouvoir faire un clic droit sur un mot et lancer la lecture à partir de ce mot


# Serveur de jeu (Console)

Le serveur est une application console qui coordonne tous les joueurs.

La communication entre les joueurs et le serveur se fait en TCP.

Pour simplifier la communication, je conseille l’utilisation de MessagePack ou autre (Protobuff, Thrift, Cap’n Proto, ...).


## Deroulement d’une partie

Le jeu se joue sur un damier N*N.

1. Le serveur attend que tous les joueurs soient prêts pour commencer la partie.
2. Le serveur décide du MJ et avertit tous les participants de leurs rôles.
3. Le MJ décide d'une case et valide son choix.
4. Les joueurs reçoivent le top départ.
5. Chaque joueur clique le plus vite possible sur la case choisie par le MJ.
6. Le serveur définit l'ordre final des joueurs grâce au temps de réaction de chaque joueur.
7. Pour chaque joueur, le serveur vérifie que la participation du joueur est valide grâce à la fonction ci-dessous. Si le joueur est exclu, la position de tous les joueurs doit être mise à jour en conséquence.
8. Le serveur communique le résultat final à tout le monde.

## Le joueur

- Un joueur doit être authentifié par login / mot de passe auprès du serveur d’identification.
  - Le serveur d’authentification doit retourner un token prouvant l’authentification.
- Un joueur est composé d’un nom et d’un token d’authentification.

## Option

- Le serveur sait gérer plusieurs parties en même temps (et donc il sait gérer des salons).
- Séparer la partie serveur de la partie jeu :
  - Le serveur est générique et charge des plugins, chaque plugin est un jeu.
  - Le serveur peut gérer plusieurs jeux en même temps.
  - On peut rajouter un jeu sans redémarrer le serveur.
- Lancer plusieurs serveurs en même temps pour augmenter la capacité maximale de joueurs :
  - Un joueur peut se connecter à n'importe quel serveur et jouer à n'importe quelle partie.
  - Si le serveur ne sait gérer qu'une partie à la fois, alors tous les joueurs de tous les serveurs rejoignent la même partie en même temps.
  - Si le serveur sait gérer plusieurs parties à la fois, alors le joueur peut choisir la partie à rejoindre quel que soit son serveur d'origine.


# Jeu (Godot, UNITY, Winform, Console, …)


Le jeu doit mettre en place les IHM permettant aux joueurs de jouer

### Commun

1. Entrer des identifiant de connexion
2. Sélection du nom
3. Ready check

# MJ ou #JOUEUR

1. Attente des autres joueurs
2. Affichage des résultats

### MJ

1. Sélection d’une case
2. Validation de la case sélectionné ou changement (ref #4)

### Joueur

1. Attente du choix du MJ
2. Affichage de la case sélectionné par le MJ
3. Clic !

## Option

- Ajout d’un temp maximal pour cliquer
- Géré les joueurs dans la liste d’ami avec le statut correspondant
- Remplacer le damier par une map créer par le MJ