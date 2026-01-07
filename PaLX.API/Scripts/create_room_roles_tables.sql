-- ═══════════════════════════════════════════════════════════════════════════════════
-- ROOM ROLES MANAGEMENT TABLES
-- Tables pour la gestion des rôles dans les salons
-- Roles: RoomSuperAdmin, RoomAdmin, RoomModerator
-- ═══════════════════════════════════════════════════════════════════════════════════

-- Table des rôles actifs dans les salons (attribution User <-> Room)
-- Note: RoomRoles existe déjà pour les définitions de rôles génériques
CREATE TABLE IF NOT EXISTS "RoomMemberRoles" (
    "Id" SERIAL PRIMARY KEY,
    "RoomId" INTEGER NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Role" VARCHAR(50) NOT NULL, -- RoomSuperAdmin, RoomAdmin, RoomModerator
    "AssignedBy" INTEGER NOT NULL REFERENCES "Users"("Id"),
    "AssignedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "RemovedAt" TIMESTAMP NULL,
    CONSTRAINT "UQ_RoomMemberRoles_Room_User" UNIQUE ("RoomId", "UserId")
);

-- Index pour les recherches
CREATE INDEX IF NOT EXISTS "IX_RoomMemberRoles_RoomId" ON "RoomMemberRoles"("RoomId");
CREATE INDEX IF NOT EXISTS "IX_RoomMemberRoles_UserId" ON "RoomMemberRoles"("UserId");
CREATE INDEX IF NOT EXISTS "IX_RoomMemberRoles_Active" ON "RoomMemberRoles"("RoomId", "IsActive");

-- Table des demandes de rôles (notifications)
CREATE TABLE IF NOT EXISTS "RoomRoleRequests" (
    "Id" SERIAL PRIMARY KEY,
    "RoomId" INTEGER NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
    "RequesterId" INTEGER NOT NULL REFERENCES "Users"("Id"), -- Le propriétaire du salon
    "TargetUserId" INTEGER NOT NULL REFERENCES "Users"("Id"), -- L'ami à qui on propose le rôle
    "Role" VARCHAR(50) NOT NULL, -- RoomSuperAdmin, RoomAdmin, RoomModerator
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Accepted, Declined, Expired
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "RespondedAt" TIMESTAMP NULL,
    "ExpiresAt" TIMESTAMP NULL DEFAULT (NOW() + INTERVAL '7 days')
);

-- Index pour les recherches
CREATE INDEX IF NOT EXISTS "IX_RoomRoleRequests_Target" ON "RoomRoleRequests"("TargetUserId", "Status");
CREATE INDEX IF NOT EXISTS "IX_RoomRoleRequests_Room" ON "RoomRoleRequests"("RoomId");

-- ═══════════════════════════════════════════════════════════════════════════════════
-- COMMENT: Hiérarchie des rôles de salon
-- ═══════════════════════════════════════════════════════════════════════════════════
-- RoomOwner (créateur du salon, non stocké ici - utilise Rooms.OwnerId)
--   └─> RoomSuperAdmin (👑 peut tout faire sauf supprimer le salon)
--         └─> RoomAdmin (⭐ peut modérer + gérer les membres)
--               └─> RoomModerator (🔧 peut modérer le chat/kick temporaire)
--
-- PowerUser est un abonnement personnel, séparé des rôles de salon
-- ═══════════════════════════════════════════════════════════════════════════════════
