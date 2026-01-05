-- ============================================
-- PaL.Xtreme - Video Call Tables
-- Version 1.6.0
-- ============================================

-- Table pour les appels vidéo
CREATE TABLE IF NOT EXISTS "VideoCalls" (
    "Id" SERIAL PRIMARY KEY,
    "CallId" UUID NOT NULL DEFAULT gen_random_uuid(),
    "CallerId" INTEGER NOT NULL REFERENCES "Users"("Id"),
    "CalleeId" INTEGER NOT NULL REFERENCES "Users"("Id"),
    "Status" INTEGER NOT NULL DEFAULT 0,
    "StartTime" TIMESTAMP,
    "EndTime" TIMESTAMP,
    "Duration" INTEGER DEFAULT 0,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

-- Index pour recherche rapide
CREATE INDEX IF NOT EXISTS idx_videocalls_callid ON "VideoCalls"("CallId");
CREATE INDEX IF NOT EXISTS idx_videocalls_caller ON "VideoCalls"("CallerId");
CREATE INDEX IF NOT EXISTS idx_videocalls_callee ON "VideoCalls"("CalleeId");
CREATE INDEX IF NOT EXISTS idx_videocalls_status ON "VideoCalls"("Status");

-- Table pour les logs d'appels vidéo
CREATE TABLE IF NOT EXISTS "VideoCallLogs" (
    "Id" SERIAL PRIMARY KEY,
    "VideoCallId" INTEGER REFERENCES "VideoCalls"("Id") ON DELETE CASCADE,
    "Event" VARCHAR(50) NOT NULL,
    "Details" TEXT,
    "Timestamp" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_videocalllogs_callid ON "VideoCallLogs"("VideoCallId");

-- Commentaires
COMMENT ON TABLE "VideoCalls" IS 'Appels vidéo - Status: 0=Pending, 1=Ringing, 2=Active, 3=Ended, 4=Missed, 5=Declined';
COMMENT ON COLUMN "VideoCalls"."CallId" IS 'UUID unique pour identifier la session WebRTC';
COMMENT ON COLUMN "VideoCalls"."Duration" IS 'Durée en secondes';
