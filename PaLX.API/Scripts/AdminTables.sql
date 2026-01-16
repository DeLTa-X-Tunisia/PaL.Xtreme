-- ============================================
-- Tables pour le Panel d'Administration PaL.X.Admin
-- Base de données: PaL.X
-- ============================================

-- Table des bans utilisateurs
CREATE TABLE IF NOT EXISTS "UserBans" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Reason" VARCHAR(500) NOT NULL,
    "BannedById" INTEGER REFERENCES "Users"("Id"),
    "ExpiresAt" TIMESTAMP, -- NULL = permanent
    "IsActive" BOOLEAN DEFAULT true,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_userbans_userid ON "UserBans"("UserId");
CREATE INDEX IF NOT EXISTS idx_userbans_active ON "UserBans"("IsActive") WHERE "IsActive" = true;

-- Table des avertissements
CREATE TABLE IF NOT EXISTS "UserWarnings" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Reason" VARCHAR(500) NOT NULL,
    "WarnedById" INTEGER REFERENCES "Users"("Id"),
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_userwarnings_userid ON "UserWarnings"("UserId");

-- Table des signalements
CREATE TABLE IF NOT EXISTS "Reports" (
    "Id" SERIAL PRIMARY KEY,
    "ReporterId" INTEGER NOT NULL REFERENCES "Users"("Id"),
    "ReportedUserId" INTEGER REFERENCES "Users"("Id"),
    "ReportedMessageId" INTEGER,
    "ReportedRoomId" INTEGER,
    "Reason" VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "Status" VARCHAR(20) DEFAULT 'Pending', -- Pending, Reviewing, Resolved, Dismissed
    "Resolution" TEXT,
    "ResolvedAt" TIMESTAMP,
    "ResolvedById" INTEGER REFERENCES "Users"("Id"),
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_reports_status ON "Reports"("Status");
CREATE INDEX IF NOT EXISTS idx_reports_reporter ON "Reports"("ReporterId");
CREATE INDEX IF NOT EXISTS idx_reports_reported ON "Reports"("ReportedUserId");

-- Table des logs d'audit admin
CREATE TABLE IF NOT EXISTS "AdminAuditLogs" (
    "Id" SERIAL PRIMARY KEY,
    "AdminId" INTEGER NOT NULL REFERENCES "Users"("Id"),
    "Action" VARCHAR(50) NOT NULL,
    "TargetType" VARCHAR(20), -- User, Room, Report, System
    "TargetId" INTEGER,
    "Details" TEXT,
    "IpAddress" VARCHAR(45),
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_adminauditlogs_adminid ON "AdminAuditLogs"("AdminId");
CREATE INDEX IF NOT EXISTS idx_adminauditlogs_createdat ON "AdminAuditLogs"("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_adminauditlogs_action ON "AdminAuditLogs"("Action");

-- Table des abonnements (si elle n'existe pas)
CREATE TABLE IF NOT EXISTS "UserSubscriptions" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "SubscriptionType" INTEGER NOT NULL, -- 0=Free, 1=Premium, 2=VIP
    "StartDate" TIMESTAMP DEFAULT NOW(),
    "EndDate" TIMESTAMP NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_usersubscriptions_userid ON "UserSubscriptions"("UserId");
CREATE INDEX IF NOT EXISTS idx_usersubscriptions_enddate ON "UserSubscriptions"("EndDate");

-- Ajouter la colonne CreatedAt à Users si elle n'existe pas
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Users' AND column_name = 'CreatedAt'
    ) THEN
        ALTER TABLE "Users" ADD COLUMN "CreatedAt" TIMESTAMP DEFAULT NOW();
    END IF;
END $$;

-- ============================================
-- Vues utiles pour les statistiques
-- ============================================

-- Vue des utilisateurs en ligne
CREATE OR REPLACE VIEW "OnlineUsersView" AS
SELECT DISTINCT u."Id", u."Username", s."ConnectéLe" as "ConnectedAt"
FROM "Users" u
JOIN "UserSessions" s ON u."Id" = s."UserId"
WHERE s."DéconnectéLe" IS NULL;

-- Vue des statistiques globales
CREATE OR REPLACE VIEW "AdminStatsView" AS
SELECT 
    (SELECT COUNT(*) FROM "Users") as total_users,
    (SELECT COUNT(DISTINCT "UserId") FROM "UserSessions" WHERE "DéconnectéLe" IS NULL) as online_users,
    (SELECT COUNT(*) FROM "Rooms" WHERE "IsActive" = true) as active_rooms,
    (SELECT COUNT(*) FROM "Reports" WHERE "Status" = 'Pending') as pending_reports,
    (SELECT COUNT(*) FROM "UserSubscriptions" WHERE "SubscriptionType" = 1 AND "EndDate" > NOW()) as premium_users,
    (SELECT COUNT(*) FROM "UserSubscriptions" WHERE "SubscriptionType" = 2 AND "EndDate" > NOW()) as vip_users;

-- ============================================
-- COMMENTAIRES
-- ============================================
COMMENT ON TABLE "UserBans" IS 'Table des bannissements utilisateurs';
COMMENT ON TABLE "UserWarnings" IS 'Table des avertissements utilisateurs';
COMMENT ON TABLE "Reports" IS 'Table des signalements';
COMMENT ON TABLE "AdminAuditLogs" IS 'Logs d''audit des actions administrateur';
COMMENT ON TABLE "UserSubscriptions" IS 'Abonnements Premium/VIP des utilisateurs';
