# Changelog - PaL.Xtreme

Toutes les modifications importantes de ce projet seront documentées dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

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
