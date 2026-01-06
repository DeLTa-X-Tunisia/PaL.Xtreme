# PaL.Xtreme

PaL.Xtreme est une solution de messagerie instantanée moderne développée en WPF (.NET 10.0), inspirée de l'interface de Paltalk Messenger.

## 🏗 Structure du Projet

La solution se compose de trois projets principaux :

*   **PaLX.API** : Le cœur du système. Une API REST (ASP.NET Core) qui gère l'authentification, la base de données PostgreSQL, et la communication temps réel via SignalR.
*   **PaLX.Launcher** : Le point d'entrée unique. Il vérifie l'état du serveur (Health Check), joue le son de bienvenue et lance l'application Client.
*   **PaLX.Client** : L'application principale. Elle permet de se connecter, de gérer son statut, de gérer ses amis, son profil, et d'effectuer des appels vidéo/audio.

> **Note** : Une interface d'administration (modération, rôles, abonnements) sera ajoutée ultérieurement.

## 🌟 Nouveautés & Améliorations Récentes

Voici un résumé des dernières fonctionnalités et optimisations intégrées au projet :

### 📹 Appel Vidéo WebRTC v2.0 (Dernière Mise à Jour - v1.6.5)
*   **Architecture WebRTC Professionnelle** :
    *   **SIPSorcery WebRTC** : Connexion peer-to-peer avec ICE, DTLS-SRTP pour le chiffrement média.
    *   **OpenCvSharp4** : Capture vidéo de la webcam avec conversion en temps réel.
    *   **NAudio** : Capture et lecture audio haute qualité.
    *   **Support TURN Server** : Configuration Coturn pour traversée NAT/firewall fiable.

*   **Partage d'Écran (Nouveau v1.6.2)** :
    *   **Capture Plein Écran** : Capture de l'écran principal via `Graphics.CopyFromScreen`.
    *   **Qualité Optimisée** : Format 24bpp RGB, gestion correcte du stride, résolution 1920x1080.
    *   **Bitrate Adapté** : 1500 kbps automatique pour le partage d'écran (vs 500 kbps caméra).
    *   **Toggle Automatique** : Désactivation caméra lors du partage, réactivation à l'arrêt.

*   **Gestion Statut "En appel" (Nouveau v1.6.2)** :
    *   **Changement Automatique** : Statut passe à "En appel" dès connexion vidéo établie.
    *   **Retour Automatique** : Statut revient à "En ligne" à la fin de l'appel.
    *   **Vérification Avant Appel** : Message d'alerte si le contact est déjà en appel.
    *   **Visibilité Contacts** : Les amis voient que l'utilisateur est occupé en appel.

*   **Améliorations UX v1.6.3** :
    *   **Bouton Minimiser** : Fenêtre d'appel vidéo peut être minimisée (bouton bien espacé du bouton fermer).
    *   **Notifications Globales** : Appels entrants notifiés même si la fenêtre de chat n'est pas ouverte.
    *   **Blocage Appels Hors Ligne** : Impossible d'appeler un utilisateur hors ligne (statut 6).
    *   **Messages Utilisateur** : "Juste un instant..." et "La parole est à vous!" plus naturels.

### 💬 Chatroom Modernisée (Nouveau v1.6.4)
*   **Design Cohérent PaL.Xtreme** :
    *   **Fenêtre Sans Bordure** : Style moderne avec coins arrondis (20px) et ombre portée.
    *   **Header Gradient Rouge** : Identique au ChatWindow (#E03E2F → #8B2920).
    *   **Badge 18+** : Indicateur visible pour les salons adultes.
    *   **Statistiques Modernes** : Compteurs (Total/Hommes/Femmes) dans des pilules semi-transparentes.
    *   **Bouton Plein Écran** : Agrandir/Restaurer avec double-clic sur le header.

### 🖼️ Avatars Chatroom (Nouveau v1.6.5)
*   **Photos de Profil Réelles** :
    *   **Liste des Membres** : Avatar circulaire avec bordure colorée selon le rôle.
    *   **Bulles de Messages** : Photo de profil à côté de chaque message.
    *   **Fallback Élégant** : Icône 👤 si l'utilisateur n'a pas d'avatar.
    *   **Temps Réel** : Avatars transmis via SignalR pour les nouveaux membres.

*   **Nettoyage Membres Fantômes** :
    *   **Déconnexion Propre** : Suppression automatique des RoomMembers à la déconnexion.
    *   **Startup Cleanup** : Nettoyage des membres non-propriétaires au démarrage du serveur.

*   **Zone de Messages Améliorée** :
    *   **Bulles Modernes** : Fond blanc, coins arrondis (16px), ombre subtile.
    *   **Avatars avec Rôle** : Bordure colorée selon le rôle (Owner=Rouge, Admin=Orange, Mod=Bleu, etc.).
    *   **Badge de Rôle** : Affichage du nom du rôle à côté du pseudo.
    *   **Messages Système** : Style distinct centré avec icône info.

*   **Sidebar Membres Premium** :
    *   **Liste Interactive** : Hover effect sur les membres.
    *   **Indicateur Micro Actif** : Point vert lumineux sur l'avatar quand le micro est ON.
    *   **Timer de Parole** : Badge rouge avec le temps de parole en cours.
    *   **Animation Pulsante** : Icône micro animée pour visualiser qui parle.
    *   **Rôle Visuel** : Point coloré + nom du rôle sous chaque pseudo.

*   **Contrôles Utilisateur Redessinés** :
    *   **Boutons Toggle Circulaires** : Plus grands (52px), effet ombre au survol.
    *   **Timer de Parole Personnel** : Bandeau rouge avec effet glow quand actif.
    *   **Interactions Fluides** : Envoi avec Enter, déplacement fenêtre par le header.

*   **Améliorations Backend** :
    *   **RoleName dans les Messages** : API retourne maintenant le nom du rôle pour chaque message.
    *   **DTOs Enrichis** : `RoomMessageDto` inclut `RoleName` pour l'affichage du badge.

*   **Codec Audio Opus** :
    *   **Concentus 2.2.0** : Implémentation Opus pure managed .NET (pas de dépendances natives).
    *   **Qualité Audio Supérieure** : 48kHz, bitrate adaptatif 24-64 kbps, FEC pour perte de paquets.
    *   **Fallback G.711** : μ-law/A-law comme codec de secours pour compatibilité.

*   **Architecture Modulaire** :
    *   **IMediaCapture** : Abstraction pour capture vidéo/audio (IVideoCapture, IAudioCapture, IAudioPlayback).
    *   **IMediaEncoder** : Interfaces encodeurs (IPaLXAudioEncoder, IPaLXVideoEncoder, IEncoderFactory).
    *   **IWebRTCTransport** : Abstraction transport WebRTC avec WebRTCConfig, TurnServerConfig.
    *   **EncoderFactory** : Factory pattern pour création dynamique des encodeurs.

*   **Interface VideoCallWindow Premium** :
    *   **Design Glass/Modern** : Effets de transparence, bordures gradient, ombres portées.
    *   **Animations** : Boutons Accept/Decline pulsants, anneau avatar rotatif, animation "..." status.
    *   **Picture-in-Picture** : Vidéo locale repositionnable avec label "Vous".
    *   **Indicateur Qualité HD** : Icône signal avec status de connexion.
    *   **Barre de Contrôles Flottante** : Micro, Caméra, Partage d'écran, Raccrocher.
    *   **Sons Appel Vidéo** : `appel_video.mp3` (sonnerie) + `end_video.mp3` (fin d'appel).
    *   **Messages Naturels** : "Juste un instant..." et "La parole est à vous!" orientés utilisateur.

*   **Corrections Stabilité v1.6.3** :
    *   **Partage d'écran** : Correction qualité image (format 24bpp, gestion stride).
    *   **Crash Arrêt Partage** : Meilleure synchronisation threads lors du retour caméra.
    *   **Arrêt Sonnerie** : La musique d'appel s'arrête dès que l'appel est accepté/refusé/terminé.
    *   **Synchronisation Caméra** : Caméra démarre uniquement après connexion WebRTC établie.
    *   **Boutons Espacés** : Minimize et Close bien positionnés (plus de superposition).
    *   **Logique Appel Corrigée** : Vérification statut hors ligne (6) uniquement, pas les autres statuts.

### 🎙️ Mode Sombre & Interface Paramètres (Dernière Mise à Jour - v1.2.0)
*   **Thème Sombre Complet** :
    *   **Toggle Mode Sombre** : Nouveau système de thème avec basculement Light/Dark en un clic.
    *   **Sauvegarde Automatique** : Les préférences de thème sont persistées localement.
    *   **Couleurs Dark Mode** : Palette sombre moderne (fond #1A1A2E, cartes #25253D, texte clair #EAEAEA).
    *   **DynamicResource** : Toutes les couleurs utilisent des ressources dynamiques pour un changement instantané.

*   **Fenêtre Paramètres Moderne** :
    *   **Design épuré** : Interface compacte sans scroll, avec icônes colorées pour chaque option.
    *   **Options disponibles** : Mode Sombre, Sons de notification, Son de démarrage.
    *   **Section À propos** : Version de l'application et copyright.
    *   **Fenêtre non-modale** : Ne bloque plus l'application principale.

*   **Barre de Navigation Modernisée** :
    *   **Design "Floating"** : Barre de navigation flottante avec effet de profondeur.
    *   **Boutons avec fond arrondi** : Chaque icône dans un cercle/carré arrondi stylisé.
    *   **Bouton central accentué** : "Ajouter un ami" (+) mis en valeur au centre avec ombre rouge.
    *   **Menu contextuel amélioré** : Menu ⚙️ avec icônes colorées, titres et descriptions :
        *   👤 Mon Profil → "Modifier mes informations"
        *   ⚙️ Paramètres → "Thème, sons, préférences"  
        *   🚫 Utilisateurs bloqués → "Gérer la liste noire"

### 🎨 Modernisation du Chat - Interface WPF Native
*   **Migration WebView2 → WPF Natif** :
    *   Remplacement complet du rendu HTML/WebView2 par des contrôles WPF natifs dans le Client.
    *   Meilleure performance, fluidité et cohérence visuelle avec le reste de l'application.
    *   Templates XAML personnalisés pour chaque type de message (Texte, Image, Audio, Vidéo, Fichier, Statut).

*   **Affichage des Images** :
    *   **Expéditeur** : Voit immédiatement la miniature de l'image envoyée.
    *   **Destinataire** : Design moderne avec aperçu flouté, overlay sombre, et boutons "✓ Accepter" / "✗ Refuser" élégants.
    *   **Images acceptées** : Affichage direct avec taille adaptative (max 200x200, petites images conservent leur taille naturelle).
    *   **Clic pour agrandir** : Ouverture dans la visionneuse système.

*   **Lecteur Audio Moderne** :
    *   **Design** : Bouton Play/Pause circulaire, visualisation waveform stylisée, durée affichée.
    *   **Fonctionnalité Play/Pause** : Clic pour jouer, re-clic pour mettre en pause, reprise possible.
    *   **Fichiers Audio (.mp3, .wav, etc.)** : 
        *   Expéditeur voit immédiatement le lecteur audio.
        *   Destinataire : Template moderne avec icône musicale et boutons d'action.
        *   Une fois accepté : Lecteur audio complet identique aux messages vocaux.

*   **Lecteur Vidéo Intégré** :
    *   Lecteur vidéo embarqué dans le chat avec contrôles Play/Pause.
    *   Simple clic : Play/Pause dans le chat.
    *   Double-clic : Ouverture dans le lecteur externe.

*   **Messages de Statut Colorés** :
    *   Couleurs dynamiques selon le statut : Vert (En ligne), Rouge (Occupé), Orange (Absent), Bleu (En appel), Magenta (Ne pas déranger).

*   **Noms d'Affichage** :
    *   Utilisation systématique des noms d'affichage ("User A", "User B") au lieu des identifiants techniques ("user1", "user2").

### 🔧 Corrections & Optimisations
*   **Correction Audio URL** : Résolution du bug où les URLs audio étaient corrompues par le convertisseur d'emojis (`:/ ` converti en 😕).
*   **Rafraîchissement des Templates** : Les transferts acceptés/refusés mettent à jour instantanément leur apparence visuelle.
*   **Ordre de Chargement** : Les informations du partenaire sont chargées AVANT l'historique pour afficher les bons noms.

### 🗂️ Administration & Stabilité (Dernière Mise à Jour)
*   **Gestion des Salons (Admin)** :
    *   **Parité Fonctionnelle** : Ajout des boutons "Éditer", "Masquer" et "Supprimer" dans la liste des salons de l'interface Admin, alignant les capacités de gestion sur celles du Client.
    *   **Contrôle Propriétaire** : Ces options sont dynamiquement visibles uniquement pour le créateur du salon.
*   **Stabilité du Processus** :
    *   **Correction "Zombie Process"** : Résolution critique du bug où le processus `PaLX.Admin` restait actif après la fermeture de la fenêtre.
    *   **Nettoyage des Ressources** : Implémentation rigoureuse du pattern `IDisposable` dans le service vocal (`VoiceCallService`) pour libérer correctement les threads WebRTC et les connexions SignalR à la fermeture.
    *   **Arrêt Forcé** : Sécurité supplémentaire garantissant l'arrêt complet de l'application lors de la sortie.

### �🛡️ Gestion Avancée du Statut "Ne pas déranger" (DND)
*   **Matrice de Rôles Stricte** : Implémentation d'une logique de permission hiérarchique pour le statut DND.
    *   Un utilisateur en mode DND bloque par défaut tous les messages entrants.
    *   **Exception Hiérarchique** : Un utilisateur peut contourner le blocage DND d'un autre utilisateur **uniquement** si son rôle est supérieur ou égal (ex: ServerMaster peut écrire à tout le monde, ServerAdmin peut écrire aux utilisateurs mais pas aux SuperAdmins en DND).
    *   **Exception Conversationnelle** : Si l'utilisateur en DND initie lui-même la conversation, le blocage est levé temporairement pour permettre la réponse.
*   **Feedback Visuel** :
    *   Zone de saisie désactivée et message d'avertissement rouge explicite : *"User est en mode == NE PAS DÉRANGER == veuillez respecter ça et réessayer plus tard."*
    *   Mise à jour en temps réel si le statut change pendant la conversation.

### 🧹 Interface "Effacer l'historique" Moderne
*   **Refonte UI** : Remplacement des boîtes de dialogue système (style Windows 2000) par une fenêtre modale personnalisée (`ClearHistoryWindow`).
*   **Design** : Interface sombre, élégante, sans bordures système, cohérente avec le reste de l'application.

### 🎨 UX & Polish Visuel (Dernière Mise à Jour)
*   **Formatage des Noms** :
    *   **Standardisation** : Affichage systématique des noms au format "Prénom Nom" (Title Case) dans toute l'application (Listes d'amis, Fenêtres d'appel, Chat, Notifications).
    *   **Suppression des IDs techniques** : Remplacement des identifiants bruts (ex: `admin1`) par des noms d'affichage professionnels.
*   **Expérience de Chat** :
    *   **Scroll Automatique Intelligent** : Le chat défile désormais automatiquement et proprement vers le bas lors de la réception de fichiers (images, vidéos, audio), garantissant que les boutons d'action sont immédiatement visibles.
    *   **Visibilité** : Ajustement des marges (padding) pour éviter que le dernier message ne soit coupé.
    *   **Interactivité** : Correction complète des boutons "Accepter / Refuser" pour tous les types de fichiers dans l'interface Admin, avec synchronisation temps réel.

### � Chat Rooms : Parité Admin & Audio (Mise à jour Majeure)
*   **Synchronisation Admin** :
    *   **Correction Temps Réel** : L'interface Admin reçoit désormais les événements `UserJoinedRoom` avec les données complètes (`RoomMemberDto`), éliminant le délai de synchronisation et les utilisateurs invisibles.
    *   **Parité Fonctionnelle** : Alignement total de la logique de gestion des membres entre le Client et l'Admin.
*   **Audio Mesh P2P** :
    *   **Support Multi-Peer** : Implémentation de la topologie Mesh WebRTC dans l'Admin (`VoiceCallService`), permettant aux administrateurs de participer pleinement aux conversations vocales de groupe.
    *   **Stabilité** : Gestion robuste des connexions multiples simultanées.
*   **Gestion du Micro** :
    *   **Mute par Défaut** : Pour éviter les bruits parasites, le microphone est désormais **désactivé par défaut** à l'entrée d'une room (Client & Admin).
    *   **Contrôle Admin** : Le bouton de micro de l'interface Admin contrôle désormais correctement le flux audio réel.

### �🛠️ Correctifs & Optimisations
*   **Admin Chat Fixes** :
    *   **Smileys** : Correction de l'affichage des smileys dans l'interface Admin (décodage correct des balises `[smiley:...]`).
    *   **Transfert de Fichiers** : Réparation des boutons "Accepter" et "Refuser" pour les images, vidéos et fichiers dans le chat Admin.
    *   **Cohérence** : Alignement complet du comportement et du rendu visuel entre le Client et l'Admin.
*   **Gestion des Utilisateurs Bloqués** :
    *   **Correction Critique** : Résolution du bug affichant une liste vide dans la fenêtre "Utilisateurs bloqués".
    *   **Robustesse SQL** : Amélioration de la requête pour gérer les données manquantes (NULL) et ignorer la casse lors de la recherche.
    *   **Diagnostic** : Remplacement des erreurs génériques (500) par des messages d'erreur détaillés pour faciliter le débogage.
*   **Interface & UX** :
    *   **Déconnexion Moderne** : Remplacement des alertes système intrusives par une fenêtre de déconnexion dédiée, élégante et transparente (`DisconnectionWindow`), offrant une expérience plus professionnelle lors de la perte de connexion.
    *   **Blocage Utilisateur** : Correction de la mise à jour visuelle immédiate (icône et voile gris) lors du blocage/déblocage d'un contact.
*   **Transfert de Fichiers** :
    *   **Synchronisation** : Correction de la logique de mise à jour des statuts de transfert (progression, succès) assurant que l'expéditeur et le destinataire voient le même état.
    *   **Sauvegarde Vidéo** : Réparation de la fonctionnalité "Enregistrer sous" pour les vidéos reçues, permettant de les sauvegarder localement via le menu contextuel.
    *   **Persistance Vidéo** : 
        *   Correction critique assurant que les vidéos envoyées restent visibles et lisibles dans l'historique après reconnexion, aussi bien sur le Client que sur l'Admin.
        *   Harmonisation de la logique de parsing des fichiers entre les deux plateformes.
*   **Stabilité Admin** : 
    *   Résolution du crash systématique lors de la déconnexion (Logout) de l'interface administrateur.
    *   Amélioration de la gestion de la fermeture des connexions SignalR.
*   **Qualité du Code** : 
    *   **Zero Warning** : Recompilation complète de la solution avec résolution de tous les avertissements (CS4014, CS8618, CS8602, etc.).
    *   **Robustesse** : Ajout de vérifications de nullité et initialisation correcte des propriétés dans les DTOs et Modèles.
*   **Dépendances** : 
    *   Ajout et consolidation des packages manquants (`Npgsql`, `BCrypt.Net-Next`) pour assurer la stabilité et la compilation du projet Admin.

### 💬 Chat & Messagerie
*   **Messages Audio (Nouveau)** :
    *   **Enregistrement Intégré** : Possibilité d'enregistrer des messages vocaux directement depuis la fenêtre de chat (bouton micro).
    *   **Lecteur Audio** : Lecteur intégré avec barre de progression, bouton Play/Pause et durée.
    *   **Envoi Fluide** : Upload automatique et affichage immédiat dans la conversation.
*   **Améliorations Visuelles** :
    *   **Séparateur de Nouveaux Messages** : Une ligne "Nouveaux messages" apparaît clairement pour séparer l'historique des messages non lus.
    *   **Horodatage Intelligent** : Affichage des dates (ex: "Aujourd'hui", "Hier") pour grouper les messages par jour.
    *   **Messages Système** : Design distinct pour les notifications système (ex: blocage, transfert de fichiers).
*   **Fonction BUZZ** :
    *   **Signal d'Appel** : Envoi d'un signal sonore et visuel (tremblement de fenêtre) pour attirer l'attention du correspondant.
    *   **Ouverture Automatique** : Si le destinataire reçoit un BUZZ alors que sa fenêtre de chat est fermée, celle-ci s'ouvre automatiquement pour garantir la réception de l'alerte.
    *   **Disponibilité** : Le bouton BUZZ (icône cloche) n'est actif que si le correspondant est "En ligne".
*   **Partage de Médias** :
    *   **Envoi d'Images** : Possibilité d'envoyer des images (JPG, PNG, GIF) directement dans le chat via le bouton trombone.
    *   **Expérience Utilisateur** : Barre de progression intégrée affichant l'avancement de l'upload en temps réel.
    *   **Visualisation** : Les images s'affichent directement dans la conversation. Un clic sur l'image l'ouvre en taille réelle dans la visionneuse par défaut du système.
    *   **Sécurité** : Validation stricte des extensions et limite de taille fixée à 5 MB.
*   **Formatage Riche** : Support complet du **Gras**, *Italique*, <u>Souligné</u> et de la **Couleur** du texte.
*   **Expérience Fluide** : Indicateur "En train d'écrire...", ouverture automatique des fenêtres de chat, et sons de notification intelligents.
*   **Historique Visuel** : Bulles de messages distinctes et affichage centralisé des changements de statut du partenaire.

### 🔄 Synchronisation & Fiabilité (Nouveau)
*   **Messages Hors-Ligne (Push)** :
    *   **Réception Automatique** : Les messages reçus pendant que l'utilisateur était déconnecté sont automatiquement "poussés" vers le client dès la reconnexion.
    *   **Gestion Intelligente** :
        *   **Client** : Les messages s'affichent directement et notifient l'utilisateur.
        *   **Admin** : Les messages hors-ligne s'ajoutent discrètement à la liste des "Messages non lus" sans ouvrir intempestivement des dizaines de fenêtres.
*   **Persistance de Lecture** :
    *   **Correction "Zombie"** : Correction d'un bug où les messages marqués comme lus réapparaissaient comme non-lus à la reconnexion.
    *   **Transferts de Fichiers** : L'ouverture d'une fenêtre de chat marque désormais correctement les transferts de fichiers comme "lus" en base de données.
*   **Stabilité API** :
    *   **Déconnexion Propre** : Distinction claire entre une déconnexion volontaire (Logout) et un crash serveur, évitant les fausses alertes de maintenance.

### 🛡️ Sécurité & Rôles
*   **Séparation Stricte** : Un utilisateur standard (Rôle 7) ne peut pas se connecter sur l'interface Admin, et inversement.
*   **Système de Blocage Avancé** :
    *   **Hiérarchie Admin** : Implémentation d'une sécurité basée sur les rôles (Niveau 1 à 7). Un utilisateur ne peut bloquer qu'un utilisateur de rang inférieur.
    *   **Flexibilité** : Options de blocage **Permanent**, **Temporaire** (7 jours) ou **Personnalisé** (date spécifique).
    *   **Gestion** : Interface dédiée pour visualiser les utilisateurs bloqués, modifier la durée ou lever le blocage.

### 🚀 Launcher & Stabilité
*   **Sons de Démarrage** : Sons d'accueil distincts pour l'application Client (`client_start.mp3`) et Admin (`admin_start.mp3`).
*   **Health Check** : Le launcher vérifie automatiquement la disponibilité de l'API avant de permettre la connexion, évitant les crashs au démarrage.
*   **Connexion Robuste** : Gestion améliorée des déconnexions et reconnexions, avec nettoyage automatique des ressources.

### 👥 UX & Notifications
*   **Notifications Temps Réel** : Badge rouge sur l'icône d'amis pour les demandes en attente, synchronisé via SignalR (Client & Admin).
*   **Sécurité des Actions** : Dans la fenêtre d'ajout d'amis, séparation claire entre le bouton "Voir le Profil" (👁️) et "Accepter" (✅) pour éviter les ajouts accidentels.
*   **Feedback Visuel** : Clignotement des contacts lors des changements de statut et tri automatique de la liste d'amis (En ligne > Hors ligne).

## 🚀 Fonctionnalités Détaillées

*   **Authentification Sécurisée** :
    *   Système de Login et d'Inscription.
    *   Hachage des mots de passe utilisant **BCrypt** pour une sécurité maximale.
    *   **Health Check** : Vérification automatique de la disponibilité du serveur au lancement du Launcher.
*   **Gestion des Rôles** :
    *   Séparation stricte entre les utilisateurs standards et les administrateurs via la base de données.
*   **Gestion des Amis** :
    *   **Recherche** : Recherche d'utilisateurs par pseudo ou email.
    *   **Demandes & Notifications** : 
        *   Envoi, réception, acceptation et refus de demandes.
        *   **Badge de Notification** : Indicateur rouge en temps réel sur l'icône d'amis signalant les demandes en attente (Client & Admin).
        *   **Interface Sécurisée** : Boutons distincts pour "Voir le Profil" (Icône bleue) et "Accepter" (Icône verte) pour éviter les erreurs.
    *   **Liste d'Amis** : Affichage en temps réel avec statut de connexion synchronisé.
        *   **Tri Intelligent** : Les utilisateurs en ligne apparaissent en premier (nom en **Gras**), suivis des utilisateurs hors ligne.
        *   **Synchronisation Instantanée** : Mise à jour immédiate lors de l'ajout d'amis et rafraîchissement rapide (toutes les 2 secondes) pour les statuts.
        *   **Indicateurs Visuels** : Texte de statut coloré selon l'état (Vert, Orange, Rouge...) et effet de **clignotement** (durée de 5 secondes) lorsqu'un ami change de statut.
        *   **Notifications Sonores** : Sons modernes et distincts lors de la connexion (son positif) et de la déconnexion (son discret) d'un ami.
*   **Messagerie Instantanée (Chat)** :
    *   **Interface Moderne** : Fenêtre de chat redimensionnée (550x700) avec un design épuré.
    *   **Formatage Riche** : Support du **Gras**, *Italique*, <u>Souligné</u> et des couleurs de texte.
    *   **Saisie Intuitive** :
        *   Zone de saisie `RichTextBox` avec persistance du style (le formatage reste actif entre les messages).
        *   Envoi rapide avec la touche **Entrée**, saut de ligne avec **Maj + Entrée**.
    *   **Indicateurs Temps Réel** :
        *   Statut "En train d'écrire..." visible par le destinataire.
        *   Mise à jour instantanée du statut du partenaire (En ligne, Occupé, etc.) dans l'en-tête.
        *   **Ouverture Automatique** : Les fenêtres de chat s'ouvrent automatiquement à la réception d'un message ou d'un **BUZZ**, aussi bien pour les Clients que pour les Admins.
    *   **Expérience Visuelle** :
        *   Bulles de messages aux couleurs modernes (Bleu Pastel `#E3F2FD` pour l'expéditeur).
        *   Sélecteur de couleurs ergonomique (Popup s'ouvrant vers le haut) avec une palette moderne.
        *   Affichage des noms au format "Nom Prénom".
        *   **Message de Statut** : Affichage centralisé du statut du partenaire (ex: "L'utilisateur est En ligne") positionné après l'historique des messages.
    *   **Notifications Sonores** :
        *   Son de notification ("Tink") discret et moderne lors de la réception d'un message.
        *   **Gestion Intelligente** : Le son se joue à l'ouverture automatique d'une fenêtre ou si la fenêtre est en arrière-plan, mais reste silencieux si l'utilisateur est actif sur la conversation.
*   **Gestion des Sessions et Statuts** :
    *   **Suivi en Temps Réel** : Système de sessions (`UserSessions`) traquant l'IP, le nom de la machine et le statut de connexion.
    *   **Synchronisation** : Mise à jour automatique des statuts (En ligne, Absent, Occupé, etc.) dans la liste d'amis toutes les 5 secondes.
    *   **Gestion des Doublons** : Logique robuste pour éviter les doublons dans la liste d'amis lors des changements de statut.
    *   **Déconnexion Sécurisée** : Fermeture automatique de toutes les fenêtres actives (Chat, Profils, etc.) lors de la déconnexion pour garantir une fin de session propre.
*   **Gestion des Blocages** :
    *   **Blocage Hiérarchique** : Système de sécurité basé sur les rôles (Niveau 1 à 7). Un utilisateur ne peut pas bloquer un supérieur hiérarchique.
    *   **Types de Blocage** : Permanent, 7 jours, ou durée personnalisée.
    *   **Interface de Gestion** : Fenêtre dédiée pour voir, modifier (durée/raison) ou lever les blocages.
*   **Profil Utilisateur** :
    *   Édition complète du profil (Avatar, Nom, Prénom, Genre, Pays, Date de naissance).
    *   Indicateur de complétion du profil.
*   **Interface Utilisateur (UI)** :
    *   **MainView** : Interface principale post-login.
    *   **En-tête** : Avatar et pseudo de l'utilisateur.
    *   **Gestion de Statut** : Menu déroulant avec indicateurs de couleur (En ligne, Occupé, Absent, En appel, Ne pas déranger, Hors ligne).
    *   **Barre d'outils** : Accès rapide aux paramètres, ajout d'amis, utilisateurs bloqués et déconnexion.
*   **Base de Données** :
    *   Intégration avec **PostgreSQL**.
    *   Tables : Users, Roles, UserRoles, UserProfiles, Friendships, BlockedUsers, UserSessions.

## 🛠 Prérequis et Installation

1.  **Environnement** :
    *   .NET 10.0 SDK ou supérieur.
    *   Visual Studio 2022 ou VS Code.
    *   PostgreSQL.

2.  **Configuration de la Base de Données** :
    *   Assurez-vous que PostgreSQL est lancé.
    *   La chaîne de connexion se trouve dans DatabaseService.cs (dans les deux projets).
    *   Par défaut : Host=localhost;Username=postgres;Password=VotreMDP;Database=VotreDB.

3.  **Lancement** :
    *   Ouvrez le dossier dans VS Code ou la solution dans Visual Studio.
    *   Compilez et lancez le projet souhaité (PaLX.Client ou PaLX.Admin).

## 🔐 Identifiants par Défaut (Développement)

Si la base de données est initialisée via le DatabaseService, les utilisateurs par défaut ont le mot de passe suivant :
*   **Mot de passe** : 12345678

## 📝 Notes Techniques

*   **Navigation** : Le système utilise une navigation par fenêtres. Lors de la connexion réussie, MainWindow (Login) se ferme et MainView s'ouvre.
*   **Styles** : Utilisation de Segoe MDL2 Assets pour les icônes et de styles XAML pour une apparence moderne et épurée.

## 🤝 Contribution

Projet maintenu par [DeLTa-X-Tunisia](https://github.com/DeLTa-X-Tunisia).
---

## ⚖️ Licence & Copyright

```
Copyright © 2026 Azizi Mounir. Tous droits réservés.
```

### 🚫 Restrictions

Ce logiciel est la propriété exclusive de **Azizi Mounir**. 

**Il est strictement interdit de :**
- ❌ Copier, reproduire ou dupliquer le code source
- ❌ Modifier, adapter ou créer des œuvres dérivées
- ❌ Distribuer, publier ou partager le logiciel
- ❌ Utiliser le code à des fins commerciales ou personnelles sans autorisation
- ❌ Décompiler, désassembler ou effectuer de l'ingénierie inverse
- ❌ Supprimer ou modifier les mentions de copyright

### ✅ Utilisation autorisée

L'accès à ce dépôt est accordé **uniquement** pour :
- Consultation à des fins d'évaluation
- Collaboration avec autorisation écrite préalable

### ⚠️ Avertissement légal

Toute violation de ces termes peut entraîner des poursuites judiciaires conformément aux lois sur la propriété intellectuelle en vigueur.

Pour toute demande de licence ou autorisation, contactez : **Azizi Mounir** via [GitHub](https://github.com/DeLTa-X-Tunisia)

---

<p align="center">
  <b>🔒 PaL.Xtreme - Propriété de Azizi Mounir</b><br>
  <sub>Développé avec ❤️ en Tunisie 🇹🇳</sub>
</p>