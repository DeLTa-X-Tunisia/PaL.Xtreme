# 🗄️ Schéma de Base de Données - PaL.Xtreme

Documentation complète de la structure PostgreSQL de PaL.Xtreme.

## 📊 Vue d'Ensemble

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                         SCHÉMA DE BASE DE DONNÉES                               │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│    ┌─────────────┐         ┌─────────────┐         ┌─────────────┐             │
│    │    Users    │────────▶│   Friends   │◀────────│    Users    │             │
│    └──────┬──────┘         └─────────────┘         └─────────────┘             │
│           │                                                                     │
│           ▼                                                                     │
│    ┌─────────────┐         ┌─────────────┐         ┌─────────────┐             │
│    │UserSessions │         │  Messages   │◀───────▶│ FileTransfers│             │
│    └─────────────┘         └─────────────┘         └─────────────┘             │
│                                                                                 │
│    ┌─────────────┐         ┌─────────────┐         ┌─────────────┐             │
│    │    Rooms    │────────▶│ RoomMembers │◀────────│ RoomBans    │             │
│    └──────┬──────┘         └─────────────┘         └─────────────┘             │
│           │                                                                     │
│           ▼                                                                     │
│    ┌─────────────┐         ┌─────────────┐         ┌─────────────┐             │
│    │ RoomMessages│         │  BotConfigs │────────▶│ BotWarnings │             │
│    └─────────────┘         └──────┬──────┘         └─────────────┘             │
│                                   │                                             │
│                                   ▼                                             │
│                            ┌─────────────┐         ┌─────────────┐             │
│                            │ BannedWords │         │QuizQuestions│             │
│                            └─────────────┘         └─────────────┘             │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 👤 Tables Utilisateurs

### Users

Table principale des utilisateurs.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `Username` | VARCHAR(50) | NOT NULL, UNIQUE | Nom d'utilisateur |
| `Password` | VARCHAR(255) | NOT NULL | Hash BCrypt du mot de passe |
| `Email` | VARCHAR(100) | NOT NULL, UNIQUE | Adresse email |
| `Nickname` | VARCHAR(50) | | Surnom affiché |
| `Gender` | VARCHAR(10) | | Genre (Male, Female, Other) |
| `Age` | INTEGER | | Âge |
| `Country` | VARCHAR(50) | | Pays |
| `Bio` | TEXT | | Biographie |
| `AvatarUrl` | VARCHAR(255) | | URL de l'avatar |
| `RoleLevel` | INTEGER | DEFAULT 0 | Niveau de rôle système (0-6) |
| `IsBanned` | BOOLEAN | DEFAULT FALSE | Compte banni |
| `BanReason` | TEXT | | Raison du bannissement |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date de création |
| `LastLogin` | TIMESTAMP | | Dernière connexion |

**Index :**
- `idx_users_username` ON `Username`
- `idx_users_email` ON `Email`

---

### UserSessions

Sessions actives des utilisateurs.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `UserId` | INTEGER | FK → Users(Id) | Utilisateur |
| `DisplayedStatus` | INTEGER | DEFAULT 1 | Statut affiché (1-6) |
| `ConnectéLe` | TIMESTAMP | DEFAULT NOW() | Connexion |
| `DéconnectéLe` | TIMESTAMP | | Déconnexion |
| `IPAddress` | VARCHAR(50) | | Adresse IP |
| `DeviceInfo` | TEXT | | Info appareil |
| `IsInvisible` | BOOLEAN | DEFAULT FALSE | Mode invisible |

**Statuts :**
- 1 = En ligne
- 2 = Absent
- 3 = Occupé
- 4 = Ne pas déranger
- 5 = Apparaître hors ligne
- 6 = Hors ligne

---

### ProfileViews

Historique des consultations de profil.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `ViewedUserId` | INTEGER | FK → Users(Id) | Profil consulté |
| `ViewerUserId` | INTEGER | FK → Users(Id) | Visiteur |
| `ViewedAt` | TIMESTAMP | DEFAULT NOW() | Date de visite |
| `Context` | VARCHAR(50) | | Contexte (room, friends, search) |

---

## 💬 Tables Messagerie

### Messages

Messages privés entre utilisateurs.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `SenderUsername` | VARCHAR(50) | NOT NULL | Expéditeur |
| `ReceiverUsername` | VARCHAR(50) | NOT NULL | Destinataire |
| `Content` | TEXT | NOT NULL | Contenu du message |
| `Timestamp` | TIMESTAMP | DEFAULT NOW() | Date d'envoi |
| `IsRead` | BOOLEAN | DEFAULT FALSE | Lu par destinataire |
| `DeletedBySender` | BOOLEAN | DEFAULT FALSE | Supprimé par expéditeur |
| `DeletedByReceiver` | BOOLEAN | DEFAULT FALSE | Supprimé par destinataire |

**Index :**
- `idx_messages_sender_receiver` ON `(SenderUsername, ReceiverUsername)`

---

### FileTransfers

Transferts de fichiers entre utilisateurs.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `SenderUsername` | VARCHAR(50) | NOT NULL | Expéditeur |
| `ReceiverUsername` | VARCHAR(50) | NOT NULL | Destinataire |
| `FileName` | TEXT | | Nom du fichier |
| `FileUrl` | TEXT | NOT NULL | URL du fichier |
| `FileSize` | BIGINT | | Taille en octets |
| `Status` | INTEGER | DEFAULT 0 | 0=Pending, 1=Accepted, 2=Declined |
| `Timestamp` | TIMESTAMP | DEFAULT NOW() | Date |
| `IsRead` | BOOLEAN | DEFAULT FALSE | Lu |
| `DeletedBySender` | BOOLEAN | DEFAULT FALSE | Supprimé par expéditeur |
| `DeletedByReceiver` | BOOLEAN | DEFAULT FALSE | Supprimé par destinataire |

---

## 👥 Tables Amis

### Friends

Relations d'amitié entre utilisateurs.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `UserId` | INTEGER | FK → Users(Id) | Utilisateur |
| `FriendId` | INTEGER | FK → Users(Id) | Ami |
| `Status` | INTEGER | DEFAULT 0 | 0=Pending, 1=Accepted, 2=Blocked |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date de demande |

**Contrainte :** `UNIQUE (UserId, FriendId)`

---

## 🏠 Tables Salons

### Rooms

Salons de discussion.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `Name` | VARCHAR(100) | NOT NULL | Nom du salon |
| `Topic` | TEXT | | Sujet du salon |
| `Category` | VARCHAR(50) | | Catégorie |
| `OwnerId` | INTEGER | FK → Users(Id) | Propriétaire |
| `MaxMembers` | INTEGER | DEFAULT 100 | Capacité max |
| `IsPrivate` | BOOLEAN | DEFAULT FALSE | Salon privé |
| `Password` | VARCHAR(255) | | Mot de passe (si privé) |
| `Is18Plus` | BOOLEAN | DEFAULT FALSE | Contenu adulte |
| `IconPath` | VARCHAR(255) | | Icône du salon |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date de création |

**Paramètres d'entrée :**
- `DefaultCanChat` | BOOLEAN | DEFAULT TRUE |
- `DefaultCanMic` | BOOLEAN | DEFAULT TRUE |
- `DefaultCanCam` | BOOLEAN | DEFAULT FALSE |

---

### RoomMembers

Membres présents dans un salon.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | FK → Rooms(Id) | Salon |
| `UserId` | INTEGER | FK → Users(Id) | Utilisateur |
| `RoleId` | INTEGER | DEFAULT 5 | Rôle dans le salon |
| `JoinedAt` | TIMESTAMP | DEFAULT NOW() | Date d'entrée |
| `CanChat` | BOOLEAN | DEFAULT TRUE | Permission chat |
| `CanMic` | BOOLEAN | DEFAULT TRUE | Permission micro |
| `CanCam` | BOOLEAN | DEFAULT FALSE | Permission caméra |

**Rôles (RoleId) :**
- 1 = Owner (Propriétaire)
- 2 = SuperAdmin
- 3 = Admin
- 4 = Moderator
- 5 = Member (par défaut)

**Contrainte :** `UNIQUE (RoomId, UserId)`

---

### RoomMessages

Messages dans les salons.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | FK → Rooms(Id) | Salon |
| `UserId` | INTEGER | FK → Users(Id) | Auteur (NULL = système) |
| `Username` | VARCHAR(50) | | Nom d'affichage |
| `Content` | TEXT | NOT NULL | Contenu |
| `MessageType` | VARCHAR(20) | DEFAULT 'User' | Type de message |
| `SentAt` | TIMESTAMP | DEFAULT NOW() | Date d'envoi |
| `IsSystemMessage` | BOOLEAN | DEFAULT FALSE | Message système |
| `SystemHidden` | BOOLEAN | DEFAULT FALSE | Caché (kick/ban) |

**Types de messages :**
- `User` : Message utilisateur standard
- `System` : Entrée/sortie du salon
- `Bot` : Message du bot
- `BotWelcome` : Bienvenue
- `BotWarning` : Avertissement
- `BotQuiz` : Question quiz
- `Kick` : Expulsion
- `Ban` : Bannissement

---

### RoomBans

Bannissements de salon.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | FK → Rooms(Id) | Salon |
| `UserId` | INTEGER | FK → Users(Id) | Utilisateur banni |
| `BannedBy` | INTEGER | FK → Users(Id) | Bannisseur |
| `Reason` | TEXT | | Raison |
| `BannedAt` | TIMESTAMP | DEFAULT NOW() | Date |
| `ExpiresAt` | TIMESTAMP | | Expiration (NULL = permanent) |

**Index :**
- `idx_roombans_room_user` ON `(RoomId, UserId)`
- `idx_roombans_expires` ON `ExpiresAt`

---

## 🤖 Tables Bot IA

### BotConfigs

Configuration du bot par salon.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | FK → Rooms(Id), UNIQUE | Salon |
| `BotName` | VARCHAR(50) | DEFAULT 'PaLX Bot' | Nom du bot |
| `BotAvatarUrl` | VARCHAR(255) | | URL avatar |
| `IsEnabled` | BOOLEAN | DEFAULT TRUE | Bot activé |
| `WelcomeMessageEnabled` | BOOLEAN | DEFAULT TRUE | Bienvenue activée |
| `ModerationEnabled` | BOOLEAN | DEFAULT TRUE | Modération activée |
| `QuizEnabled` | BOOLEAN | DEFAULT FALSE | Quiz activé |
| `MentionResponseEnabled` | BOOLEAN | DEFAULT TRUE | Réponse aux mentions |
| `TopicSuggestionEnabled` | BOOLEAN | DEFAULT FALSE | Suggestions sujets |
| `WelcomeMessageTemplate` | TEXT | | Template bienvenue |
| `WarningMessageTemplate` | TEXT | | Template avertissement |
| `KickMessageTemplate` | TEXT | | Template expulsion |
| `WarningsBeforeKick` | INTEGER | DEFAULT 3 | Avertissements avant kick |
| `WarningResetMinutes` | INTEGER | DEFAULT 60 | Reset des avertissements |
| `QuizIntervalMinutes` | INTEGER | DEFAULT 30 | Intervalle quiz |
| `QuizTimeoutSeconds` | INTEGER | DEFAULT 60 | Timeout réponse |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Création |
| `UpdatedAt` | TIMESTAMP | DEFAULT NOW() | Mise à jour |

---

### BotWarnings

Avertissements donnés par le bot.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | FK → Rooms(Id) | Salon |
| `UserId` | INTEGER | FK → Users(Id) | Utilisateur averti |
| `Reason` | VARCHAR(500) | | Raison |
| `TriggerWord` | VARCHAR(100) | | Mot déclencheur |
| `OriginalMessage` | TEXT | | Message original |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date |
| `IsActive` | BOOLEAN | DEFAULT TRUE | Actif |

**Index :**
- `idx_botwarnings_room_user_active` ON `(RoomId, UserId, IsActive)` WHERE `IsActive = TRUE`

---

### BannedWords

Mots interdits par salon.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | FK → Rooms(Id) | Salon |
| `Word` | VARCHAR(100) | NOT NULL | Mot interdit |
| `Severity` | VARCHAR(20) | DEFAULT 'Warning' | Warning, Kick, Ban |
| `AddedBy` | INTEGER | FK → Users(Id) | Ajouté par |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date |

**Contrainte :** `UNIQUE (RoomId, Word)`

---

### QuizQuestions

Questions de quiz.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | DEFAULT 0 | 0 = global |
| `Question` | TEXT | NOT NULL | Question |
| `Answer` | VARCHAR(500) | NOT NULL | Réponse correcte |
| `Options` | TEXT[] | | Options QCM |
| `Category` | VARCHAR(50) | DEFAULT 'General' | Catégorie |
| `Points` | INTEGER | DEFAULT 10 | Points |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date |

---

### DiscussionTopics

Sujets de discussion.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | SERIAL | PRIMARY KEY | Identifiant unique |
| `RoomId` | INTEGER | DEFAULT 0 | 0 = global |
| `Topic` | TEXT | NOT NULL | Sujet |
| `Category` | VARCHAR(50) | DEFAULT 'General' | Catégorie |
| `CreatedAt` | TIMESTAMP | DEFAULT NOW() | Date |

---

## 🔐 Niveaux de Rôles Système

| Niveau | Nom | Description |
|--------|-----|-------------|
| 0 | User | Utilisateur standard |
| 1 | ServerMaster | Administrateur principal |
| 2 | ServerEditor | Éditeur de contenu |
| 3 | ServerSuperAdmin | Super administrateur |
| 4 | ServerAdmin | Administrateur |
| 5 | ServerModerator | Modérateur serveur |
| 6 | ServerHelp | Support/Aide |

---

## 📝 Notes Techniques

### Conventions de Nommage

- Tables : PascalCase pluriel (`Users`, `Rooms`, `Messages`)
- Colonnes : PascalCase (`UserId`, `CreatedAt`)
- Index : `idx_tablename_columns`
- Contraintes : `unique_table_column`, `fk_table_reference`

### Cascade de Suppression

- La suppression d'un `User` :
  - Supprime ses `UserSessions`
  - Supprime ses `Messages` (soft delete recommandé)
  - Met à NULL les références dans `BannedWords.AddedBy`

- La suppression d'un `Room` :
  - Supprime `RoomMembers`
  - Supprime `RoomMessages`
  - Supprime `RoomBans`
  - Supprime `BotConfigs` et tables liées

### Performance

- Index sur les colonnes fréquemment recherchées
- Utilisation de `WHERE` partiel sur les index
- Pagination recommandée pour les listes

---

**Dernière mise à jour :** v1.8.9 - Janvier 2026
