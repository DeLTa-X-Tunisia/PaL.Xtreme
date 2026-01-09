-- Script: fix_room_member_defaults.sql
-- Date: 2026-01-09
-- Description: Corriger les valeurs par défaut des colonnes IsCamOn, IsMicOn, etc.
--              pour que les nouveaux membres rejoignent avec caméra et micro OFF par défaut.

-- 1. Corriger les entrées existantes où IsCamOn=true alors que personne n'a vraiment activé sa caméra
UPDATE "RoomMembers" 
SET "IsCamOn" = false, "IsMicOn" = false 
WHERE "IsCamOn" = true OR "IsMicOn" = true;

-- 2. Modifier les valeurs par défaut des colonnes (si elles ne sont pas déjà correctes)
ALTER TABLE "RoomMembers" 
ALTER COLUMN "IsCamOn" SET DEFAULT false;

ALTER TABLE "RoomMembers" 
ALTER COLUMN "IsMicOn" SET DEFAULT false;

ALTER TABLE "RoomMembers" 
ALTER COLUMN "HasHandRaised" SET DEFAULT false;

ALTER TABLE "RoomMembers" 
ALTER COLUMN "IsMuted" SET DEFAULT false;

-- Vérification
SELECT column_name, column_default 
FROM information_schema.columns 
WHERE table_name = 'RoomMembers' 
AND column_name IN ('IsCamOn', 'IsMicOn', 'HasHandRaised', 'IsMuted');
