# 🎮 Gauniv GameServer

Serveur de jeu multijoueur en temps réel pour le jeu de mémorisation Simon Says.

## 📋 Description

Le **GameServer** est un serveur ASP.NET Core dédié qui gère toute la logique multijoueur du jeu de mémorisation. Il communique avec les clients Godot via WebSocket (SignalR) et stocke les données dans PostgreSQL.

### Fonctionnalités

- ✅ Création et gestion de parties multijoueur
- ✅ Génération aléatoire de patterns
- ✅ Validation des tentatives en temps réel
- ✅ Système de scoring avec bonus de rapidité
- ✅ Classements en direct
- ✅ Gestion de la connexion/déconnexion des joueurs
- ✅ Stockage persistant dans PostgreSQL

---

## 🏗️ Architecture

```
Godot Client ← WebSocket (SignalR) → GameServer:5000
                                           ↓
                                      PostgreSQL
```

**Séparation des responsabilités :**
- **WebServer** : Plateforme de distribution (comme Steam)
- **GameServer** : Logique de jeu multijoueur en temps réel
- **Godot** : Client graphique du jeu

---

## 🚀 Démarrage rapide

### Prérequis

- .NET 10.0 SDK
- PostgreSQL 15+
- Docker (optionnel)

### Installation

1. **Cloner le projet**
```bash
git clone <repo>
cd Gauniv.GameServer
```

2. **Configurer la base de données**

Éditer `appsettings.json` :
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=gauniv_game;Username=root;Password=root"
  }
}
```

3. **Créer la base de données**
```bash
dotnet ef database update
```

4. **Lancer le serveur**
```bash
dotnet run
```

Le serveur démarre sur **http://localhost:5000**

---

## 🐳 Docker

### Build l'image
```bash
docker build -t gauniv-gameserver -f Dockerfile ..
```

### Lancer avec Docker Compose
```bash
cd ..
docker-compose up gauniv.gameserver
```

---

## 📡 API SignalR

### URL de connexion
```
ws://localhost:5000/gamehub
```

### Méthodes disponibles

#### MJ (Game Master)
- `CreateGame(gameMasterName, gridSize)` → Retourne le code de partie
- `StartGame(gameCode)` → Démarre la partie
- `StartRound(gameCode)` → Lance un nouveau round
- `NextRound(gameCode)` → Passe au round suivant
- `StopGame(gameCode)` → Termine la partie

#### Joueurs
- `JoinGame(gameCode, playerName)` → Rejoindre une partie
- `SubmitAttempt(gameCode, attempt, reactionTimeMs)` → Soumettre sa tentative
- `GetLeaderboard(gameCode)` → Obtenir le classement

### Événements reçus

- `GameStarted(gameId)` → La partie a commencé
- `ShowPattern(pattern, roundNumber)` → Pattern à mémoriser
- `PlayerJoined(playerName, playerId)` → Nouveau joueur
- `PlayerSubmitted(playerName, isCorrect, pointsEarned, totalScore)` → Tentative d'un joueur
- `RoundChanged(newRound)` → Changement de round
- `GameEnded(leaderboard)` → Fin de partie avec classement
- `PlayerDisconnected(playerName)` → Déconnexion

---

## 🎯 Règles du jeu

### Principe

Simon Says est un jeu de mémorisation où les joueurs doivent reproduire une séquence de cellules qui s'allume.

### Déroulement

1. Le **MJ** crée une partie et communique le code à 6 caractères aux joueurs
2. Les **joueurs** rejoignent avec le code
3. Le **MJ** démarre la partie
4. À chaque round :
   - Le serveur génère un pattern aléatoire (nombre de cellules = numéro du round)
   - Le pattern est affiché à tous les joueurs
   - Les joueurs reproduisent le pattern
   - Le serveur valide et attribue les points
5. Le **MJ** passe au round suivant ou termine la partie

### Scoring

```
Points = 100 × Numéro du round + Bonus de rapidité

Bonus de rapidité (max 50 points):
- < 5000ms : 50 × (1 - temps/5000)
- ≥ 5000ms : 0

Exemple:
Round 3, réponse en 2s → 100×3 + 50×(1-2/5) = 300 + 30 = 330 points
```

---

## 📊 Base de données

### Tables

#### `GameSessions`
- `Id` : Identifiant unique
- `Code` : Code à 6 caractères (ex: "ABC123")
- `GameMasterName` : Nom du créateur
- `Status` : Waiting / InProgress / Finished
- `GridSize` : Taille de la grille (4x4 par défaut)
- `CurrentRound` : Numéro du round actuel

#### `GamePlayers`
- `Id` : Identifiant unique
- `GameSessionId` : Partie associée
- `PlayerName` : Nom du joueur
- `Score` : Score total
- `ConnectionId` : ID de connexion SignalR
- `IsConnected` : Statut de connexion

#### `GameRounds`
- `Id` : Identifiant unique
- `GameSessionId` : Partie associée
- `RoundNumber` : Numéro du round
- `Pattern` : Séquence JSON (ex: `[0, 5, 10, 5]`)

#### `PlayerAttempts`
- `Id` : Identifiant unique
- `GameRoundId` : Round associé
- `GamePlayerId` : Joueur
- `Attempt` : Tentative JSON
- `IsCorrect` : Succès/Échec
- `PointsEarned` : Points gagnés
- `ReactionTimeMs` : Temps de réaction

---

## 🔧 Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=gauniv_game;Username=root;Password=root"
  },
  "Server": {
    "Port": 5000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Variables d'environnement

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://+:5000
export ConnectionStrings__DefaultConnection="Host=db;Port=5432;..."
```

---

## 🧪 Tests

### Tester la connexion

```bash
# Installer wscat
npm install -g wscat

# Se connecter au Hub
wscat -c ws://localhost:5000/gamehub
```

### Créer une partie
```json
{
  "type": 1,
  "target": "CreateGame",
  "arguments": ["TestMaster", 4]
}
```

---

## 📝 Logs

Le serveur affiche des logs en temps réel :

```
✅ Client connected: abc123
🎮 Game created: ABC123 by TestMaster
👤 Player1 joined game ABC123
▶️ Game ABC123 started
🔢 Round 1 started - Pattern: 0, 5, 10
📝 Player1: ✓ (+100pts, total: 100)
🏁 Game ABC123 ended
❌ Client disconnected: abc123
```

---

## 🔐 Sécurité

**Note:** Actuellement, le serveur n'a pas d'authentification. Pour la production :

- [ ] Ajouter JWT pour l'authentification
- [ ] Valider les rôles (MJ vs Joueur)
- [ ] Limiter le nombre de parties par utilisateur
- [ ] Rate limiting sur les tentatives
- [ ] Validation des codes de partie côté serveur

---

## 📚 Documentation

- [Guide d'intégration Godot](../GODOT_GAMESERVER_INTEGRATION.md)
- [Documentation SignalR](https://learn.microsoft.com/aspnet/core/signalr)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)

---

## 🛠️ Développement

### Migrations

Créer une nouvelle migration :
```bash
dotnet ef migrations add NomDeLaMigration
```

Appliquer les migrations :
```bash
dotnet ef database update
```

### Build
```bash
dotnet build
```

### Publish
```bash
dotnet publish -c Release -o ./publish
```

---

## 🐛 Troubleshooting

### Le serveur ne démarre pas

1. Vérifier que le port 5000 n'est pas utilisé
```bash
netstat -ano | findstr :5000
```

2. Vérifier les logs de démarrage

### Impossible de se connecter à la BDD

1. Vérifier que PostgreSQL est démarré
2. Tester la connexion :
```bash
psql -h localhost -U root -d gauniv_game
```

### Les clients Godot ne se connectent pas

1. Vérifier que CORS est activé (déjà configuré)
2. Vérifier l'URL : `ws://localhost:5000/gamehub`
3. Regarder les logs du serveur

---

## 📦 Structure du projet

```
Gauniv.GameServer/
├── Data/
│   ├── GameModels.cs       # Entités (Session, Player, Round, Attempt)
│   └── GameDbContext.cs    # Context EF Core
├── Hubs/
│   └── GameHub.cs          # Hub SignalR (logique métier)
├── appsettings.json        # Configuration
├── Program.cs              # Point d'entrée
├── Dockerfile              # Image Docker
└── README.md               # Ce fichier
```

---

## 🤝 Contribution

1. Fork le projet
2. Créer une branche (`git checkout -b feature/AmazingFeature`)
3. Commit (`git commit -m 'Add AmazingFeature'`)
4. Push (`git push origin feature/AmazingFeature`)
5. Ouvrir une Pull Request

---

## 📄 Licence

Ce projet est sous licence MIT - voir [LICENSE.txt](../LICENSE.txt)

---

## 👥 Équipe

- **Backend** : [Votre nom]
- **Godot Client** : [Nom du dev Godot]

---

✨ **Bon développement !**
