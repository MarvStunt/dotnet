## Auteurs

- Mateus LOPES
- Marvin CONIL

## Projet 

API ASP.NET
Jeu godot

## Particularités

**Si le serveur de jeu est sur une autre machine :**
**Modifier l'URL du serveur :**
   - Dans l'éditeur Godot, ouvrir le fichier `NetworkManager.cs`
   - Localiser la ligne 10 :
   ```csharp
   private string serverUrl = "ws://localhost:5000/gamehub";
   ```
   - Modifier l'adresse par l'adresse de la machine hôte
   ```csharp
   // Exemple
   private string serverUrl = "ws://172.10.2.5:5000/gamehub";
   ```



Le jeu est disponible en téléchargement sur la page web sous "Memory Game" des lors que les fichiers se trouvent dans le dossier `executable` a la racine du projet.

Details sur le projet dans : - [README-details.md](README-details.md)
