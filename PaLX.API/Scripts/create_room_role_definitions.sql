-- ═══════════════════════════════════════════════════════════════════════════════════
-- ROOM ROLES DEFINITIONS - v2.3.2
-- Table pour définir les rôles de salons personnalisables avec leurs permissions
-- ═══════════════════════════════════════════════════════════════════════════════════

-- Table des définitions de rôles (templates de rôles)
CREATE TABLE IF NOT EXISTS "RoomRoleDefinitions" (
    "Id" SERIAL PRIMARY KEY,
    "RoleLevel" INTEGER NOT NULL UNIQUE, -- 1=Owner, 2=SuperAdmin, 3=Admin, 4=PowerUser, 5=Moderator, 6=Member
    "RoleName" VARCHAR(50) NOT NULL UNIQUE, -- Technical name (RoomOwner, RoomAdmin, etc.)
    "DisplayName" VARCHAR(100) NOT NULL, -- Display name (Propriétaire du Salon)
    "Description" TEXT, -- Description du rôle
    "Icon" VARCHAR(50) NOT NULL DEFAULT 'user', -- Icône (crown, shield, user, etc.)
    "Color" VARCHAR(20) NOT NULL DEFAULT '#95A5A6', -- Couleur hex
    "IsSystem" BOOLEAN NOT NULL DEFAULT FALSE, -- Rôles système non supprimables
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "CreatedBy" INTEGER NULL REFERENCES "Users"("Id")
);

-- Table des permissions disponibles
CREATE TABLE IF NOT EXISTS "RoomPermissions" (
    "Id" SERIAL PRIMARY KEY,
    "PermissionKey" VARCHAR(100) NOT NULL UNIQUE, -- Technical key (manage_settings, kick_users, etc.)
    "DisplayName" VARCHAR(150) NOT NULL, -- Nom affiché (Modifier les paramètres du salon)
    "Description" TEXT, -- Description de la permission
    "Category" VARCHAR(50) NOT NULL DEFAULT 'general', -- Catégorie (general, moderation, media, members)
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

-- Table de liaison Rôles <-> Permissions (Many-to-Many)
CREATE TABLE IF NOT EXISTS "RoomRolePermissions" (
    "Id" SERIAL PRIMARY KEY,
    "RoleId" INTEGER NOT NULL REFERENCES "RoomRoleDefinitions"("Id") ON DELETE CASCADE,
    "PermissionId" INTEGER NOT NULL REFERENCES "RoomPermissions"("Id") ON DELETE CASCADE,
    "GrantedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT "UQ_RolePermissions" UNIQUE ("RoleId", "PermissionId")
);

-- Index pour les performances
CREATE INDEX IF NOT EXISTS "IX_RoomRoleDefinitions_Level" ON "RoomRoleDefinitions"("RoleLevel");
CREATE INDEX IF NOT EXISTS "IX_RoomRolePermissions_Role" ON "RoomRolePermissions"("RoleId");
CREATE INDEX IF NOT EXISTS "IX_RoomPermissions_Category" ON "RoomPermissions"("Category");

-- ═══════════════════════════════════════════════════════════════════════════════════
-- DONNÉES PAR DÉFAUT - Permissions disponibles
-- ═══════════════════════════════════════════════════════════════════════════════════

INSERT INTO "RoomPermissions" ("PermissionKey", "DisplayName", "Description", "Category") VALUES
-- Général
('manage_settings', 'Modifier les paramètres du salon', 'Peut modifier le nom, description, et autres paramètres du salon', 'general'),
('delete_room', 'Supprimer le salon', 'Peut supprimer définitivement le salon', 'general'),
('manage_subscriptions', 'Gérer les abonnements', 'Peut gérer les abonnements du salon', 'general'),
('configure_bot', 'Configurer le bot', 'Peut configurer le bot IA du salon', 'general'),
('access_studio', 'Accès au studio', 'Peut accéder au studio de diffusion', 'general'),
('view_stats', 'Voir les statistiques', 'Peut consulter les statistiques du salon', 'general'),

-- Rôles
('assign_all_roles', 'Attribuer tous les rôles', 'Peut attribuer tous les rôles du salon', 'roles'),
('assign_admin_roles', 'Attribuer les rôles Admin et inférieurs', 'Peut attribuer Admin, PowerUser, Mod, Member', 'roles'),
('assign_mod_roles', 'Attribuer les rôles Mod et inférieurs', 'Peut attribuer Mod et Member', 'roles'),

-- Modération
('kick_users', 'Kicker des utilisateurs', 'Peut expulser temporairement des utilisateurs', 'moderation'),
('ban_users', 'Bannir des utilisateurs', 'Peut bannir définitivement des utilisateurs', 'moderation'),
('mute_users', 'Muter des utilisateurs', 'Peut couper le micro des utilisateurs', 'moderation'),
('warn_users', 'Avertir des utilisateurs', 'Peut envoyer des avertissements', 'moderation'),
('delete_messages', 'Supprimer des messages', 'Peut supprimer les messages du chat', 'moderation'),
('report_to_owner', 'Signaler au propriétaire', 'Peut signaler des problèmes au propriétaire', 'moderation'),

-- Membres
('invite_members', 'Inviter des membres', 'Peut inviter des utilisateurs dans le salon', 'members'),
('view_members', 'Voir la liste des membres', 'Peut voir tous les membres du salon', 'members'),

-- Média
('priority_media', 'Priorité micro/caméra', 'A la priorité pour le micro et la caméra', 'media'),
('share_files', 'Partager des fichiers', 'Peut partager des fichiers dans le salon', 'media'),
('request_mic', 'Demander le micro', 'Peut demander à activer le micro', 'media'),
('request_cam', 'Demander la caméra', 'Peut demander à activer la caméra', 'media'),

-- Base
('send_messages', 'Envoyer des messages', 'Peut envoyer des messages dans le chat', 'base'),
('view_chat', 'Voir le chat', 'Peut voir les messages du chat', 'base'),
('view_online_members', 'Voir les membres en ligne', 'Peut voir qui est connecté', 'base')

ON CONFLICT ("PermissionKey") DO NOTHING;

-- ═══════════════════════════════════════════════════════════════════════════════════
-- DONNÉES PAR DÉFAUT - Rôles système
-- ═══════════════════════════════════════════════════════════════════════════════════

INSERT INTO "RoomRoleDefinitions" ("RoleLevel", "RoleName", "DisplayName", "Description", "Icon", "Color", "IsSystem") VALUES
(1, 'RoomOwner', 'Propriétaire du Salon', 'Contrôle total sur le salon. Peut modifier tous les paramètres, gérer les rôles et supprimer le salon.', 'crown', '#FFD700', TRUE),
(2, 'RoomSuperAdmin', 'Super Administrateur', 'Pouvoirs étendus de gestion. Peut attribuer les rôles Admin et inférieurs.', 'shield-check', '#E74C3C', TRUE),
(3, 'RoomAdmin', 'Administrateur', 'Gère la modération et les membres. Peut attribuer les rôles Modérateur et inférieurs.', 'shield', '#9B59B6', TRUE),
(4, 'PowerUser', 'Utilisateur Avancé', 'Utilisateur de confiance avec des privilèges étendus comme le partage vidéo prioritaire.', 'bolt', '#3498DB', TRUE),
(5, 'RoomModerator', 'Modérateur', 'Surveille le chat et peut avertir ou muter les utilisateurs problématiques.', 'eye', '#2ECC71', TRUE),
(6, 'RoomMember', 'Membre', 'Membre standard du salon avec les permissions de base.', 'user', '#95A5A6', TRUE)
ON CONFLICT ("RoleName") DO NOTHING;

-- ═══════════════════════════════════════════════════════════════════════════════════
-- ATTRIBUTION DES PERMISSIONS PAR DÉFAUT
-- ═══════════════════════════════════════════════════════════════════════════════════

-- RoomOwner (Level 1) - Toutes les permissions
INSERT INTO "RoomRolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM "RoomRoleDefinitions" r, "RoomPermissions" p
WHERE r."RoleName" = 'RoomOwner'
ON CONFLICT DO NOTHING;

-- RoomSuperAdmin (Level 2) - Tout sauf delete_room et assign_all_roles
INSERT INTO "RoomRolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM "RoomRoleDefinitions" r, "RoomPermissions" p
WHERE r."RoleName" = 'RoomSuperAdmin'
  AND p."PermissionKey" NOT IN ('delete_room', 'assign_all_roles')
ON CONFLICT DO NOTHING;

-- RoomAdmin (Level 3) - Modération + membres
INSERT INTO "RoomRolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM "RoomRoleDefinitions" r, "RoomPermissions" p
WHERE r."RoleName" = 'RoomAdmin'
  AND p."PermissionKey" IN (
    'assign_mod_roles', 'kick_users', 'ban_users', 'mute_users', 'warn_users', 
    'delete_messages', 'invite_members', 'view_members', 'view_stats',
    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam'
  )
ON CONFLICT DO NOTHING;

-- PowerUser (Level 4) - Privilèges étendus
INSERT INTO "RoomRolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM "RoomRoleDefinitions" r, "RoomPermissions" p
WHERE r."RoleName" = 'PowerUser'
  AND p."PermissionKey" IN (
    'priority_media', 'invite_members', 'view_members', 'view_stats', 'share_files',
    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam'
  )
ON CONFLICT DO NOTHING;

-- RoomModerator (Level 5) - Surveillance
INSERT INTO "RoomRolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM "RoomRoleDefinitions" r, "RoomPermissions" p
WHERE r."RoleName" = 'RoomModerator'
  AND p."PermissionKey" IN (
    'mute_users', 'warn_users', 'delete_messages', 'report_to_owner', 'view_members',
    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam'
  )
ON CONFLICT DO NOTHING;

-- RoomMember (Level 6) - Base
INSERT INTO "RoomRolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM "RoomRoleDefinitions" r, "RoomPermissions" p
WHERE r."RoleName" = 'RoomMember'
  AND p."PermissionKey" IN (
    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam'
  )
ON CONFLICT DO NOTHING;

-- ═══════════════════════════════════════════════════════════════════════════════════
-- COMMENTAIRES
-- ═══════════════════════════════════════════════════════════════════════════════════
COMMENT ON TABLE "RoomRoleDefinitions" IS 'Définitions des rôles de salons avec métadonnées';
COMMENT ON TABLE "RoomPermissions" IS 'Liste des permissions disponibles pour les rôles de salons';
COMMENT ON TABLE "RoomRolePermissions" IS 'Attribution des permissions aux rôles (Many-to-Many)';
