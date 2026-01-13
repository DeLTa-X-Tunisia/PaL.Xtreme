-- ═══════════════════════════════════════════════════════════════════════════════════
-- ROOM BANS MANAGEMENT - v1.8.4
-- Table pour la gestion des kicks et bannissements dans les salons
-- ═══════════════════════════════════════════════════════════════════════════════════

-- Table des bannissements de salon
CREATE TABLE IF NOT EXISTS "RoomBans" (
    "Id" SERIAL PRIMARY KEY,
    
    -- Références
    "RoomId" INTEGER NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "BannedBy" INTEGER NOT NULL REFERENCES "Users"("Id"),
    
    -- Détails du ban
    "Reason" VARCHAR(500) NULL,                           -- Raison du kick/ban (optionnel)
    "BanType" VARCHAR(20) NOT NULL DEFAULT 'Permanent',   -- 'Kick', 'Temporary', 'Permanent'
    "DurationMinutes" INTEGER NULL,                        -- Durée en minutes (NULL = permanent)
    
    -- Timestamps
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "ExpiresAt" TIMESTAMP NULL,                            -- NULL = permanent, sinon date d'expiration
    
    -- État
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,              -- FALSE = débanni
    "UnbannedBy" INTEGER NULL REFERENCES "Users"("Id"),   -- Qui a débanni
    "UnbannedAt" TIMESTAMP NULL                            -- Quand le déban a eu lieu
);

-- Index pour les recherches rapides
CREATE INDEX IF NOT EXISTS "IX_RoomBans_RoomId" ON "RoomBans"("RoomId");
CREATE INDEX IF NOT EXISTS "IX_RoomBans_UserId" ON "RoomBans"("UserId");
CREATE INDEX IF NOT EXISTS "IX_RoomBans_Active" ON "RoomBans"("RoomId", "IsActive") WHERE "IsActive" = TRUE;
CREATE INDEX IF NOT EXISTS "IX_RoomBans_Expires" ON "RoomBans"("ExpiresAt") WHERE "ExpiresAt" IS NOT NULL AND "IsActive" = TRUE;

-- Index unique pour éviter les doublons de bans actifs
-- Un utilisateur ne peut avoir qu'UN SEUL ban actif par salon
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RoomBans_Unique_Active" 
ON "RoomBans"("RoomId", "UserId") 
WHERE "IsActive" = TRUE AND "BanType" != 'Kick';

-- ═══════════════════════════════════════════════════════════════════════════════════
-- COMMENT: Types de bannissement
-- ═══════════════════════════════════════════════════════════════════════════════════
-- Kick      : Éjection temporaire sans ban (peut revenir immédiatement)
--             DurationMinutes = NULL, ExpiresAt = NULL
--             Permissions: Moderator+
--
-- Temporary : Ban temporaire (1h, 24h, 7j, etc.)
--             DurationMinutes = durée, ExpiresAt = CreatedAt + Duration
--             Permissions: Admin+
--
-- Permanent : Ban définitif jusqu'à déban manuel
--             DurationMinutes = NULL, ExpiresAt = NULL
--             Permissions: Owner, SuperAdmin, ou SystemAdmin
-- ═══════════════════════════════════════════════════════════════════════════════════

-- ═══════════════════════════════════════════════════════════════════════════════════
-- COMMENT: Hiérarchie des permissions (RoomRoleLevel)
-- ═══════════════════════════════════════════════════════════════════════════════════
-- Owner (1)      : Peut tout faire (kick, ban temp, ban perm, unban)
-- SuperAdmin (2) : Peut tout faire (kick, ban temp, ban perm, unban)
-- Admin (3)      : Peut kick, ban temporaire (≤7j), voir la liste des bans
-- PowerUser (4)  : Pas de permissions de modération
-- Moderator (5)  : Peut kick seulement
-- Member (6)     : Pas de permissions
--
-- Note: SystemAdmin (RoleLevel 1-5) peuvent agir dans tous les salons
-- ═══════════════════════════════════════════════════════════════════════════════════
