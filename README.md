# PaL.Xtreme

PaL.Xtreme est une solution de messagerie instantanée moderne développée en WPF (.NET 10.0), inspirée de l'interface de Paltalk Messenger. Le projet est divisé en deux applications distinctes : une pour les clients et une pour les administrateurs, partageant une base de données PostgreSQL commune.

## 🏗 Structure du Projet

La solution se compose de deux projets principaux :

*   **PaLX.Client** : L'application destinée aux utilisateurs finaux. Elle permet de se connecter, de gérer son statut, de gérer ses amis et son profil.
*   **PaLX.Admin** : L'application d'administration. Elle offre les mêmes fonctionnalités sociales que le client, adaptées aux besoins de gestion (rôles 1 à 6), avec une identification visuelle distincte.

## 🌟 Nouveautés & Améliorations Récentes

Voici un résumé des dernières fonctionnalités et optimisations intégrées au projet :

### 💬 Chat & Messagerie
*   **Formatage Riche** : Support complet du **Gras**, *Italique*, <u>Souligné</u> et de la **Couleur** du texte.
*   **Expérience Fluide** : Indicateur "En train d'écrire...", ouverture automatique des fenêtres de chat, et sons de notification intelligents (actifs uniquement si la fenêtre n'a pas le focus).
*   **Historique Visuel** : Bulles de messages distinctes et affichage centralisé des changements de statut du partenaire.

### 🛡️ Système de Blocage Avancé
*   **Hiérarchie Admin** : Implémentation d'une sécurité basée sur les rôles (Niveau 1 à 7). Un utilisateur ne peut bloquer qu'un utilisateur de rang inférieur.
*   **Flexibilité** : Options de blocage **Permanent**, **Temporaire** (7 jours) ou **Personnalisé** (date spécifique).
*   **Gestion** : Interface dédiée pour visualiser les utilisateurs bloqués, modifier la durée ou lever le blocage.

### 🚀 Launcher & Stabilité
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
        *   **Ouverture Automatique** : Les fenêtres de chat s'ouvrent automatiquement à la réception d'un message, aussi bien pour les Clients que pour les Admins.
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
    *   Par défaut : Host=localhost;Username=postgres;Password=2012704;Database=PaL.X.

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
