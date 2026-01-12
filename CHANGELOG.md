# Changelog - PaL.Xtreme

Toutes les modifications importantes de ce projet seront documentées dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

---

## [1.8.0] - 2026-01-12

### 🧹 Suppression Message "A levé la main"

#### Nettoyage du Chat
- **Message supprimé** : "User A a levé la main ✋" n'apparaît plus dans le chat
- **Raison** : Redondant car l'indicateur visuel 🤚 est déjà présent dans la liste des membres
- **Avantage** : Chat plus propre, moins encombré par les actions répétitives

### 🔧 Fichiers Modifiés
- `PaLX.Client/RoomWindow.xaml.cs` : Suppression de AddSystemMessage pour hand raised

---

## [1.7.9] - 2026-01-12

### 🎨 Header Chatroom Réorganisé

#### Nouvelle Structure à 2 Lignes
- **Ligne 1** : Nom du salon, catégorie, badges (18+, invisible), owner + durée, contrôles fenêtre
- **Ligne 2** : Compteurs participants (total, hommes, femmes, autres) + icône modération
- **Meilleure lisibilité** : Plus d'espace, éléments bien séparés
- **Design compact** : Tailles de police et icônes optimisées

### 🔐 Correction Déconnexion

#### Retour à l'Écran de Connexion
- **Avant** : Le bouton "Se déconnecter" fermait complètement l'application
- **Après** : Retour propre à la fenêtre de connexion (LoginForm)
- **Solution** : Définir `Application.Current.MainWindow` avant fermeture de MainView
- **Prévention shutdown** : L'app ne se ferme plus grâce au changement de MainWindow

### 🔧 Fichiers Modifiés
- `PaLX.Client/RoomWindow.xaml` : Header 2 lignes, layout restructuré
- `PaLX.Client/MainView.xaml.cs` : Logout_Click corrigé pour retour au login

---

## [1.7.8] - 2026-01-12

### 🔧 Améliorations Bouton Gestion du Salon

#### Correction de la Fenêtre Cible
- **Fenêtre correcte** : L'icône Shield ouvre maintenant "Modifier le Salon" (CreateRoomWindow)
- **Avant** : Ouvrait incorrectement RoomStudioWindow (création de salon)
- **Après** : Ouvre CreateRoomWindow en mode édition avec les données du salon actuel

#### Fenêtre Non-Bloquante
- **Mode non-modal** : `Show()` au lieu de `ShowDialog()`
- **Interaction libre** : L'utilisateur peut continuer à chatter pendant la modification
- **Fenêtre flottante** : Peut être déplacée, réduite, superposée au salon

### 🔧 Fichiers Modifiés
- `PaLX.Client/RoomWindow.xaml.cs` : Correction du handler RoomSettings_Click

---

## [1.7.7] - 2026-01-12

### 🛡️ Bouton Gestion du Salon (Room Settings)

#### Nouvelle Icône Shield
- **Position** : Coin supérieur droit du header de la chatroom
- **Design** : Icône Shield moderne avec engrenage intégré (SVG cyan #4FC3F7)
- **Action** : Ouvre la fenêtre RoomStudioWindow pour modifier le salon
- **Tooltip** : "Modifier le salon"

#### Visibilité Contrôlée
- **Admins Système** :
  - ServerMaster (RoleLevel 1)
  - ServerEditor (RoleLevel 2)
  - ServerSuperAdmin (RoleLevel 3)
  - ServerAdmin (RoleLevel 4)
  - ServerModerator (RoleLevel 5)
  - ServerHelp (RoleLevel 6)

- **Admins du Salon** :
  - RoomOwner (propriétaire)
  - RoomSuperAdmin
  - RoomAdmin
  - RoomModerator

#### Méthodes Ajoutées
- `CanUserManageRoom()` : Vérifie si l'utilisateur peut gérer le salon
- `UpdateRoomSettingsButtonVisibility()` : Met à jour la visibilité du bouton
- `RoomSettings_Click()` : Handler pour ouvrir RoomStudioWindow

### 🔧 Fichiers Modifiés
- `PaLX.Client/RoomWindow.xaml` : Bouton Shield SVG dans le header
- `PaLX.Client/RoomWindow.xaml.cs` : Logique de visibilité et handlers

---

## [1.7.6] - 2026-01-12

### 🤫 Chuchotement (Whisper) - Messages Privés en Chatroom

#### Fonctionnalité de Chuchotement
- **Bouton Chuchoter** : Nouveau bouton dans le menu contextuel des membres (icône 🤫)
- **WhisperWindow** : Fenêtre modale élégante avec thème sombre (#252525)
- **Envoi Direct** : Chuchotement envoyé directement au destinataire via SignalR
- **Format Distinctif** : Bordure violette (#9C27B0) pour identifier les chuchotements

#### Affichage des Chuchotements
- **Chuchotement Envoyé** : Style rouge avec encadrement `═══ Chuchotement envoyé à [Nom] ═══`
- **Chuchotement Reçu** : Style bleu avec encadrement `═══ Chuchotement reçu de [Nom] ═══`
- **RichMessageTextBlock** : Rendu spécial pour les whispers avec formatage italique

#### Vue Modérateur des Chuchotements
- **Rôles Autorisés** : ServerMaster, ServerEditor, ServerSuperAdmin, ServerAdmin, ServerModerator, ServerHelp (RoleLevel 1-6)
- **Visibilité Complète** : Les modérateurs voient TOUS les chuchotements du salon
- **Format Modération** : `═══ [MOD] Chuchotement de Sender → Recipient ═══`
- **Style Distinctif** : Violet pour cadre, orange pour expéditeur, cyan pour destinataire

#### Backend SignalR
- **SendWhisper** : Nouvelle méthode Hub pour envoyer des chuchotements
- **WhisperReceived** : Event pour notifier le destinataire
- **WhisperModView** : Event séparé pour la vue modérateur
- **Tracking RoleLevel** : Le niveau de rôle est maintenant suivi dans les connexions

### 🎨 Modernisation des Icônes Header Chatroom

#### Icônes SVG Colorées
- **Participants** : Icône groupe avec couleur verte pastel (#81C784)
- **Hommes** : Icône homme avec couleur bleue pastel (#64B5F6)
- **Femmes** : Icône femme avec couleur rose pastel (#F48FB1)
- **Autres** : Icône neutre avec couleur orange pastel (#FFB74D)
- **Couronne Owner** : Icône dorée (#FFD700)
- **Horloge Durée** : Icône cyan (#00BCD4)

#### Améliorations Visuelles
- **Owner en Gras** : Le nom du propriétaire du salon est maintenant en gras
- **Tooltip Enrichis** : Infobulles descriptives sur chaque compteur
- **Consistance** : Toutes les icônes utilisent maintenant des Path SVG modernes

### 🔧 Fichiers Modifiés
- `PaLX.API/Hubs/RoomHub.cs` : SendWhisper, WhisperModView, tracking RoleLevel
- `PaLX.Client/Services/ApiService.cs` : Events OnWhisperReceived, OnWhisperModView
- `PaLX.Client/RoomWindow.xaml` : Bouton chuchoter, header icons modernisés
- `PaLX.Client/RoomWindow.xaml.cs` : Handlers whisper, DisplayModeratorWhisper
- `PaLX.Client/WhisperWindow.xaml` : Nouvelle fenêtre de chuchotement
- `PaLX.Client/WhisperWindow.xaml.cs` : Code-behind WhisperWindow
- `PaLX.Client/Controls/RichMessageTextBlock.cs` : RenderWhisperMod

---

## [1.7.5] - 2026-01-11

### 💬 RichText & Smileys dans Chatroom

#### Zone de Saisie Enrichie
- **RichTextBox** : Remplace le simple TextBox par un RichTextBox complet
- **Barre de formatage** : Boutons Gras (B), Italique (I), Souligné (U)
- **Sélecteur de couleur** : 14 couleurs modernes (popup au-dessus)
- **Bouton Emoji** : Ouvre le panneau de smileys (popup)
- **Préservation du formatage** : Le style est conservé après envoi

#### Smileys/Émoticônes
- **Même collection** : Utilise les 41 smileys du dossier `Smiley/pxt_01`
- **Popup intégré** : Panneau scrollable de 280px de large
- **Insertion inline** : Les smileys s'insèrent à la position du curseur
- **Format [smiley:xxx]** : Compatible avec le système existant

#### Affichage des Messages
- **RichMessageTextBlock** : Les messages utilisent le contrôle personnalisé
- **Rendu HTML** : Support `<b>`, `<i>`, `<u>`, `<span style='color:...'>`
- **Rendu Smileys** : Les tags `[smiley:pxt_01/N.png]` affichent les images
- **Cohérence** : Même rendu que dans le chat privé

### 🔧 Fichiers Modifiés
- `PaLX.Client/RoomWindow.xaml` : Zone de saisie RichText, formatage, popup smileys
- `PaLX.Client/RoomWindow.xaml.cs` : Handlers formatage, smileys, conversion HTML
- Template messages : Utilise `controls:RichMessageTextBlock` au lieu de `TextBlock`

---

## [1.7.4] - 2026-01-11

### 🎥 Correction Vidéo Chatroom - Peer Video

#### Correction Connexion SignalR
- **RoomHubConnection explicite** : Nouvelle propriété `RoomHubConnection` dans ApiService pour les opérations de chatroom
- **Correction RoomVideoPeerService** : Utilise maintenant explicitement `RoomHubConnection` au lieu de `HubConnection` générique
- **Correction critique** : Les frames vidéo étaient envoyées sur ChatHub au lieu de RoomHub - corrigé

#### Amélioration Fenêtre Vidéo Chatroom
- **Barre de contrôle transparente** : Overlay semi-transparent (#60000000) au-dessus de la vidéo
- **Bouton changement de caméra** : Icône 🔄 avec menu contextuel pour sélectionner la caméra
- **Liste dynamique des caméras** : Détection automatique des périphériques disponibles

#### Optimisations Performance
- **Frame limiting** : Maximum 2 frames en attente (`MAX_PENDING_FRAMES`) pour éviter saturation ThreadPool
- **Initialisation non-bloquante** : La caméra s'initialise en arrière-plan via `CameraCaptureLoopWithInit`
- **Logging détaillé** : Traçage complet du flux vidéo (envoi, réception, décodage)

#### Événements Centralisés
- **ApiService Events** : `OnRoomCameraStarted`, `OnRoomCameraStopped`, `OnRoomVideoFrame` centralisés
- **Handlers uniques** : Évite les handlers SignalR multiples en centralisant dans ApiService
- **PeerVideoWindow simplifié** : Reçoit les frames via `UpdateVideoFrame()` appelé par RoomWindow

### 🔧 Fichiers Modifiés
- `PaLX.Client/Services/ApiService.cs` : Ajout propriété `RoomHubConnection`, logging événements vidéo
- `PaLX.Client/Services/RoomVideoPeerService.cs` : Utilise `RoomHubConnection`, frame limiting, logging
- `PaLX.Client/RoomWindow.xaml.cs` : Vérification `RoomHubConnection`
- `PaLX.Client/RoomVideoWindow.xaml` : Barre contrôle transparente, bouton caméra switcher
- `PaLX.Client/RoomVideoWindow.xaml.cs` : Logique changement de caméra
- `PaLX.Client/PeerVideoWindow.xaml.cs` : Simplifié pour utiliser événements centralisés
- `PaLX.API/Hubs/RoomHub.cs` : Logging `SendRoomVideoFrame`

---

## [1.7.3] - 2026-01-11

### 🔐 Contrôle de Session Unique

#### Détection de Session Active
- **Vérification à la connexion** : Détecte si l'utilisateur est déjà connecté sur un autre appareil
- **Informations détaillées** : Affiche le nom de l'appareil, l'IP et l'heure de connexion de la session existante
- **Fenêtre élégante** : `AlreadyConnectedWindow` avec design moderne (coins arrondis, ombre portée)

#### Force Connect
- **Prise de contrôle** : Option "Se connecter ici" pour déconnecter l'ancienne session
- **Signal ForceDisconnect** : Notification SignalR envoyée à l'ancien client
- **Fermeture propre** : L'ancien client affiche un message explicatif avant de se fermer

#### Transaction Atomique Anti-Race Condition
- **Approche robuste** : Création de la nouvelle session AVANT fermeture des anciennes
- **PostgreSQL Transaction** : Opérations groupées dans une seule transaction
- **Pas de fenêtre de vulnérabilité** : Impossible de contourner le contrôle même en se reconnectant très rapidement

### 🪟 Nouvelles Fenêtres
- `AlreadyConnectedWindow.xaml/.cs` : Dialogue de confirmation pour forcer la connexion
- `SessionKickedWindow.xaml/.cs` : Notification élégante quand on est déconnecté par une autre session

### 🛠️ Améliorations Techniques
- **Coins arrondis VideoCallWindow** : Correction du clipping avec `ClipToBounds="True"`
- **Avatars Chatroom** : Méthode `BuildAvatarUrl()` pour construire les URLs complètes
- **IsConnected RoomMembers** : Colonne pour tracker la présence réelle en temps réel

### 🔧 Fichiers Modifiés
- `PaLX.API/Services/AuthService.cs` : Logique de session unique avec transaction atomique
- `PaLX.API/Models/AuthResult.cs` : Propriétés `IsAlreadyConnected`, `ActiveSession*`
- `PaLX.API/Models/LoginModel.cs` : Propriété `ForceConnect`
- `PaLX.Client/Services/ApiService.cs` : Handler `ForceDisconnect`, événement `OnForceDisconnect`
- `PaLX.Client/LoginView.xaml.cs` : Gestion du flux de session avec `AlreadyConnectedWindow`
- `PaLX.Client/MainView.xaml.cs` : Handler `OnForceDisconnect` pour fermeture propre
- `PaLX.Client/RoomWindow.xaml.cs` : Méthode `BuildAvatarUrl()` pour avatars
- `PaLX.Client/VideoCallWindow.xaml` : Fix coins arrondis

---

## [1.7.2] - 2026-01-10

### 📐 Optimisation Dimensions Fenêtre Vidéo

#### Taille Réduite
- **Nouvelles dimensions** : 900x600 pixels (au lieu de 1080x720)
- **Minimum réduit** : 650x450 pixels (au lieu de 800x550)
- **Meilleure ergonomie** : Fenêtre moins intrusive, laisse plus d'espace écran

### 🔧 Fichiers Modifiés
- `PaLX.Client/VideoCallWindow.xaml` : Ajustement des dimensions de la fenêtre

---

## [1.7.1] - 2026-01-10

### 🎨 Amélioration Interface Appel Vidéo

#### Barre de Contrôle Transparente
- **Overlay transparent** : La barre de contrôle (micro, caméra, écran, raccrocher) est maintenant transparente
- **Vue vidéo complète** : Plus de bande noire en bas de la fenêtre vidéo
- **Contrôles flottants** : Les boutons flottent élégamment au-dessus de la vidéo
- **Effet visuel allégé** : Ombre réduite pour un look plus discret et moderne

### 🔧 Fichiers Modifiés
- `PaLX.Client/VideoCallWindow.xaml` : Refonte de la barre de contrôle en overlay transparent

---

## [1.7.0] - 2026-01-09

### 🎥 Refonte Complète des Appels Vidéo (MixedReality.WebRTC)

#### Migration vers MixedReality.WebRTC
- **Nouveau moteur WebRTC** : Migration complète de l'ancien encodeur VP8 vers MixedReality.WebRTC v2.0.2
- **Démarrage caméra ultra-rapide** : Plus de délai d'1+ minute au démarrage - la caméra démarre instantanément
- **APIs natives Windows** : Utilisation des APIs natives pour de meilleures performances

#### Audio & Vidéo Bidirectionnels
- **Audio 100% fonctionnel** : Transmission audio bidirectionnelle parfaite entre appelant et appelé
- **Vidéo locale et distante** : Affichage correct des deux flux vidéo
- **Support I420A et ARGB32** : Détection automatique du format de frame de la caméra

#### Partage d'Écran
- **Partage d'écran fonctionnel** : Capture et transmission de l'écran principal
- **ExternalVideoTrackSource** : Utilisation de source externe pour le partage d'écran
- **Arrêt propre** : Nettoyage correct des ressources lors de l'arrêt du partage

#### Contrôles Média
- **Mute micro** : Désactivation/réactivation du microphone pendant l'appel
- **Pause caméra** : Mise en pause de la caméra sans crash
- **Synchronisation distant** : La pause caméra est visible des deux côtés de l'appel

#### Gestion des Appels
- **Statut "En appel" corrigé** : Le statut revient à "En ligne" après raccrochage
- **Rappel possible** : Plus de blocage "Utilisateur en appel" après un appel terminé
- **Nettoyage async** : Fermeture de fenêtre sans freeze grâce au cleanup non-bloquant

### 🗑️ Suppression de l'Ancien Encodeur
- Supprimé : `VP8Encoder.cs`, `VP8Decoder.cs`, `VP8Native.cs`, `IVideoEncoder.cs`, `IVideoDecoder.cs`
- Plus de dépendance à libvpx - tout est géré par MixedReality.WebRTC

### 🔧 Fichiers Modifiés
- `PaLX.Client/Services/VideoCallService.cs` : Réécrit entièrement (~1000 lignes)
- `PaLX.Client/VideoCallWindow.xaml.cs` : Ajout gestion pause vidéo partenaire
- `PaLX.Client/PaLX.Client.csproj` : Ajout package MixedReality.WebRTC

---

## [1.6.7] - 2026-01-09

### 🎨 Thème Dynamique - Fenêtre de Modération

#### Harmonisation Visuelle Complète
- **Support thème clair/sombre** : La fenêtre de modération s'adapte automatiquement au thème global
- **Ressources dynamiques** : Utilisation de `DynamicResource` pour tous les éléments de couleur
- **Header amélioré** : Fond gris clair doux au lieu du blanc pur (thème clair)

#### Couleurs Pastel pour les Rôles (Thème Clair)
- 🟣 **SuperAdmin** : Violet lavande `#E8E0F0`
- 🔴 **Admin** : Rose poudré `#FCE4E4`
- 🔵 **Moderator** : Bleu ciel `#E0F0F8`

### 🔧 Fichiers Modifiés
- `PaLX.Client/RoomModerationWindow.xaml` : Migration vers ressources dynamiques + teintes pastel harmonisées

---

## [1.6.6] - 2026-01-09

### 🔊 Son de Démarrage par Rôle

#### Sons Personnalisés selon le Niveau d'Administration
- **ServerMaster (Niveau 1)** : Son exclusif `master_start.mp3`
- **Administrateurs (Niveaux 2-6)** : Son `admin_start.mp3`
  - ServerEditor (2), ServerSuperAdmin (3), ServerAdmin (4), ServerModerator (5), ServerHelp (6)
- **Utilisateur (Niveau 7)** : Son standard `client_start.mp3` (inchangé)

### 💬 Chatroom Sans Historique

#### Nouvelle Expérience Chatroom
- **Pas d'historique** : Les utilisateurs rejoignant un salon ne voient plus les anciens messages
- **Fenêtre blanche** : Affichage propre avec uniquement le message de bienvenue
- **Message de bienvenue** : "Bienvenu dans votre salon [Nom]" affiché à l'arrivée
- **Chats privés préservés** : L'historique reste visible dans les conversations privées

### 🔧 Fichiers Modifiés
- `PaLX.Client/MainView.xaml.cs` : Logique de son de démarrage basée sur le rôle utilisateur
- `PaLX.Client/RoomWindow.xaml.cs` : Suppression du chargement de l'historique des messages

---

## [1.6.5] - 2026-01-08

### 🎬 Amélioration de la Qualité Vidéo

#### Encodeur VP8 Optimisé
- **Bitrate par défaut augmenté** : 500 → 1200 kbps pour une image plus nette
- **Keyframes périodiques** : Force un keyframe toutes les 60 frames (~2 sec) pour éviter l'accumulation d'artefacts et les lignes horizontales
- **Plage de bitrate élargie** : 300-8000 kbps (au lieu de 100-5000)

#### Capture Caméra Améliorée
- **Résolution dynamique** : Utilise la vraie résolution de la frame au lieu de forcer 640×480
- **Buffer réduit** : Réduit le lag vidéo
- **Format MJPEG** : Meilleure qualité de capture brute
- **Auto-exposition et autofocus** activés automatiquement

#### Presets de Qualité Optimisés
| Qualité | Résolution | Bitrate | FPS |
|---------|------------|---------|-----|
| 🐢 Basse | 640×480 | 800 kbps | 24 |
| ⚖️ Moyenne | 960×540 | 1500 kbps | 30 |
| 🚀 Haute | 1280×720 | 2500 kbps | 30 |

### ⚙️ Paramètres Persistants (SettingsService)

#### Nouveau Service de Paramètres
- **SettingsService** : Nouveau service pour sauvegarder automatiquement les préférences utilisateur
- **Stockage JSON** : Fichier `settings.json` dans `%AppData%\PaL.Xtreme\`
- **Sauvegarde automatique** : Chaque modification est sauvegardée instantanément

#### Paramètres Sauvegardés
- Mode sombre (DarkMode)
- Sons de notification (SoundNotifications)
- Son de démarrage (StartupSound)
- Caméra sélectionnée (SelectedCameraIndex)
- Qualité vidéo (VideoQuality)

### 🖥️ Interface Paramètres Améliorée
- **Sélecteur de qualité vidéo** : Nouveau dropdown avec 3 presets (Basse/Moyenne/Haute)
- **Affichage de la résolution** : Montre la config actuelle (ex: "960×540 @ 1500kbps")

### 🔧 Fichiers Modifiés
- `PaLX.Client/Services/SettingsService.cs` : Nouveau fichier - gestion des paramètres persistants
- `PaLX.Client/Services/Encoders/EncoderFactory.cs` : Optimisation VP8, keyframes périodiques
- `PaLX.Client/Services/VideoCallService.cs` : Résolution dynamique, paramètres caméra améliorés
- `PaLX.Client/SettingsWindow.xaml` : Ajout sélecteur qualité vidéo
- `PaLX.Client/SettingsWindow.xaml.cs` : Chargement/sauvegarde des paramètres

---

## [1.5.7.2] - 2026-01-08

### 🐛 Corrections de Bugs

#### Création de Salon - Erreur DialogResult
- **Problème** : Erreur "DialogResult ne peut être défini qu'après la création de Window et affiché en tant que boîte de dialogue" lors de la création d'un salon
- **Cause** : `CreateRoomWindow` était ouverte avec `Show()` (non-modal) mais utilisait `DialogResult = true`
- **Solution** : Suppression de `DialogResult = true`, utilisation simple de `Close()`

#### Bouton Supprimer Salon Non Fonctionnel
- **Problème** : Le bouton 🗑️ pour supprimer un salon ne fonctionnait pas
- **Cause** : Le XAML passait `Tag="{Binding}"` (un `RoomViewModel`), mais le code attendait `btn.Tag is int roomId`
- **Solution** : Le code gère maintenant les deux cas (`RoomViewModel` ou `int`) et affiche un message de confirmation avec le nom du salon

### 🔧 Fichiers Modifiés
- `PaLX.Client/CreateRoomWindow.xaml.cs` : Suppression de `DialogResult = true`
- `PaLX.Client/Controls/RoomListControl.xaml.cs` : Correction de `DeleteRoom_Click()` pour gérer le binding correct

---

## [1.5.7.1] - 2026-01-08

### 🐛 Correction du Mode Invisible
- **Invisibilité temps réel** : L'admin invisible n'est plus visible par les utilisateurs déjà présents dans le salon
  - Avant : Si un admin rejoignait en invisible un salon occupé, tout le monde le voyait via SignalR
  - Maintenant : L'événement `UserJoined` est envoyé uniquement aux admins de rang égal ou supérieur
  
### 🔧 Backend (API)
- **`NotifyVisibleMembersOnlyAsync()`** : Nouvelle méthode pour notifier sélectivement les membres éligibles
- **`GetUserSystemLevelAsync()`** : Helper pour récupérer le niveau système d'un utilisateur
- **`GetRoomMemberDetailsAsync()`** : Récupère maintenant `IsInvisible` depuis la base de données
- **Logique SignalR intelligente** : Si invisible → notifie seulement les admins éligibles, sinon → broadcast normal

---

## [1.5.7] - 2026-01-08

### ✨ Nouvelles fonctionnalités - Mode Invisible Admin
- **Mode Invisible pour Admins Système** : Les admins peuvent rejoindre un salon en mode invisible
  - 👻 Modal élégant de choix : "Normal" ou "Invisible"
  - En mode invisible, l'admin n'apparaît pas dans la liste des membres
  - Seuls les admins de rang **égal ou supérieur** peuvent voir les invisibles
  - Badge violet "👻 INVISIBLE" affiché dans le header du salon
  - Indicateur `👻` devant le nom des membres invisibles (pour ceux qui peuvent les voir)

### 🎯 Règles de Visibilité des Invisibles
- **ServerMaster (1)** : Voit TOUS les membres invisibles
- **ServerEditor (2)** : Voit les invisibles de niveau 2-6
- **ServerSuperAdmin (3)** : Voit les invisibles de niveau 3-6
- **ServerAdmin (4)** : Voit les invisibles de niveau 4-6
- **ServerModerator (5)** : Voit les invisibles de niveau 5-6
- **ServerHelp (6)** : Voit les invisibles de niveau 6
- **Utilisateurs normaux** : Ne voient AUCUN membre invisible

### 🔧 Base de Données
- **Nouvelle colonne `IsInvisible`** : `BOOLEAN DEFAULT FALSE` dans la table `RoomMembers`
- **Script SQL** : `add_invisible_mode.sql` pour la migration

### 🔧 Backend (API)
- **`JoinRoomAsync(isInvisible)`** : Paramètre pour activer le mode invisible
- **`GetRoomMembersAsync(requesterId)`** : Filtrage intelligent des membres invisibles selon le niveau du demandeur
- **`AddMemberToRoomInternal(isInvisible)`** : Stockage du mode invisible
- **`JoinRoomDto.IsInvisible`** : Nouveau champ dans le DTO

### 🔧 Frontend (Client)
- **`JoinRoomModeWindow.xaml`** : Modal moderne avec design sombre et 2 boutons (👁️ Normal / 👻 Invisible)
- **`ApiService.JoinRoomAsync(isInvisible)`** : Support du mode invisible
- **`RoomWindow`** : Badge "INVISIBLE" + indicateur 👻 dans la liste des membres
- **`RoomMemberViewModel.IsInvisible`** : Propriété pour l'état invisible
- **`RoomMemberDto.IsInvisible`** : Propriété pour recevoir l'état depuis l'API

---

## [1.5.6] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **Bouton Rouge "Cacher Salon (Admin)"** : Les admins système peuvent cacher un salon même au propriétaire
  - 🔴 Bouton `⛔/🚫` visible uniquement pour les admins système
  - Quand activé, le RoomOwner ne voit plus son propre salon
  - Seuls les admins système peuvent voir et gérer le salon caché
  - Confirmation de sécurité avant l'action

### 🔄 Mise à Jour Temps Réel
- **SignalR `RoomVisibilityChanged`** : Notification instantanée des changements de visibilité
  - Plus besoin de se reconnecter pour voir les changements
  - La liste des salons se rafraîchit automatiquement
  - Fonctionne pour les deux types de visibilité (Owner et Admin)

### 🔧 Base de Données
- **Nouvelle colonne `IsSystemHidden`** : `BOOLEAN DEFAULT FALSE` dans la table `Rooms`
- **Script SQL** : `add_system_hidden_column.sql` pour la migration

### 🔧 Backend (API)
- **`ToggleSystemHiddenAsync()`** : Nouvelle méthode pour le toggle admin
- **`GetRoomsAsync()`** : Logique de filtrage mise à jour
  - Admins système voient TOUT
  - `IsSystemHidden=TRUE` → invisible même pour le Owner
  - `IsActive=FALSE` → visible uniquement par Owner + admins
- **Endpoint** : `POST /api/room/{roomId}/toggle-system-hidden`

### 🔧 Frontend (Client)
- **`ApiService.ToggleSystemHiddenAsync()`** : Appel API pour le toggle admin
- **`ApiService.OnRoomVisibilityChanged`** : Événement SignalR pour temps réel
- **`RoomViewModel.IsSystemHidden`** : Propriété pour l'état admin-caché
- **`RoomListControl.xaml`** : Nouveau bouton rouge avec style distinct
- **`RoomListControl.xaml.cs`** : Handler `SystemHideRoom_Click` + abonnement SignalR

---

## [1.5.5] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **Accès Total pour les Rôles Système** : Les administrateurs serveur ont un accès complet à tous les salons
  - 🏆 **ServerMaster** (Niveau 1) - Accès total
  - ✏️ **ServerEditor** (Niveau 2) - Accès total  
  - 👑 **ServerSuperAdmin** (Niveau 3) - Accès total
  - ⚙️ **ServerAdmin** (Niveau 4) - Accès total
  - 🛡️ **ServerModerator** (Niveau 5) - Accès total

### 🔧 Permissions Accordées
Les rôles système peuvent maintenant sur **tous les salons** :
- ✏️ **Modifier le salon** (nom, description, catégorie, options)
- 🗑️ **Supprimer le salon** (même s'ils ne sont pas propriétaires)
- 👁️ **Cacher / Afficher le salon** (toggle visibilité)
- ⚙️ **Ouvrir la fenêtre de gestion** (tous les paramètres)
- 👥 **Ouvrir la fenêtre de modération** (gestion des rôles)

### 🔧 Implémentation Backend (API)
- **`IsSystemAdminAsync()`** : Nouvelle méthode pour vérifier si un utilisateur est admin système
- **`HasOwnerAccessAsync()`** : Vérifie si l'utilisateur est Owner OU admin système
- **`DeleteRoomAsync`** : Autorise les admins système
- **`UpdateRoomAsync`** : Autorise les admins système
- **`ToggleRoomVisibilityAsync`** : Autorise les admins système

### 🔧 Implémentation Frontend (Client)
- **`ApiService.IsSystemAdmin`** : Nouvelle propriété pour vérifier le rôle système (RoleLevel 1-5)
- **`RoomViewModel.HasOwnerAccess`** : Owner OU admin système
- **`RoomListControl.xaml`** : Boutons Delete/Visibility visibles pour `HasOwnerAccess`
- **`CreateRoomWindow.HasFullAccess`** : Permissions complètes pour Owner et admins système
- **`RoomModerationWindow`** : Accès complet à la modération pour les admins système

---

## [1.5.4] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **Affichage des Rôles Système** : Les admins serveur sont reconnus dans les salons
  - 🏆 **Maître du Serveur** (ServerMaster) - #FFD700
  - ✏️ **Éditeur** (ServerEditor) - #9B59B6
  - 👑 **Super Administrateur** (ServerSuperAdmin) - #E74C3C
  - ⚙️ **Administrateur** (ServerAdmin) - #3498DB
  - 🛡️ **Modérateur** (ServerModerator) - #2ECC71
  - 🤝 **Assistant** (ServerHelp) - #1ABC9C

### 🔧 Améliorations
- **Priorité d'affichage** : RoomOwner > SystemAdmin (niveau 1-6) > RoomRole
- **RoleDisplayMapper étendu** : Support des rôles système avec `GetSystemRoleInfo()`
- **Détection automatique** : Les admins système sont identifiés via `UserRoles` + `Roles`
- **Logs détaillés** : Messages console pour tracer l'identification des admins

---

## [1.5.3] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **Synchronisation Automatique des Rôles à l'Entrée** : Cohérence parfaite entre `RoomAdmins` et `RoomMembers`
  - À chaque entrée dans un salon, le système vérifie `RoomAdmins`
  - Le `RoleId` dans `RoomMembers` est automatiquement synchronisé
  - Plus besoin de quitter/re-rejoindre après attribution d'un rôle

### 🔧 Améliorations Backend
- **JoinRoomAsync amélioré** : Vérifie Owner → RoomAdmins → Member (dans cet ordre)
- **AssignRoleAsync** : Met à jour `RoomMembers.RoleId` en même temps que `RoomAdmins`
- **RemoveRoomRoleAsync** : Remet `RoleId` à Member (6) lors de la suppression
- **Logs détaillés** : Messages console pour tracer la synchronisation des rôles

### 🐛 Corrections
- Correction de l'affichage "Membre" au lieu du vrai rôle dans la room
- Les rôles attribués s'affichent maintenant immédiatement avec le bon DisplayName

---

## [1.5.2] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **DisplayName des Rôles** : Affichage des noms français à la place des noms techniques
  - `RoomOwner` → **Propriétaire du Salon** (🔴 #FF0000)
  - `RoomSuperAdmin` → **Super Administrateur** (🟠 #FF4500)
  - `RoomAdmin` → **Administrateur** (🟡 #FFA500)
  - `PowerUser` → **Utilisateur Avancé** (🟢 #008000)
  - `RoomModerator` → **Modérateur** (🔵 #0000FF)
  - `RoomMember` → **Membre** (⚫ #808080)

- **Synchronisation Temps Réel des Rôles** : Mise à jour instantanée dans la room
  - Événement SignalR `MemberRoleUpdated` pour notifier tous les membres
  - Le DisplayName, la couleur et l'icône se mettent à jour sans reconnexion
  - Message système affiché lors du changement de rôle

### 🔧 Améliorations Backend
- **RoleDisplayMapper** : Nouvelle classe utilitaire pour le mapping des rôles
- **Couleurs cohérentes** : Les couleurs proviennent du mapper (pas de la BDD)
- **Messages de room** : RoleName traduit aussi pour l'historique des messages

---

## [1.5.1] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **Système de Permissions Hiérarchiques** : Contrôle d'accès basé sur le rôle
  - **RoomOwner** : Accès complet (Nom, Description, Catégorie, 18+, Modération)
  - **SuperAdmin** : Peut attribuer Admin 🔧 et Moderator ⭐ (pas SuperAdmin 👑)
  - **Admin** : Peut attribuer Moderator 🔧 uniquement
  - **Moderator** : Aucun accès à la fenêtre de Modération
  - Champs du salon en lecture seule pour les non-propriétaires (opacité 0.6)

### 🔧 Améliorations
- **Filtrage visuel des boutons** : Seuls les boutons autorisés par le rôle sont affichés
- **Filtrage de la liste des admins** : Chaque rôle ne voit que les rôles inférieurs
- **Bouton Modération conditionnel** : Masqué pour les Moderators

### 🐛 Corrections
- Correction du doublon `</Button>` dans RoomModerationWindow.xaml
- Ajout des propriétés de visibilité dans les modèles `FriendItem` et `AdminItem`

---

## [1.5.0] - 2026-01-08

### ✨ Nouvelles fonctionnalités
- **Fenêtre de Modération Repensée** : Nouvelle interface à deux listes
  - **Liste "Amis disponibles"** : Affiche les amis sans rôle avec boutons d'attribution rapide
  - **Liste "Administrateurs du salon"** : Affiche les amis avec rôle et badge coloré
  - **Attribution en un clic** : Boutons 👑 (SuperAdmin), ⭐ (Admin), 🔧 (Moderator)
  - **Suppression rapide** : Bouton ❌ pour retirer un rôle instantanément

- **Synchronisation Temps Réel des Rôles** : Mise à jour instantanée via SignalR
  - **Notification RoleAssigned** : L'icône ✏️ apparaît immédiatement chez l'utilisateur
  - **Notification RoleRemoved** : L'icône ✏️ disparaît et la fenêtre d'édition se ferme
  - **Toast informatif** : "Vous êtes maintenant SuperAdmin 👑 du salon 'X'"
  - **Rafraîchissement automatique** : La liste des salons se met à jour instantanément

### 🔧 Améliorations Backend
- **Correction SignalR UserIdentifier** : Envoi des notifications au username (pas à l'ID numérique)
- **Correction SQL GetRoomRolesAsync** : Utilisation de `UserProfiles.FirstName/LastName` au lieu de `Users.DisplayName`
- **Debug Console** : Ajout de `AllocConsole()` pour le debugging WPF (à retirer en production)

### 🐛 Corrections
- **Bug persistance des rôles** : Les rôles restent maintenant visibles après reconnexion
- **Bug icône Modifier** : L'icône apparaît/disparaît en temps réel pour les admins
- **Bug notification SignalR** : Correction du mapping UserId → Username pour les notifications

---

## [1.4.0] - 2026-01-07

### ✨ Nouvelles fonctionnalités
- **Système de Rôles Simplifié** : Refonte complète de la gestion des rôles dans les salons
  - Nouvelle table unique `RoomAdmins` (remplace `RoomRoleRequests` + `RoomMemberRoles`)
  - Attribution directe des rôles par le propriétaire (plus de demande/acceptation)
  - Trois niveaux de rôles : SuperAdmin 👑, Admin ⭐, Moderator 🔧
  - Suppression immédiate des rôles en un clic

- **Permissions d'Édition par Rôle** : Gestion fine des droits d'accès
  - **RoomOwner** : Toutes les fonctions (Modifier, Cacher/Afficher, Supprimer)
  - **Admin/Moderator** : Accès à la fonction "Modifier" uniquement
  - **Utilisateur simple** : Aucun accès aux fonctions d'administration
  - Retrait automatique de l'accès si le rôle est révoqué

### 🔧 Améliorations Backend
- **API Simplifiée** :
  - `GET /rooms/{id}/roles` - Liste les admins d'un salon
  - `POST /rooms/{id}/roles/assign` - Attribution directe (UPSERT)
  - `DELETE /rooms/{id}/roles/{userId}` - Suppression directe
  - Suppression des endpoints obsolètes (SendRoleRequest, RespondToRoleRequest, etc.)
  - Ajout de `UserRole` dans `RoomDto` pour récupérer le rôle de l'utilisateur connecté

### 🗑️ Suppressions
- Table `RoomRoleRequests` supprimée (plus de workflow de demande)
- Table `RoomMemberRoles` supprimée (fusionnée dans `RoomAdmins`)
- Notifications SignalR pour les demandes de rôle supprimées
- Fenêtre `RoleRequestWindow` désactivée (attribution directe)

### 🐛 Corrections
- Correction du crash toast (ProgressBar.Width négative)
- Correction du blocage de fenêtre (ShowDialog → Show)
- Ajout de try-catch sur les handlers des boutons d'icône

---

## [1.3.0] - 2026-01-05

### ✨ Nouvelles fonctionnalités
- **Mode Sombre** : Système de thème complet Light/Dark
  - Toggle dans les paramètres pour basculer entre les thèmes
  - Sauvegarde automatique des préférences utilisateur
  - Palette dark moderne (#1A1A2E, #25253D, #EAEAEA)
  - Utilisation de DynamicResource pour changement instantané

- **Fenêtre Paramètres** : Nouvelle interface de configuration
  - Design épuré et compact sans scroll
  - Icônes colorées pour chaque option (🌙🔔🎵🔥)
  - Options : Mode Sombre, Sons notification, Son démarrage
  - Section À propos avec version et copyright
  - Fenêtre non-modale (ne bloque plus l'application)

### 🎨 Améliorations UI
- **Barre de Navigation Moderne** :
  - Design "floating" avec effet de profondeur
  - Boutons avec fond arrondi stylisé
  - Bouton central "+" mis en valeur avec ombre rouge
  - Hover effect moderne avec SurfaceBrush

- **Menu Contextuel Amélioré** (⚙️) :
  - Icônes colorées avec fond (bleu/violet/rouge)
  - Titres avec descriptions explicites
  - Padding et espacement optimisés

---

## [1.2.0] - 2026-01-05

### ✨ Nouvelles fonctionnalités
- **UserProfiles** : Nouveau design moderne avec layout 2 colonnes
  - Panneau gauche avec avatar (120x120) et gradient rouge
  - Formulaire sans scroll, contrôles plus grands
  - Interface harmonieuse et moderne

### 🐛 Corrections
- **Appel vocal** : Correction du bug où le destinataire continuait à sonner quand l'appelant raccrochait avant réponse
  - Ajout du suivi des appels sortants en attente (`_pendingOutgoingCalls`)
  - Le destinataire voit maintenant "*{Nom} a annulé l'appel*" et sa fenêtre se ferme

### 🎨 Améliorations UI
- **Transfert de fichiers** : Icônes personnalisées par type de fichier (Excel, Word, PDF, ZIP/RAR)
- **Chat** : Couleur bleue (#3498DB) pour les messages de déblocage
- **Modal Bloquer** : Affichage du DisplayName au lieu du username

---

## [1.1.0] - 2026-01-04

### 🐛 Corrections
- **Lecteur vidéo** : Correction du bug de fenêtre fantôme
- **Playback vidéo** : Améliorations UX

---

## [1.0.0] - 2026-01-03

### ✨ Nouvelles fonctionnalités
- **Chat modernisé** : Templates natifs WPF, lecteurs audio/vidéo intégrés
- **Gestion des salons** : Implémentation complète (API, Client, Admin)
- **Appels vocaux** : Système d'appel P2P avec WebRTC

---

## Versioning

- **MAJOR** (1.x.x) : Changements incompatibles avec les versions précédentes
- **MINOR** (x.1.x) : Nouvelles fonctionnalités rétro-compatibles  
- **PATCH** (x.x.1) : Corrections de bugs rétro-compatibles
