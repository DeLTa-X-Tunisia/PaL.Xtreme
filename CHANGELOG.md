# Changelog - PaL.Xtreme

Toutes les modifications importantes de ce projet seront documentées dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

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
