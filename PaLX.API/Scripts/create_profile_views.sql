-- Script de création de la table ProfileViews
-- Permet d'enregistrer qui a consulté le profil de qui
-- Utilisé pour la fonctionnalité "Qui a vu mon profil"

CREATE TABLE IF NOT EXISTS "ProfileViews" (
    "Id" SERIAL PRIMARY KEY,
    "ViewerId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "ViewedUserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "ViewedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "Context" VARCHAR(50) NOT NULL DEFAULT 'room',
    CONSTRAINT "UQ_ProfileViews_Viewer_Viewed_Date" UNIQUE ("ViewerId", "ViewedUserId", "ViewedAt")
);

-- Index pour les requêtes fréquentes
CREATE INDEX IF NOT EXISTS "IX_ProfileViews_ViewedUserId" ON "ProfileViews" ("ViewedUserId");
CREATE INDEX IF NOT EXISTS "IX_ProfileViews_ViewerId" ON "ProfileViews" ("ViewerId");
CREATE INDEX IF NOT EXISTS "IX_ProfileViews_ViewedAt" ON "ProfileViews" ("ViewedAt" DESC);

-- Commentaires
COMMENT ON TABLE "ProfileViews" IS 'Historique des consultations de profil';
COMMENT ON COLUMN "ProfileViews"."ViewerId" IS 'ID de l''utilisateur qui consulte';
COMMENT ON COLUMN "ProfileViews"."ViewedUserId" IS 'ID de l''utilisateur dont le profil est consulté';
COMMENT ON COLUMN "ProfileViews"."ViewedAt" IS 'Date et heure de la consultation';
COMMENT ON COLUMN "ProfileViews"."Context" IS 'Contexte: room, friends_list, search, etc.';
