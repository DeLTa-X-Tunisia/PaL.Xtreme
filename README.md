# PaL.Xtreme

PaL.Xtreme est une solution de messagerie instantanée moderne développée en WPF (.NET 10.0), inspirée de l'interface de Paltalk Messenger. Le projet est divisé en deux applications distinctes : une pour les clients et une pour les administrateurs, partageant une base de données PostgreSQL commune.

## 🏗 Structure du Projet

La solution se compose de deux projets principaux :

*   **PaLX.Client** : L'application destinée aux utilisateurs finaux. Elle permet de se connecter, de gérer son statut (En ligne, Occupé, etc.) et de voir sa liste d'amis.
*   **PaLX.Admin** : L'application d'administration. Elle offre une interface similaire mais adaptée aux besoins de gestion (rôles 1 à 6), avec une identification visuelle distincte (icône bouclier).

## 🚀 Fonctionnalités

*   **Authentification Sécurisée** :
    *   Système de Login et d'Inscription.
    *   Hachage des mots de passe utilisant **BCrypt** pour une sécurité maximale.
*   **Gestion des Rôles** :
    *   Séparation stricte entre les utilisateurs standards et les administrateurs via la base de données.
*   **Interface Utilisateur (UI)** :
    *   **MainView** : Interface principale post-login.
    *   **En-tête** : Avatar et pseudo de l'utilisateur.
    *   **Gestion de Statut** : Menu déroulant avec indicateurs de couleur (En ligne, Occupé, Absent, En appel, Ne pas déranger, Hors ligne).
    *   **Liste d'Amis** : Affichage stylisé des contacts.
    *   **Barre d'outils** : Accès rapide aux paramètres, ajout d'amis et déconnexion.
*   **Base de Données** :
    *   Intégration avec **PostgreSQL**.
    *   Tables : `Users`, `Roles`, `UserRoles`.

## 🛠 Prérequis et Installation

1.  **Environnement** :
    *   .NET 10.0 SDK ou supérieur.
    *   Visual Studio 2022 ou VS Code.
    *   PostgreSQL.

2.  **Configuration de la Base de Données** :
    *   Assurez-vous que PostgreSQL est lancé.
    *   La chaîne de connexion se trouve dans `DatabaseService.cs` (dans les deux projets).
    *   Par défaut : `Host=localhost;Username=postgres;Password=admin;Database=PaLXtreme`.

3.  **Lancement** :
    *   Ouvrez le dossier dans VS Code ou la solution dans Visual Studio.
    *   Compilez et lancez le projet souhaité (`PaLX.Client` ou `PaLX.Admin`).

## 🔐 Identifiants par Défaut (Développement)

Si la base de données est initialisée via le `DatabaseService`, les utilisateurs par défaut ont le mot de passe suivant :
*   **Mot de passe** : `12345678`

## 📝 Notes Techniques

*   **Navigation** : Le système utilise une navigation par fenêtres. Lors de la connexion réussie, `MainWindow` (Login) se ferme et `MainView` s'ouvre.
*   **Styles** : Utilisation de `Segoe MDL2 Assets` pour les icônes et de styles XAML pour une apparence moderne et épurée.

## 🤝 Contribution

Projet maintenu par [DeLTa-X-Tunisia](https://github.com/DeLTa-X-Tunisia).
