# PaL.Xtreme

PaL.Xtreme est une solution de messagerie instantanée moderne développée en WPF (.NET 10.0), inspirée de l'interface de Paltalk Messenger. Le projet est divisé en deux applications distinctes : une pour les clients et une pour les administrateurs, partageant une base de données PostgreSQL commune.

## 🏗 Structure du Projet

La solution se compose de deux projets principaux :

*   **PaLX.Client** : L'application destinée aux utilisateurs finaux. Elle permet de se connecter, de gérer son statut, de gérer ses amis et son profil.
*   **PaLX.Admin** : L'application d'administration. Elle offre les mêmes fonctionnalités sociales que le client, adaptées aux besoins de gestion (rôles 1 à 6), avec une identification visuelle distincte.

## 🚀 Fonctionnalités

*   **Authentification Sécurisée** :
    *   Système de Login et d'Inscription.
    *   Hachage des mots de passe utilisant **BCrypt** pour une sécurité maximale.
*   **Gestion des Rôles** :
    *   Séparation stricte entre les utilisateurs standards et les administrateurs via la base de données.
*   **Gestion des Amis** :
    *   **Recherche** : Recherche d'utilisateurs par pseudo ou email.
    *   **Demandes** : Envoi, réception, acceptation et refus de demandes d'amis.
    *   **Liste d'Amis** : Affichage en temps réel avec statut de connexion synchronisé.
        *   **Tri Intelligent** : Les utilisateurs en ligne apparaissent en premier (nom en **Gras**), suivis des utilisateurs hors ligne.
        *   **Synchronisation Instantanée** : Mise à jour immédiate lors de l'ajout d'amis et rafraîchissement rapide (toutes les 2 secondes) pour les statuts.
        *   **Indicateurs Visuels** : Texte de statut coloré selon l'état (Vert, Orange, Rouge...) et effet de **clignotement** (durée de 5 secondes) lorsqu'un ami change de statut.
*   **Gestion des Sessions et Statuts** :
    *   **Suivi en Temps Réel** : Système de sessions (`UserSessions`) traquant l'IP, le nom de la machine et le statut de connexion.
    *   **Synchronisation** : Mise à jour automatique des statuts (En ligne, Absent, Occupé, etc.) dans la liste d'amis toutes les 5 secondes.
    *   **Gestion des Doublons** : Logique robuste pour éviter les doublons dans la liste d'amis lors des changements de statut.
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
