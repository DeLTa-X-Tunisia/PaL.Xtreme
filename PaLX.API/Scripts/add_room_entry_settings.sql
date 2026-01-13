-- Script d'ajout des conditions d'entrée par défaut pour les salons
-- Permet à l'admin de définir les permissions par défaut des nouveaux utilisateurs

-- Ajouter les colonnes pour les conditions d'entrée
ALTER TABLE "Rooms" ADD COLUMN IF NOT EXISTS "DefaultTextEnabled" BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE "Rooms" ADD COLUMN IF NOT EXISTS "DefaultMicEnabled" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "Rooms" ADD COLUMN IF NOT EXISTS "DefaultCamEnabled" BOOLEAN NOT NULL DEFAULT FALSE;

-- Commentaires
COMMENT ON COLUMN "Rooms"."DefaultTextEnabled" IS 'Si TRUE, les nouveaux utilisateurs peuvent écrire dans le chat par défaut';
COMMENT ON COLUMN "Rooms"."DefaultMicEnabled" IS 'Si TRUE, les nouveaux utilisateurs ont le micro activé par défaut';
COMMENT ON COLUMN "Rooms"."DefaultCamEnabled" IS 'Si TRUE, les nouveaux utilisateurs ont la caméra activée par défaut';
