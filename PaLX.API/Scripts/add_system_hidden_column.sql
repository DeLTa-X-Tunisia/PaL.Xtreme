-- Script: add_system_hidden_column.sql
-- Date: 2026-01-08
-- Description: Ajoute la colonne IsSystemHidden pour permettre aux admins système
--              de cacher un salon même au RoomOwner

-- Ajouter la colonne IsSystemHidden à la table Rooms
ALTER TABLE "Rooms" ADD COLUMN IF NOT EXISTS "IsSystemHidden" BOOLEAN NOT NULL DEFAULT FALSE;

-- Commentaire sur la colonne
COMMENT ON COLUMN "Rooms"."IsSystemHidden" IS 
'Quand TRUE, le salon est caché même au RoomOwner. Seuls les admins système (RoleLevel 1-5) peuvent le voir.';

-- Index pour optimiser les requêtes
CREATE INDEX IF NOT EXISTS idx_rooms_system_hidden ON "Rooms" ("IsSystemHidden");
