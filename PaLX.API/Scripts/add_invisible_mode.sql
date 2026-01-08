-- Script: add_invisible_mode.sql
-- Date: 2026-01-08
-- Description: Ajoute le mode invisible pour les admins système dans les salons

-- Ajouter la colonne IsInvisible à la table RoomMembers
ALTER TABLE "RoomMembers" ADD COLUMN IF NOT EXISTS "IsInvisible" BOOLEAN NOT NULL DEFAULT FALSE;

-- Commentaire sur la colonne
COMMENT ON COLUMN "RoomMembers"."IsInvisible" IS 
'Quand TRUE, le membre est invisible pour les utilisateurs normaux et les admins de niveau inférieur. 
Seuls les admins système de niveau égal ou supérieur peuvent le voir.';

-- Index pour optimiser les requêtes
CREATE INDEX IF NOT EXISTS idx_room_members_invisible ON "RoomMembers" ("IsInvisible");
