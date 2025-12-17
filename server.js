const WebSocket = require('ws');
const http = require('http');

// Créer un serveur HTTP
const server = http.createServer();

// Créer un serveur WebSocket
const wss = new WebSocket.Server({ server });

// Stockage des jeux actifs
const games = new Map();
let gameIdCounter = 1000;

console.log('🎮 ========================================');
console.log('🎮 Serveur Memory Game démarré');
console.log('🎮 ws://localhost:8080');
console.log('🎮 ========================================\n');

wss.on('connection', (ws) => {
	console.log('✅ Client connecté');
	let playerId = null;
	let gameId = null;
	let role = null;

	ws.on('message', (data) => {
		try {
			const message = JSON.parse(data);
			console.log(`\n📨 Message: ${message.type}`);
			console.log(`   PlayerId: ${message.player_id}`);
			console.log(`   GameId: ${message.game_id || 'N/A'}`);

			switch (message.type) {
				case 'create_game':
					handleCreateGame(ws, message);
					break;
				case 'join_game':
					handleJoinGame(ws, message);
					break;
				case 'send_sequence':
					handleSendSequence(ws, message);
					break;
				case 'player_answer':
					handlePlayerAnswer(ws, message);
					break;
				default:
					console.log(`⚠️ Type inconnu: ${message.type}`);
			}
		} catch (error) {
			console.error('❌ Erreur:', error.message);
		}
	});

	ws.on('close', () => {
		console.log(`\n❌ Client déconnecté: ${playerId}`);
	});

	// ===== HANDLERS =====

	function handleCreateGame(ws, message) {
		playerId = message.player_id;
		gameId = `game_${gameIdCounter++}`;
		role = 'master';

		// Créer le jeu
		games.set(gameId, {
			master: playerId,
			masterWs: ws,
			players: [],
			playersWs: [],
			sequence: [],
			currentRound: 0
		});

		console.log(`\n🎮 JEU CRÉÉ`);
		console.log(`   ID: ${gameId}`);
		console.log(`   Master: ${message.player_name}`);

		// Envoyer confirmation au master
		ws.send(JSON.stringify({
			type: 'game_started',
			game_id: gameId,
			role: 'master'
		}));

		console.log(`\n✅ Réponse: game_started (master)`);
	}

	function handleJoinGame(ws, message) {
		playerId = message.player_id;
		gameId = message.game_id;
		role = 'player';

		if (!games.has(gameId)) {
			console.log(`\n❌ Jeu non trouvé: ${gameId}`);
			ws.send(JSON.stringify({
				type: 'error',
				message: 'Game not found'
			}));
			return;
		}

		const game = games.get(gameId);
		game.players.push(playerId);
		game.playersWs.push(ws);

		console.log(`\n👤 JOUEUR REJOINT`);
		console.log(`   Nom: ${message.player_name}`);
		console.log(`   Game: ${gameId}`);
		console.log(`   Total joueurs: ${game.players.length}`);

		// Envoyer confirmation au joueur
		ws.send(JSON.stringify({
			type: 'game_started',
			game_id: gameId,
			role: 'player'
		}));

		console.log(`\n✅ Réponse: game_started (player)`);

		// Notifier le master
		game.masterWs.send(JSON.stringify({
			type: 'player_joined',
			player_name: message.player_name,
			total_players: game.players.length
		}));
	}

	function handleSendSequence(ws, message) {
		const game = games.get(message.game_id);
		if (!game || game.master !== playerId) {
			console.log(`\n❌ Erreur: pas un master ou jeu non trouvé`);
			ws.send(JSON.stringify({
				type: 'error',
				message: 'Not a master or game not found'
			}));
			return;
		}

		console.log(`\n🔢 SÉQUENCE REÇUE`);
		console.log(`   Sequence: [${message.sequence}]`);

		// Sauvegarder la séquence
		game.latestSequence = message.sequence;

		// Envoyer à tous les joueurs
		game.playersWs.forEach((playerWs, index) => {
			playerWs.send(JSON.stringify({
				type: 'sequence',
				sequence: message.sequence
			}));
			console.log(`   ✅ Envoyé au joueur ${index + 1}`);
		});
	}

	function handlePlayerAnswer(ws, message) {
		const game = games.get(message.game_id);
		if (!game || !game.players.includes(playerId)) {
			console.log(`\n❌ Erreur: pas un joueur ou jeu non trouvé`);
			ws.send(JSON.stringify({
				type: 'error',
				message: 'Not a player or game not found'
			}));
			return;
		}

		console.log(`\n✏️ RÉPONSE DU JOUEUR`);
		console.log(`   Answer: [${message.answer.join(', ')}]`);

		// Comparer avec la séquence du master
		const masterSequence = game.latestSequence || [];
		const playerAnswer = message.answer;
		
		let isCorrect = true;
		if (playerAnswer.length !== masterSequence.length) {
			isCorrect = false;
		} else {
			for (let i = 0; i < playerAnswer.length; i++) {
				if (playerAnswer[i] !== masterSequence[i]) {
					isCorrect = false;
					break;
				}
			}
		}

		console.log(`   ${isCorrect ? '✅ CORRECT' : '❌ INCORRECT'}`);

		// Envoyer résultat au joueur
		ws.send(JSON.stringify({
			type: 'validation_result',
			correct: isCorrect,
			message: isCorrect ? 'Bravo! Correct!' : 'Dommage... Incorrect!'
		}));

		// Notifier le master
		game.masterWs.send(JSON.stringify({
			type: 'player_answered',
			correct: isCorrect,
			message: isCorrect ? 'Le joueur a trouvé la bonne séquence!' : 'Le joueur s\'est trompé!'
		}));

		// Si correct, fin du jeu
		if (isCorrect) {
			console.log(`\n🎉 JEU TERMINÉ - JOUEUR GAGNE!`);
			
			ws.send(JSON.stringify({
				type: 'game_ended',
				won: true,
				reason: 'Vous avez gagné! 🎉'
			}));

			game.masterWs.send(JSON.stringify({
				type: 'game_ended',
				won: true,
				reason: 'Le joueur a gagné! 🎉'
			}));
		}
	}
});

// Lancer le serveur
server.listen(8080, () => {
	console.log('🚀 Serveur en écoute sur ws://localhost:8080');
	console.log('\n📝 Utilisation:');
	console.log('   1. Lance l\'app Godot');
	console.log('   2. Crée une partie (Master)');
	console.log('   3. Ouvre une 2ème instance Godot et rejoins la partie');
	console.log('   4. Master envoie une séquence');
	console.log('   5. Player reproduit la séquence');
	console.log('\n✅ Prêt à jouer! 🎮\n');
});
